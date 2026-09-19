"""Downsize imported model textures for a stable mobile AR frame rate."""

from pathlib import Path

from PIL import Image


texture_dir = Path("Assets/KrakensEmbrace/Art/Model")
files = list(texture_dir.glob("texture_*.*"))
before = sum(path.stat().st_size for path in files)

for path in files:
    with Image.open(path) as image:
        image.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
        image.save(path, optimize=True)

after = sum(path.stat().st_size for path in files)
print(f"Optimized {len(files)} textures: {before / 1048576:.1f} MB -> {after / 1048576:.1f} MB")
