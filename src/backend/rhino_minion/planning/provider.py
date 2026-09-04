import json
import re
from abc import ABC, abstractmethod
from typing import Any

import httpx

from rhino_minion.config import settings
from rhino_minion.planning.models import GeometryPlan, PlannedOperation
from rhino_minion.planning.prompts import GEOMETRY_PLAN_SCHEMA, SYSTEM_INSTRUCTIONS


class PlanningError(RuntimeError):
    pass


class Planner(ABC):
    @abstractmethod
    async def plan(self, prompt: str, scene: dict[str, Any]) -> GeometryPlan:
        raise NotImplementedError


class DeterministicPlanner(Planner):
    """Offline fallback for an explicit rectangular box request."""

    _dimensions = re.compile(
        r"(?P<a>\d+(?:[.,]\d+)?)\s*[x×х]\s*"
        r"(?P<b>\d+(?:[.,]\d+)?)\s*[x×х]\s*"
        r"(?P<c>\d+(?:[.,]\d+)?)\s*(?P<unit>mm|cm|m|мм|см|м)?",
        re.IGNORECASE,
    )

    async def plan(self, prompt: str, scene: dict[str, Any]) -> GeometryPlan:
        match = self._dimensions.search(prompt)
        if match is None or not re.search(r"box|короб|объ[её]м|блок", prompt, re.IGNORECASE):
            raise PlanningError(
                "Local planner supports only box requests such as 'Create a box 30 × 20 × 80 m'. "
                "Configure the OpenAI planner for free-form prompts."
            )

        values = [float(match.group(key).replace(",", ".")) for key in ("a", "b", "c")]
        factor = _unit_factor(match.group("unit") or "m", str(scene.get("units", "")))
        dimensions = [value * factor for value in values]
        return GeometryPlan(
            summary=f"Create a {values[0]:g} × {values[1]:g} × {values[2]:g} box.",
            operations=[
                PlannedOperation(
                    operation_id="create-box-1",
                    tool="create_box",
                    arguments={
                        "origin": [0, 0, 0],
                        "width": dimensions[0],
                        "depth": dimensions[1],
                        "height": dimensions[2],
                        "layer": "AI_Massing",
                        "name": "AI_Box",
                    },
                )
            ],
        )


class OpenAIPlanner(Planner):
    def __init__(self, api_key: str, model: str) -> None:
        if not api_key or not model:
            raise PlanningError(
                "RHINO_MINION_OPENAI_API_KEY and RHINO_MINION_OPENAI_MODEL are required."
            )
        self._api_key = api_key
        self._model = model

    async def plan(self, prompt: str, scene: dict[str, Any]) -> GeometryPlan:
        body = {
            "model": self._model,
            "store": False,
            "max_output_tokens": 3000,
            "instructions": SYSTEM_INSTRUCTIONS,
            "input": f"USER REQUEST:\n{prompt}\n\nRHINO SCENE:\n{json.dumps(scene)}",
            "text": {
                "format": {
                    "type": "json_schema",
                    "name": "geometry_plan",
                    "strict": True,
                    "schema": GEOMETRY_PLAN_SCHEMA,
                }
            },
        }
        async with httpx.AsyncClient(timeout=60) as client:
            response = await client.post(
                "https://api.openai.com/v1/responses",
                headers={"Authorization": f"Bearer {self._api_key}"},
                json=body,
            )
        if response.is_error:
            raise PlanningError(f"OpenAI planner failed with status {response.status_code}.")

        payload = response.json()
        output_text = _response_output_text(payload)
        try:
            raw_plan = json.loads(output_text)
            for operation in raw_plan.get("operations", []):
                operation["arguments"] = json.loads(operation.pop("arguments_json"))
            return GeometryPlan.model_validate(raw_plan)
        except (TypeError, ValueError) as exception:
            raise PlanningError("OpenAI returned an invalid geometry plan.") from exception


def get_planner() -> Planner:
    if settings.planner == "deterministic":
        return DeterministicPlanner()
    if settings.planner == "openai":
        return OpenAIPlanner(settings.openai_api_key, settings.openai_model)
    raise PlanningError(f"Unknown planner: {settings.planner}")


def _response_output_text(payload: dict[str, Any]) -> str:
    for item in payload.get("output", []):
        if item.get("type") != "message":
            continue
        for content in item.get("content", []):
            if content.get("type") == "output_text":
                return str(content.get("text", ""))
    raise PlanningError("OpenAI response did not contain output text.")


def _unit_factor(source: str, destination: str) -> float:
    meters = {"m": 1.0, "м": 1.0, "cm": 0.01, "см": 0.01, "mm": 0.001, "мм": 0.001}
    units_per_meter = {
        "Millimeters": 1000.0,
        "Centimeters": 100.0,
        "Decimeters": 10.0,
        "Meters": 1.0,
        "Inches": 39.3700787402,
        "Feet": 3.280839895,
    }
    if source.lower() not in meters or destination not in units_per_meter:
        raise PlanningError(f"Unsupported unit conversion: {source} to {destination or 'unknown'}.")
    return meters[source.lower()] * units_per_meter[destination]
