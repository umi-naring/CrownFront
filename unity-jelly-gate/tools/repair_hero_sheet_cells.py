"""Repair generated attack cells that contain an effect but no actor.

The image generator occasionally emits the release projectile in column six
without the character. Unity's runtime slicer intentionally keeps only the
component nearest each cell centre, so those cells would animate as a floating
effect. This pass places the complete wind-up actor beneath that effect while
preserving the generated release art.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


def cell_bounds(size: int, index: int, count: int) -> tuple[int, int]:
    return round(index * size / count), round((index + 1) * size / count)


def add_release_effect(base: Image.Image, kind: str) -> Image.Image:
    canvas = base.copy()
    draw = ImageDraw.Draw(canvas, "RGBA")
    width, height = canvas.size
    if kind == "frost":
        origin = (int(width * .58), int(height * .50))
        tip = (int(width * .94), int(height * .50))
        draw.line((origin, tip), fill=(96, 226, 255, 235), width=max(3, width // 45))
        draw.polygon(
            [
                (int(width * .72), int(height * .45)),
                tip,
                (int(width * .72), int(height * .55)),
                (int(width * .78), int(height * .50)),
            ],
            fill=(175, 245, 255, 220),
        )
        draw.line(
            ((int(width * .65), int(height * .46)), (int(width * .9), int(height * .46))),
            fill=(215, 255, 255, 170),
            width=max(1, width // 90),
        )
    else:
        centre = (int(width * .58), int(height * .72))
        radius = max(8, width // 12)
        for offset, colour in (
            (0, (255, 255, 255, 235)),
            (radius // 3, (82, 215, 255, 225)),
            (radius // 2, (255, 206, 54, 210)),
        ):
            r = max(2, radius - offset)
            draw.ellipse(
                (centre[0] - r, centre[1] - r, centre[0] + r, centre[1] + r),
                outline=colour,
                width=max(2, width // 70),
            )
    return canvas


def repair(path: Path, rows: list[int], effect_kind: str) -> None:
    sheet = Image.open(path).convert("RGBA")
    for row in rows:
        x0, x1 = cell_bounds(sheet.width, 4, 7)
        bad_x0, bad_x1 = cell_bounds(sheet.width, 5, 7)
        y0, y1 = cell_bounds(sheet.height, row, 3)
        donor = sheet.crop((x0, y0, x1, y1))
        destination = sheet.crop((bad_x0, y0, bad_x1, y1))

        # All generated cells differ by at most one pixel because of rounded
        # atlas boundaries. Normalise the donor to the destination cell.
        donor = donor.resize(destination.size, Image.Resampling.LANCZOS)
        repaired = add_release_effect(donor, effect_kind)
        # Paste without an alpha mask so the old, malformed cell is completely
        # cleared instead of remaining visible behind transparent pixels.
        sheet.paste(repaired, (bad_x0, y0))
    sheet.save(path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tank", type=Path, required=True)
    parser.add_argument("--single-mage", type=Path, required=True)
    args = parser.parse_args()

    # Tank front release is an effect-only shield impact. The rear release
    # contains two overlapping actors, so use a clean full-body attack pose.
    repair(args.tank, [0, 2], effect_kind="shield")
    # All three frost release cells contain only the focused ice projectile.
    repair(args.single_mage, [0, 1, 2], effect_kind="frost")


if __name__ == "__main__":
    main()
