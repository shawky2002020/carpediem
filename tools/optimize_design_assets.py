"""Create non-runtime Carpediem image candidates from reviewed Drive originals.

Only people-free images are listed. This script does not publish assets to an app.
The caller supplies locally downloaded originals; see docs/ux/ASSET_REGISTRY.md.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps


ASSETS = (
    # public name, local source name, source Drive ID, focal point, crops
    (
        "mango-hut-01",
        "huts.png",
        "1OtqMhZ0UQKAABZ-afPzwAIswj3wSz5yq",
        (0.50, 0.52),
        (("portrait", 4 / 5, (480, 768)),),
    ),
    (
        "mango-hut-01",
        "hut-1.jpg",
        "12638wgHErxPtnY42zvoZ3kMqEWqBNU2w",
        (0.49, 0.54),
        (("landscape", 16 / 9, (1200, 1600)),),
    ),
    (
        "flower-hut-02",
        "hut-2.jpg",
        "1NEKqckjwRiSbpX6ByiZpMlu8xn5GOsLP",
        (0.52, 0.53),
        (("landscape", 16 / 9, (1200,)),),
    ),
    (
        "mango-grove-01",
        "img-4016.png",
        "1he9tQ2Sk_b86jpqq4y2I2sO8p5euiAHy",
        (0.51, 0.48),
        (("portrait", 4 / 5, (480, 768)),),
    ),
    (
        "hut-interior-01",
        "hut-inside-1.jpg",
        "19QjxaMstU7HqkCdYVyh6TreSAq9wTXId",
        (0.52, 0.50),
        (("landscape", 16 / 9, (1200,)),),
    ),
    (
        "community-hall-01",
        "p1655568.jpg",
        "1LIISk7ZhptgcaBipbTamPe-_ZrxZPFNY",
        (0.50, 0.53),
        (("landscape", 16 / 9, (1200,)),),
    ),
)


def crop_at_focal(image: Image.Image, aspect: float, focal: tuple[float, float]) -> Image.Image:
    width, height = image.size
    if width / height > aspect:
        crop_width, crop_height = round(height * aspect), height
    else:
        crop_width, crop_height = width, round(width / aspect)
    left = max(0, min(width - crop_width, round(width * focal[0] - crop_width / 2)))
    top = max(0, min(height - crop_height, round(height * focal[1] - crop_height / 2)))
    return image.crop((left, top, left + crop_width, top + crop_height))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=Path("design-assets/review"))
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    manifest = []

    for name, source_name, drive_id, focal, crops in ASSETS:
        source = args.source_dir / source_name
        if not source.is_file():
            raise FileNotFoundError(f"Missing source: {source}")
        with Image.open(source) as original:
            image = ImageOps.exif_transpose(original).convert("RGB")
            for crop_name, aspect, widths in crops:
                cropped = crop_at_focal(image, aspect, focal)
                for width in widths:
                    resized = cropped.resize((width, round(width / aspect)), Image.Resampling.LANCZOS)
                    for extension, options in (
                        ("avif", {"quality": 55, "speed": 6}),
                        ("webp", {"quality": 78, "method": 6}),
                        ("jpg", {"quality": 82, "optimize": True, "progressive": True}),
                    ):
                        dest = args.output_dir / f"{name}-{crop_name}-{width}.{extension}"
                        resized.save(dest, **options)
                        manifest.append(
                            {
                                "file": dest.name,
                                "drive_id": drive_id,
                                "source": source_name,
                                "width": resized.width,
                                "height": resized.height,
                                "bytes": dest.stat().st_size,
                                "sha256": hashlib.sha256(dest.read_bytes()).hexdigest(),
                                "rights": "RIGHTS UNKNOWN",
                                "delivery": "review only; not runtime",
                            }
                        )

    (args.output_dir / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Wrote {len(manifest)} candidates to {args.output_dir}")
    print(f"Largest output: {max(item['bytes'] for item in manifest):,} bytes")


if __name__ == "__main__":
    main()
