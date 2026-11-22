"""Build shop colourways on the original head UVs. Run from the repository root.

blender -b -P tools/bake-headwear.py -- DIRECTORY
python tools/bake-headwear.py DIRECTORY   (Pillow and NumPy)
"""
from pathlib import Path
import sys
import numpy as np

root = Path("unity/OwlSlope/Assets/Resources")
work = Path(sys.argv[-1])
kits = [("OwlMenu", "OwlMenuRace0"), ("OwlRide", "OwlRace4"),
        ("BearMenu", "BearMenuRace0"), ("Bear", "BearRace4")]

def extract(model):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str((root / (model + ".fbx")).resolve()))
    body = next(o for o in bpy.context.scene.objects if o.type == "MESH" and len(o.data.vertices) > 9000)
    mesh = body.data
    head = next(g.index for g in body.vertex_groups if g.name.lower().endswith("head"))
    owl = model.startswith("Owl")
    points = [body.matrix_world @ v.co for v in mesh.vertices]
    head_weight = [sum(g.weight for g in v.groups if g.group == head) for v in mesh.vertices]
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active.data
    polys = []
    for tri in mesh.loop_triangles:
        ids = tri.vertices
        if max(head_weight[i] for i in ids) < .48:
            continue
        z = sum(points[i].z for i in ids) / 3
        x = abs(sum(points[i].x for i in ids) / 3)
        # The lens rim rises over the beak. Keep the owl's blue eyebrow feathers outside the finish.
        rim = .813 + .047 * max(0, 1 - x / .091)
        if z < (rim if owl else .835):
            continue
        polys.append([tuple(uv[i].uv) for i in tri.loops])
    np.save(work / (model + ".npy"), polys)

def bake(model, texture):
    from PIL import Image, ImageDraw, ImageFilter
    folder = root / "Art"
    source = Image.open(folder / (texture + ".jpg")).convert("RGB")
    width, height = source.size
    coverage = Image.new("L", source.size)
    draw = ImageDraw.Draw(coverage)
    for poly in np.load(work / (model + ".npy")):
        draw.polygon([(u * width, (1-v) * height) for u, v in poly], fill=255)
    mask = np.asarray(coverage.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(.65))) / 255.
    rgb = np.asarray(source, dtype=float) / 255.
    r, g, b = rgb.transpose(2, 0, 1)
    owl = model.startswith("Owl")
    if owl:
        # Select cyan/blue lens and strap, retain feathers, eyes and specular highlights.
        weight = np.clip((b-r-.035)*12, 0, 1) * np.clip((b-g+.12)*10, 0, 1)
        tint = np.stack([np.minimum(1,b*1.01+.08), .36*g+.065, .67*b+.14], axis=-1)
    else:
        # Preserve the dark printed M and shell shading, including the vents.
        weight = np.clip((r-.33)*7, 0, 1) * np.clip((.24-(rgb.max(2)-rgb.min(2)))*9, 0, 1)
        tint = np.stack([np.minimum(1,r*1.09+.085), g*.43, b*.095], axis=-1)
    alpha = (mask * weight)[:, :, None]
    out = np.clip((rgb + (tint-rgb)*alpha)*255, 0, 255).astype("uint8")
    if np.count_nonzero(alpha > .35) < .001 * width * height:
        raise ValueError(f"No visible headwear on {model}")
    Image.fromarray(out).save(folder / (texture + "Shop.jpg"), quality=93, subsampling=0)

if "--" in sys.argv:
    for model, _ in kits:
        extract(model)
else:
    for model, texture in kits:
        bake(model, texture)
