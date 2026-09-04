import json
from typing import Any
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, HTTPException, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from rhino_minion.auth import CurrentUser, generation_user
from rhino_minion.billing.routes import repository, router as billing_router
from rhino_minion.bridge import RhinoConnection, registry
from rhino_minion.config import settings
from rhino_minion.models import BridgeRequest, BridgeResponse, HealthResponse, PROTOCOL_VERSION
from rhino_minion.planning import PlanningError, get_planner
from rhino_minion.planning.models import PromptResult

app = FastAPI(title="RHINO Minion Backend", version="0.1.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://127.0.0.1:5173", "http://localhost:5173"],
    allow_credentials=False,
    allow_methods=["GET", "POST"],
    allow_headers=["Authorization", "Content-Type"],
)
app.include_router(billing_router)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", protocol_version=PROTOCOL_VERSION)


class ExecuteRequest(BaseModel):
    type: str
    payload: dict[str, Any]


class PromptRequest(BaseModel):
    prompt: str


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


@app.post("/api/sessions/{session_id}/prompt", response_model=PromptResult)
async def prompt(
    session_id: UUID,
    request: PromptRequest,
    user: CurrentUser = Depends(generation_user),
) -> PromptResult:
    text = request.prompt.strip()
    if not text or len(text) > 4000:
        raise HTTPException(status_code=422, detail="Prompt must contain 1-4000 characters")

    connection = registry.get(session_id)
    if connection is None:
        raise HTTPException(status_code=404, detail="Rhino session not connected")
    generation_id = str(uuid4())
    credit_reserved = False
    if settings.auth_enabled:
        credit_reserved = repository().consume_credit(user.id, generation_id)
        if not credit_reserved:
            raise HTTPException(status_code=402, detail="Not enough RHINO Minion credits")
    try:
        scene_response = await connection.request("get_scene", {"max_objects": 500})
        if not scene_response.ok or scene_response.result is None:
            message = (
                scene_response.error.message
                if scene_response.error
                else "Could not read Rhino scene"
            )
            raise HTTPException(status_code=502, detail=message)

        plan = await get_planner().plan(text, scene_response.result)
        if len(plan.operations) > settings.max_plan_operations:
            raise HTTPException(status_code=422, detail="Geometry plan exceeds operation limit")
        if not plan.operations:
            return PromptResult(
                message=plan.summary,
                plan=plan,
                rhino={"ok": True, "result": {"operations": []}},
            )

        rhino_response = await connection.request(
            "execute_batch",
            {
                "label": f"RHINO Minion: {plan.summary[:80]}",
                "operations": [operation.model_dump() for operation in plan.operations],
            },
        )
        if not rhino_response.ok:
            message = (
                rhino_response.error.message
                if rhino_response.error
                else "Rhino operation failed"
            )
            raise HTTPException(status_code=422, detail=message)
        return PromptResult(
            message=plan.summary,
            plan=plan,
            rhino=rhino_response.model_dump(mode="json"),
        )
    except PlanningError as exception:
        if credit_reserved:
            repository().release_credit(user.id, generation_id)
        raise HTTPException(status_code=422, detail=str(exception)) from exception
    except TimeoutError as exception:
        if credit_reserved:
            repository().release_credit(user.id, generation_id)
        raise HTTPException(
            status_code=504,
            detail="Planning or Rhino execution timed out",
        ) from exception
    except ConnectionError as exception:
        if credit_reserved:
            repository().release_credit(user.id, generation_id)
        raise HTTPException(status_code=503, detail=str(exception)) from exception
    except HTTPException:
        if credit_reserved:
            repository().release_credit(user.id, generation_id)
        raise


@app.websocket("/ws/rhino")
async def rhino_websocket(websocket: WebSocket) -> None:
    allowed_hosts = {"127.0.0.1", "::1", "testclient"}
    if websocket.client is None or websocket.client.host not in allowed_hosts:
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
        supplied_secret = handshake.payload.get("secret")
        if settings.bridge_secret and supplied_secret != settings.bridge_secret:
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
