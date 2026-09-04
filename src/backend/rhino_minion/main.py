import json
from typing import Any
from uuid import UUID

from fastapi import FastAPI, HTTPException, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from rhino_minion.bridge import RhinoConnection, registry
from rhino_minion.config import settings
from rhino_minion.models import BridgeRequest, BridgeResponse, HealthResponse, PROTOCOL_VERSION

app = FastAPI(title="RHINO Minion Backend", version="0.1.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://127.0.0.1:5173", "http://localhost:5173"],
    allow_credentials=False,
    allow_methods=["GET", "POST"],
    allow_headers=["Content-Type"],
)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", protocol_version=PROTOCOL_VERSION)


class ExecuteRequest(BaseModel):
    type: str
    payload: dict[str, Any]


@app.get("/api/sessions")
def sessions() -> list[dict[str, str]]:
    return registry.list()


@app.post("/api/sessions/{session_id}/execute", response_model=BridgeResponse)
async def execute(session_id: UUID, command: ExecuteRequest) -> BridgeResponse:
    connection = registry.get(session_id)
    if connection is None:
        raise HTTPException(status_code=404, detail="Rhino session not connected")
    try:
        return await connection.request(command.type, command.payload)
    except TimeoutError as exception:
        raise HTTPException(status_code=504, detail="Rhino request timed out") from exception
    except ConnectionError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception


@app.websocket("/ws/rhino")
async def rhino_websocket(websocket: WebSocket) -> None:
    if websocket.client is None or websocket.client.host not in {"127.0.0.1", "::1", "testclient"}:
        await websocket.close(code=1008, reason="Loopback connections only")
        return

    await websocket.accept()
    connection: RhinoConnection | None = None
    try:
        raw_handshake = await websocket.receive_text()
        handshake = BridgeRequest.model_validate_json(raw_handshake)
        if handshake.protocol_version != PROTOCOL_VERSION or handshake.type != "handshake":
            await websocket.close(code=1002, reason="Invalid handshake")
            return
        if settings.bridge_secret and handshake.payload.get("secret") != settings.bridge_secret:
            await websocket.close(code=1008, reason="Invalid bridge secret")
            return

        connection = RhinoConnection(
            session_id=handshake.session_id,
            websocket=websocket,
            rhino_version=str(handshake.payload.get("rhino_version", "unknown")),
        )
        registry.register(connection)
        while True:
            raw_response = await websocket.receive_text()
            response = BridgeResponse.model_validate(json.loads(raw_response))
            connection.resolve(response)
    except WebSocketDisconnect:
        pass
    finally:
        if connection is not None:
            registry.unregister(connection)
