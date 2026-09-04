import asyncio
from dataclasses import dataclass, field
from typing import Any
from uuid import UUID, uuid4

from fastapi import WebSocket

from rhino_minion.models import PROTOCOL_VERSION, BridgeRequest, BridgeResponse


@dataclass
class RhinoConnection:
    session_id: UUID
    websocket: WebSocket
    rhino_version: str
    pending: dict[UUID, asyncio.Future[BridgeResponse]] = field(default_factory=dict)
    send_lock: asyncio.Lock = field(default_factory=asyncio.Lock)

    async def request(self, message_type: str, payload: dict[str, Any]) -> BridgeResponse:
        request = BridgeRequest(
            protocol_version=PROTOCOL_VERSION,
            request_id=uuid4(),
            session_id=self.session_id,
            type=message_type,
            payload=payload,
        )
        loop = asyncio.get_running_loop()
        result = loop.create_future()
        self.pending[request.request_id] = result
        try:
            async with self.send_lock:
                await self.websocket.send_text(request.model_dump_json())
            return await asyncio.wait_for(result, timeout=30)
        finally:
            self.pending.pop(request.request_id, None)

    def resolve(self, response: BridgeResponse) -> None:
        future = self.pending.get(response.request_id)
        if future is not None and not future.done():
            future.set_result(response)

    def disconnect(self) -> None:
        for future in self.pending.values():
            if not future.done():
                future.set_exception(ConnectionError("Rhino bridge disconnected"))
        self.pending.clear()


class BridgeRegistry:
    def __init__(self) -> None:
        self._connections: dict[UUID, RhinoConnection] = {}

    def register(self, connection: RhinoConnection) -> None:
        previous = self._connections.get(connection.session_id)
        if previous is not None:
            previous.disconnect()
        self._connections[connection.session_id] = connection

    def unregister(self, connection: RhinoConnection) -> None:
        if self._connections.get(connection.session_id) is connection:
            self._connections.pop(connection.session_id, None)
        connection.disconnect()

    def get(self, session_id: UUID) -> RhinoConnection | None:
        return self._connections.get(session_id)

    def list(self) -> list[dict[str, str]]:
        return [
            {"session_id": str(connection.session_id), "rhino_version": connection.rhino_version}
            for connection in self._connections.values()
        ]


registry = BridgeRegistry()
