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
    """Offline Russian/English fallback for basic creation and selected edits."""

    _dimensions = re.compile(
        r"(?P<a>\d+(?:[.,]\d+)?)\s*[xх×]\s*"
        r"(?P<b>\d+(?:[.,]\d+)?)\s*[xх×]\s*"
        r"(?P<c>\d+(?:[.,]\d+)?)\s*(?P<unit>mm|cm|m|мм|см|м)?",
        re.IGNORECASE,
    )
    _number_with_unit = re.compile(
        r"(?P<value>\d+(?:[.,]\d+)?)\s*(?P<unit>mm|cm|m|мм|см|м)\b",
        re.IGNORECASE,
    )
    _percent = re.compile(r"(?P<value>\d+(?:[.,]\d+)?)\s*%")
    _degrees = re.compile(r"(?P<value>-?\d+(?:[.,]\d+)?)\s*(?:°|deg|град)", re.IGNORECASE)

    async def plan(self, prompt: str, scene: dict[str, Any]) -> GeometryPlan:
        normalized = prompt.casefold()
        dimensions = self._dimensions.search(prompt)
        if dimensions is not None and re.search(
            r"box|короб|объ[её]м|блок|параллелепипед|башн", normalized
        ):
            return self._create_box(dimensions, scene)

        targets = _resolve_targets(prompt, scene)
        if not targets:
            raise PlanningError(
                "Выделите объект в Rhino и повторите команду. Локальный режим также понимает "
                "создание блока, например: «Создай блок 30 × 20 × 80 м»."
            )

        center = _target_center(targets)
        ids = [str(item["id"]) for item in targets]

        if re.search(r"перемест|сдвин|move", normalized):
            distance = self._distance(prompt, scene)
            axis_index = _axis_from_prompt(normalized)
            vector = [0.0, 0.0, 0.0]
            negative = re.search(r"вниз|назад|влево|negative|minus", normalized)
            vector[axis_index] = -distance if negative else distance
            return _transform_plan(
                "Перемещаю выбранную геометрию.", ids, "move", vector=vector
            )

        if re.search(r"поверн|разверн|rotate", normalized):
            degrees = self._degrees.search(prompt)
            if degrees is None:
                raise PlanningError("Укажите угол, например: «Поверни выбранное на 15°».")
            angle = float(degrees.group("value").replace(",", "."))
            axis = [0.0, 0.0, 1.0]
            if re.search(r"вокруг\s*x|по\s*x", normalized):
                axis = [1.0, 0.0, 0.0]
            elif re.search(r"вокруг\s*y|по\s*y", normalized):
                axis = [0.0, 1.0, 0.0]
            return _transform_plan(
                "Поворачиваю выбранную геометрию.",
                ids,
                "rotate",
                center=center,
                axis=axis,
                angle_degrees=angle,
            )

        if re.search(r"увелич|уменьш|масштаб|шире|уже|выше|ниже|scale", normalized):
            percent = self._percent.search(prompt)
            if percent is None:
                raise PlanningError("Укажите изменение, например: «Сделай на 20% выше».")
            delta = float(percent.group("value").replace(",", ".")) / 100
            shrinking = bool(re.search(r"уменьш|уже|ниже|меньше|shrink", normalized))
            factor = 1 - delta if shrinking else 1 + delta
            if factor <= 0:
                raise PlanningError("Масштаб должен оставаться больше нуля.")
            factors = [factor, factor, factor]
            if re.search(r"выше|ниже|высот|по\s*z", normalized):
                factors = [1.0, 1.0, factor]
            elif re.search(r"шире|уже|тоньше|по\s*x", normalized):
                factors = [factor, factor, 1.0]
            return _transform_plan(
                "Изменяю размеры выбранной геометрии.",
                ids,
                "scale_xyz",
                center=center,
                factors=factors,
            )

        raise PlanningError(
            "Локальный режим понимает создание блока, перемещение, поворот и изменение "
            "размера выбранного объекта. Для свободных запросов подключите AI planner."
        )

    def _create_box(self, match: re.Match[str], scene: dict[str, Any]) -> GeometryPlan:
        values = [float(match.group(key).replace(",", ".")) for key in ("a", "b", "c")]
        factor = _unit_factor(match.group("unit") or "m", str(scene.get("units", "")))
        dimensions = [value * factor for value in values]
        return GeometryPlan(
            summary=f"Создаю блок {values[0]:g} × {values[1]:g} × {values[2]:g}.",
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

    def _distance(self, prompt: str, scene: dict[str, Any]) -> float:
        match = self._number_with_unit.search(prompt)
        if match is None:
            raise PlanningError("Укажите расстояние с единицей измерения, например 3 м.")
        value = float(match.group("value").replace(",", "."))
        return value * _unit_factor(match.group("unit"), str(scene.get("units", "")))


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


def _resolve_targets(prompt: str, scene: dict[str, Any]) -> list[dict[str, Any]]:
    objects = [item for item in scene.get("objects", []) if isinstance(item, dict)]
    selected = [item for item in objects if item.get("selected") is True]
    if selected:
        return selected
    lowered = prompt.casefold()
    named = [
        item
        for item in objects
        if item.get("name") and str(item["name"]).casefold() in lowered
    ]
    if named:
        return named
    return objects if len(objects) == 1 else []


def _target_center(targets: list[dict[str, Any]]) -> list[float]:
    minima = [item.get("bbox", {}).get("min") for item in targets]
    maxima = [item.get("bbox", {}).get("max") for item in targets]
    if not all(isinstance(value, list) and len(value) == 3 for value in minima + maxima):
        raise PlanningError("У выбранной геометрии нет корректного bounding box.")
    return [
        (
            min(float(value[index]) for value in minima)
            + max(float(value[index]) for value in maxima)
        )
        / 2
        for index in range(3)
    ]


def _axis_from_prompt(prompt: str) -> int:
    if re.search(r"вверх|вниз|по\s*z|ось\s*z", prompt):
        return 2
    if re.search(r"впер[её]д|назад|по\s*y|ось\s*y", prompt):
        return 1
    return 0


def _transform_plan(summary: str, ids: list[str], kind: str, **arguments: Any) -> GeometryPlan:
    return GeometryPlan(
        summary=summary,
        operations=[
            PlannedOperation(
                operation_id=f"{kind}-selected-1",
                tool="transform",
                arguments={"object_ids": ids, "kind": kind, "copy": False, **arguments},
            )
        ],
    )


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
