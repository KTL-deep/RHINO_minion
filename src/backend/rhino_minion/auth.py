from dataclasses import dataclass
from typing import Any

import jwt
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import PyJWKClient

from rhino_minion.config import settings

bearer = HTTPBearer(auto_error=False)


@dataclass(frozen=True)
class CurrentUser:
    id: str
    email: str | None
    claims: dict[str, Any]


def require_user(
    credentials: HTTPAuthorizationCredentials | None = Depends(bearer),
) -> CurrentUser:
    if not settings.auth_enabled:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Authentication must be enabled for billing",
        )
    return _authenticate(credentials)


def generation_user(
    credentials: HTTPAuthorizationCredentials | None = Depends(bearer),
) -> CurrentUser:
    if not settings.auth_enabled:
        return CurrentUser(id="local-development", email=None, claims={})
    return _authenticate(credentials)


def _authenticate(credentials: HTTPAuthorizationCredentials | None) -> CurrentUser:
    if credentials is None or credentials.scheme.lower() != "bearer":
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Bearer token required",
        )

    try:
        jwks_client = PyJWKClient(
            f"{settings.oidc_issuer.rstrip('/')}/protocol/openid-connect/certs"
        )
        signing_key = jwks_client.get_signing_key_from_jwt(credentials.credentials)
        claims = jwt.decode(
            credentials.credentials,
            signing_key.key,
            algorithms=["RS256"],
            issuer=settings.oidc_issuer,
            options={"verify_aud": False},
        )
    except jwt.PyJWTError as exception:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid access token",
        ) from exception

    audience = claims.get("aud", [])
    if isinstance(audience, str):
        audience = [audience]
    valid_client = (
        claims.get("azp") == settings.oidc_client_id
        or settings.oidc_client_id in audience
    )
    if not valid_client:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Wrong token client")
    subject = claims.get("sub")
    if not isinstance(subject, str) or not subject:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Token has no subject")
    return CurrentUser(id=subject, email=claims.get("email"), claims=claims)
