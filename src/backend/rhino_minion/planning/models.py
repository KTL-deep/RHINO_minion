from typing import Any

from pydantic import Field, model_validator

from rhino_minion.models import StrictModel

ALLOWED_TOOLS = {
    "create_point",
    "create_line",
    "create_circle",
    "create_arc",
    "create_ellipse",
    "create_rectangle",
    "create_polygon",
    "create_nurbs_curve",
    "create_box",
    "create_sphere",
    "create_cylinder",
    "create_polyline",
    "extrude",
    "transform",
    "duplicate_objects",
    "delete_objects",
    "set_object_attributes",
}


class PlannedOperation(StrictModel):
    operation_id: str = Field(min_length=1, max_length=80)
    tool: str
    arguments: dict[str, Any]

    @model_validator(mode="after")
    def supported_tool(self) -> "PlannedOperation":
        if self.tool not in ALLOWED_TOOLS:
            raise ValueError(f"unsupported tool: {self.tool}")
        return self


class GeometryPlan(StrictModel):
    summary: str = Field(min_length=1, max_length=500)
    operations: list[PlannedOperation] = Field(max_length=50)

    @model_validator(mode="after")
    def unique_operation_ids(self) -> "GeometryPlan":
        identifiers = [operation.operation_id for operation in self.operations]
        if len(identifiers) != len(set(identifiers)):
            raise ValueError("operation IDs must be unique")
        return self


class PromptResult(StrictModel):
    message: str
    plan: GeometryPlan
    rhino: dict[str, Any]
