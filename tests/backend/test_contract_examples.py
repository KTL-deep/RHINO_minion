import json
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

ROOT = Path(__file__).resolve().parents[2]


def test_request_examples_match_envelope_schema() -> None:
    schema = json.loads(
        (ROOT / "schemas/protocol/bridge-request.schema.json").read_text(encoding="utf-8")
    )
    validator = Draft202012Validator(schema, format_checker=FormatChecker())

    for fixture in sorted((ROOT / "examples/requests").glob("*.json")):
        payload = json.loads(fixture.read_text(encoding="utf-8"))
        errors = list(validator.iter_errors(payload))
        assert not errors, f"{fixture.name}: {errors}"
