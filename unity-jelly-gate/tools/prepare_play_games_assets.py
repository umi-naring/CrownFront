"""Prepare existing Google Play store art for Play Games Services."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--icon", required=True, type=Path)
    parser.add_argument("--feature", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)

    with Image.open(args.icon) as icon:
        if icon.size != (512, 512):
            raise ValueError(f"Expected 512x512 icon, got {icon.size}")
        icon.convert("RGBA").save(args.output / "crownfront-play-games-icon.png", optimize=True)

    with Image.open(args.feature) as feature:
        if feature.size not in {(512, 250), (1024, 500)}:
            raise ValueError(f"Expected 2.048:1 feature art, got {feature.size}")
        resized = feature.convert("RGB").resize((1024, 500), Image.Resampling.LANCZOS)
        resized.save(args.output / "crownfront-play-games-feature.png", optimize=True)

    print(args.output / "crownfront-play-games-icon.png")
    print(args.output / "crownfront-play-games-feature.png")


if __name__ == "__main__":
    main()
