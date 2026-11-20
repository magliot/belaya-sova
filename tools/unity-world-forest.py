"""Convert the original WebP course cards to textures supported by the Unity importer."""
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[1]
source = repo / "docs/art/slope"
target = repo / "unity/OwlSlope/Assets/Resources/Race/Forest"
target.mkdir(parents=True, exist_ok=True)

def convert(name, destination):
    with Image.open(source / (name + ".webp")) as image:
        image.convert("RGBA").save(target / (destination + ".png"), optimize=True)

for family in range(3):
    for variant, letter in enumerate("abc"):
        convert(f"forest-{19+family}{letter}", f"forestCard{family}{variant}")
convert("yeti-print-56c", "yeti-print-56c")
