"""
How a page image is recognised. A request carries only ids and image keys, so the image is the only way to know a page.

- The hash is a 64-bit difference hash (dHash): the page in grey, shrunk to 9 by 8 pixels; each bit says whether a pixel
  is brighter than its right-hand neighbour, row by row from the top left. It survives re-rendering and re-encoding,
  and it hardly changes when the page is blurred.
- So a blurred capture of a page has nearly the hash of the sharp one. The sharpness tells them apart: the root mean
  square of the differences between neighbouring pixels (0 to 255) of the page in grey, scaled to 600 pixels wide.
  Squaring matters: blur spreads an edge over more pixels, which leaves the plain sum of the differences as it was.

A recording matches an image within HASH_TOLERANCE bits and within a factor of SHARPNESS_TOLERANCE in sharpness.
"""

import io
from dataclasses import dataclass

from PIL import Image, ImageChops, ImageStat, UnidentifiedImageError

HASH_TOLERANCE = 6
"""At most this many of the 64 bits may differ."""

SHARPNESS_TOLERANCE = 1.5
"""The sharper of the two may be at most this many times as sharp."""

SHARPNESS_WIDTH = 600


class NotAnImage(Exception):
    """The bytes could not be decoded as an image."""


@dataclass(frozen=True)
class Fingerprint:
    page_hash: int
    sharpness: float

    def matches(self, other: "Fingerprint") -> bool:
        return (
            hamming(self.page_hash, other.page_hash) <= HASH_TOLERANCE
            and sharpness_ratio(self.sharpness, other.sharpness) <= SHARPNESS_TOLERANCE
        )


def hamming(one: int, other: int) -> int:
    return (one ^ other).bit_count()


def sharpness_ratio(one: float, other: float) -> float:
    low, high = sorted((one, other))
    if high == 0:
        return 1.0
    return high / low if low > 0 else float("inf")


def fingerprint(image: bytes) -> Fingerprint:
    try:
        with Image.open(io.BytesIO(image)) as opened:
            grey = opened.convert("L")
    except (UnidentifiedImageError, OSError) as error:
        raise NotAnImage from error
    return Fingerprint(page_hash=_dhash(grey), sharpness=_sharpness(grey))


def _dhash(grey: Image.Image) -> int:
    small = grey.resize((9, 8), Image.Resampling.LANCZOS).tobytes()
    bits = 0
    for row in range(8):
        for column in range(8):
            left, right = small[row * 9 + column], small[row * 9 + column + 1]
            bits = (bits << 1) | (left > right)
    return bits


def _sharpness(grey: Image.Image) -> float:
    height = max(2, round(grey.height * SHARPNESS_WIDTH / grey.width))
    scaled = grey.resize((SHARPNESS_WIDTH, height), Image.Resampling.LANCZOS)
    width, height = scaled.size
    across = ImageChops.difference(
        scaled.crop((1, 0, width, height)), scaled.crop((0, 0, width - 1, height))
    )
    down = ImageChops.difference(
        scaled.crop((0, 1, width, height)), scaled.crop((0, 0, width, height - 1))
    )
    return (ImageStat.Stat(across).rms[0] + ImageStat.Stat(down).rms[0]) / 2
