"""Page Reader. Loads page images by key with read-only credentials limited to pages/."""

from typing import Protocol

import boto3
from botocore.config import Config
from botocore.exceptions import BotoCoreError, ClientError

from document_ai.config import Settings


class PageReadError(Exception):
    """This page could not be read. Only this page falls back to manual entry."""


class PageReader(Protocol):
    def read(self, image_key: str) -> bytes: ...


class S3PageReader:
    def __init__(self, settings: Settings) -> None:
        self._bucket = settings.storage_bucket
        self._client = boto3.client(
            "s3",
            endpoint_url=settings.storage_endpoint,
            aws_access_key_id=settings.storage_access_key,
            aws_secret_access_key=settings.storage_secret_key,
            region_name="us-east-1",
            config=Config(s3={"addressing_style": "path"}, signature_version="s3v4"),
        )

    def read(self, image_key: str) -> bytes:
        try:
            response = self._client.get_object(Bucket=self._bucket, Key=image_key)
            return response["Body"].read()
        except (BotoCoreError, ClientError) as error:
            raise PageReadError(image_key) from error
