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
