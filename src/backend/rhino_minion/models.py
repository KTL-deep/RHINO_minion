from enum import StrEnum
from typing import Any
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator

PROTOCOL_VERSION = "0.1"


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid")


class ErrorCode(StrEnum):
    INVALID_ARGUMENT = "INVALID_ARGUMENT"
    OBJECT_NOT_FOUND = "OBJECT_NOT_FOUND"
    GEOMETRY_FAILED = "GEOMETRY_FAILED"
    DOCUMENT_UNAVAILABLE = "DOCUMENT_UNAVAILABLE"
    TIMEOUT = "TIMEOUT"
    PROTOCOL_ERROR = "PROTOCOL_ERROR"
    INTERNAL_ERROR = "INTERNAL_ERROR"


class BridgeError(StrictModel):
    code: ErrorCode
    message: str = Field(min_length=1)
    operation_id: str | None = None
    details: dict[str, Any] = Field(default_factory=dict)


class BridgeRequest(StrictModel):
    protocol_version: str
    request_id: UUID
    session_id: UUID
    type: str = Field(min_length=1)
    payload: dict[str, Any]


class BridgeResponse(StrictModel):
    request_id: UUID
    ok: bool
    result: dict[str, Any] | None = None
    error: BridgeError | None = None

    @model_validator(mode="after")
    def result_matches_status(self) -> "BridgeResponse":
        if self.ok and self.error is not None:
            raise ValueError("successful response cannot contain an error")
        if not self.ok and self.error is None:
            raise ValueError("failed response must contain an error")
        return self


class HealthResponse(StrictModel):
    status: str
    protocol_version: str
