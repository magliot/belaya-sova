"""Bake the two helmets against the rest-space geometry of the shipped FBXs.

blender --factory-startup -b -P tools/bake-headwear.py -- DIRECTORY
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
    head_weight = [sum(g.weight for g in v.groups if g.group == head) for v in mesh.vertices]
    points = np.array([tuple(body.matrix_world @ v.co) for v in mesh.vertices])
    mesh.calc_loop_triangles()
    faces = [t for t in mesh.loop_triangles if max(head_weight[i] for i in t.vertices) > .20]
    uv = mesh.uv_layers.active.data
    np.savez(work / (model + "-helmet.npz"),
             corners=points[[list(t.vertices) for t in faces]],
             uvs=np.array([[tuple(uv[i].uv) for i in t.loops] for t in faces]))


def shell(points, owl):
    x, y, z = points.T
    if not owl:
        # The hard hat slopes down towards the back. The brim sits above the eyes and below the ears.
        return z > .778 - .25 * y

    # Cross-sections taken on the black goggle frame and feather line of the original FBX.
    # Front is -Y. Project along the visor tilt so the cut follows its curved outer surface.
    side = np.abs(x)
    height = z + .070 * (y + .045)
    visor_x = [0, .024, .051, .081, .105, .121, .131, .141, .151]
    visor_top = np.interp(side, visor_x, [.947, .947, .942, .935, .923, .913, .905, .891, .861])
    visor_bottom = np.interp(side, visor_x, [.856, .846, .821, .807, .805, .804, .805, .813, .844])
    visor = (y < -.118) & (side < .151) & (height >= visor_bottom) & (height <= visor_top)
    face_x = [0, .025, .05, .081, .106, .126, .138, .152, .163, .171, .182]
    brow = np.interp(side, face_x, [.770, .802, .802, .793, .789, .786, .774, .762, .747, .726, .692])
    plumage = (y < -.094) & (height < brow)
    return (z > .686) & ~visor & ~plumage


def surface_mask(model, size):
    from PIL import Image, ImageFilter
    width, height = size
    geo = np.load(work / (model + "-helmet.npz"))
    mask = np.zeros((height, width), dtype=np.uint8)
    for corners, uv in zip(geo["corners"], geo["uvs"]):
        tri = uv * [width, -height] + [0, height]
        x0, y0 = np.maximum(np.floor(tri.min(0)).astype(int), 0)
        x1, y1 = np.minimum(np.ceil(tri.max(0)).astype(int), [width-1, height-1])
        if x1 < x0 or y1 < y0:
            continue
        ab, ac = tri[1:] - tri[0]
        det = ab[0]*ac[1] - ab[1]*ac[0]
        if abs(det) < 1e-7:
            continue
        yy, xx = np.ogrid[y0:y1+1, x0:x1+1]
        dx, dy = xx+.5-tri[0,0], yy+.5-tri[0,1]
        b, c = (dx*ac[1]-dy*ac[0])/det, (ab[0]*dy-ab[1]*dx)/det
        inside = (b >= 0) & (c >= 0) & (b+c <= 1)
        if inside.any():
            weights = np.stack([1-b[inside]-c[inside], b[inside], c[inside]], axis=1)
            region = mask[y0:y1+1, x0:x1+1]
            region[inside] = 255 * shell(weights @ corners, model.startswith("Owl"))
    # A small gutter keeps UV seams covered when Unity builds its mipmaps.
    return np.asarray(Image.fromarray(mask).filter(ImageFilter.MaxFilter(5))) / 255.


def bake(model, texture):
    from PIL import Image
    folder = root / "Art"
    source = Image.open(folder / (texture + ".jpg")).convert("RGB")
    rgb = np.asarray(source, dtype=np.float32) / 255.
    r, g, b = rgb.transpose(2, 0, 1)
    coverage = surface_mask(model, source.size)
    if model.startswith("Owl"):
        # Leave the black strap, white mountain emblem, feathers and snow flecks in their original finish.
        chroma = b - np.maximum(r, g)
        weight = np.clip((chroma-.056)*8, 0, 1)
        tint = np.stack([np.minimum(1, b*1.03+.035), .40*g+.11, .60*b+.12], axis=-1)
    else:
        # The ears are warm brown and the lettering is printed black on the neutral shell.
        weight = np.clip((.26-(rgb.max(2)-rgb.min(2)))*10, 0, 1) * np.clip((rgb.max(2)-.17)*8, 0, 1)
        light = .26*r+.57*g+.17*b
        tint = np.stack([np.minimum(1, light*1.08+.06), .445*light, .075*light], axis=-1)
    alpha = (coverage * weight)[:, :, None]
    if np.count_nonzero(alpha > .5) < .001 * source.width * source.height:
        raise ValueError(f"No visible helmet on {model}")
    out = np.rint(np.clip(rgb + (tint-rgb)*alpha, 0, 1)*255).astype("uint8")
    Image.fromarray(out).save(folder / (texture + "Shop.jpg"), quality=95, subsampling=0)


if "--" in sys.argv:
    for model, _ in kits:
        extract(model)
else:
    for model, texture in kits:
        bake(model, texture)
