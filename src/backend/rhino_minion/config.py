from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="RHINO_MINION_", env_file=".env")

    host: str = "127.0.0.1"
    port: int = Field(default=8766, ge=1024, le=65535)
    log_level: str = "INFO"
    bridge_secret: str = ""


settings = Settings()
