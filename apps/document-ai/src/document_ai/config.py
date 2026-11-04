from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Read from DOCUMENT_AI_* environment variables. Secrets never have a default."""

    model_config = SettingsConfigDict(env_prefix="DOCUMENT_AI_", env_file=".env", extra="ignore")

    service_key: str
    gateway: str = "fake"
    storage_endpoint: str = "http://localhost:8333"
    storage_bucket: str = "claimlens"
    storage_access_key: str = ""
    storage_secret_key: str = ""
