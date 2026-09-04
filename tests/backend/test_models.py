import pytest
from pydantic import ValidationError

from rhino_minion.models import BridgeResponse


def test_failed_response_requires_error() -> None:
    with pytest.raises(ValidationError):
        BridgeResponse(
            request_id="11111111-1111-1111-1111-111111111111",
            ok=False,
        )
