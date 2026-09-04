from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="RHINO_MINION_", env_file=".env")

    host: str = "127.0.0.1"
    port: int = Field(default=8766, ge=1024, le=65535)
    log_level: str = "INFO"
    bridge_secret: str = ""
    planner: str = "deterministic"
    openai_api_key: str = ""
    openai_model: str = ""
    max_plan_operations: int = Field(default=20, ge=1, le=50)
    auth_enabled: bool = False
    oidc_issuer: str = "http://127.0.0.1:8080/realms/rhino-minion"
    oidc_client_id: str = "rhino-minion-desktop"
    yookassa_shop_id: str = ""
    yookassa_secret_key: str = ""
    yookassa_return_url: str = "http://127.0.0.1:5173/?payment=return"
    billing_database: str = "data/billing.db"
    credits_100_price_rub: int = Field(default=0, ge=0)


settings = Settings()
