import asyncio

import pytest
from rhino_minion.planning.provider import DeterministicPlanner, PlanningError


def test_deterministic_planner_converts_meters_to_millimeters() -> None:
    plan = asyncio.run(
        DeterministicPlanner().plan(
            "Create a box 30 × 20 × 80 m",
            {"units": "Millimeters", "objects": []},
        )
    )

    assert plan.operations[0].tool == "create_box"
    assert plan.operations[0].arguments["width"] == 30_000
    assert plan.operations[0].arguments["height"] == 80_000


def test_deterministic_planner_rejects_unsupported_prompt() -> None:
    with pytest.raises(PlanningError):
        asyncio.run(DeterministicPlanner().plan("Make a twisted tower", {"units": "Meters"}))


def test_deterministic_planner_understands_russian_box() -> None:
    plan = asyncio.run(
        DeterministicPlanner().plan(
            "Создай блок 30 × 20 × 80 м",
            {"units": "Millimeters", "objects": []},
        )
    )

    assert plan.operations[0].tool == "create_box"
    assert plan.operations[0].arguments["height"] == 80_000


def test_deterministic_planner_scales_selected_object_height() -> None:
    scene = {
        "units": "Millimeters",
        "objects": [
            {
                "id": "d07615db-883a-4e8c-a9d8-fbb08ad00f1b",
                "name": "Tower",
                "selected": True,
                "bbox": {"min": [0, 0, 0], "max": [10_000, 8_000, 30_000]},
            }
        ],
    }
    plan = asyncio.run(
        DeterministicPlanner().plan("Сделай выбранную башню на 20% выше", scene)
    )

    operation = plan.operations[0]
    assert operation.tool == "transform"
    assert operation.arguments["kind"] == "scale_xyz"
    assert operation.arguments["factors"] == [1.0, 1.0, 1.2]
    assert operation.arguments["center"] == [5_000, 4_000, 15_000]


def test_deterministic_planner_moves_selected_object_up() -> None:
    scene = {
        "units": "Millimeters",
        "objects": [
            {
                "id": "d07615db-883a-4e8c-a9d8-fbb08ad00f1b",
                "selected": True,
                "bbox": {"min": [0, 0, 0], "max": [1, 1, 1]},
            }
        ],
    }
    plan = asyncio.run(DeterministicPlanner().plan("Перемести выбранное вверх на 3 м", scene))

    assert plan.operations[0].arguments["vector"] == [0.0, 0.0, 3_000]
