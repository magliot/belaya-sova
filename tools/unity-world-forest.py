"""Prepare Unity forest cards and continuous windswept crowns from the approved tree meshes."""
import math
import struct
import base64
import json
import re
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


def bend_tree(source_mesh, target_mesh, height, lean):
    # Bend each trunk, branch, snowy cap and needle with the same curve. The old C
    # variant placed its top cone on a different axis from its last rings of foliage.
    data = bytearray(source_mesh.read_bytes())
    cursor = 0

    def integer():
        nonlocal cursor
        value, = struct.unpack_from("<i", data, cursor)
        cursor += 4
        return value

    for _ in range(integer()):
        name_length = integer()
        cursor += name_length + 12  # material name, blend, lit, emission
        count = integer()
        normals = cursor + count * 12
        for i in range(count):
            vertex = cursor + i * 12
            x, y, z = struct.unpack_from("<3f", data, vertex)
            t = max(y, 0) / height
            struct.pack_into("<3f", data, vertex, x + lean * height * t**1.7, y, z)
            normal = normals + i * 3
            nx, ny, nz = struct.unpack_from("<3b", data, normal)
            ny -= nx * lean * 1.7 * t**.7
            length = math.sqrt(nx*nx + ny*ny + nz*nz)
            if length:
                struct.pack_into("<3b", data, normal, *(round(v * 127 / length) for v in (nx, ny, nz)))
        cursor += count * 27
        indices = integer()
        cursor += indices * 4
        if integer():
            raise ValueError(f"Unexpected morphs in fixed forest mesh {source_mesh}")
    if cursor != len(data):
        raise ValueError(f"Invalid course model {source_mesh}")
    target_mesh.write_bytes(data)


models = target.parent / "Models"
for family, reference, height, lean in [(0, 0, 3.7, .26), (1, 1, 7.2, .24), (2, 0, 2.45, .25)]:
    bend_tree(models / f"tree{family}{reference}.bytes", models / f"tree{family}2.bytes", height, lean)


def canopy_tree(species, height, width, bend):
    """Keep the original textured tree, with one continuous curve from root to crown."""
    library = (repo / "docs/models/trees.js").read_text(encoding="utf8")
    packed = re.search(rf'__GLB(?:\.|\["){species}(?:"\])?\s*=\s*"([A-Za-z0-9+/=]+)"', library)
    glb = base64.b64decode(packed.group(1))
    json_length, = struct.unpack_from("<I", glb, 12)
    gltf = json.loads(glb[20:20+json_length])
    binary = memoryview(glb)[28+json_length:]
    mesh = gltf["meshes"][0]["primitives"][0]

    def read_attribute(index, fmt):
        accessor = gltf["accessors"][index]
        view = gltf["bufferViews"][accessor["bufferView"]]
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", struct.calcsize(fmt))
        return [struct.unpack_from(fmt, binary, start+i*stride) for i in range(accessor["count"])]

    attrs = mesh["attributes"]
    vertices = read_attribute(attrs["POSITION"], "<3f")
    normals = read_attribute(attrs["NORMAL"], "<3f")
    uvs = read_attribute(attrs["TEXCOORD_0"], "<2f")
    indices = [v for v, in read_attribute(mesh["indices"], "<H")]
    low, high = min(p[1] for p in vertices), max(p[1] for p in vertices)
    root = [p for p in vertices if p[1] < low + .045*(high-low)]
    cx = (min(p[0] for p in root)+max(p[0] for p in root))*.5
    cz = (min(p[2] for p in root)+max(p[2] for p in root))*.5
    factor = height/(high-low)
    pos, norm, uv = bytearray(), bytearray(), bytearray()
    for (x,y,z), (nx,ny,nz), (u,v) in zip(vertices, normals, uvs):
        t = (y-low)/(high-low)
        x = (x-cx)*factor*width + height*(.045*t+bend*t*t)
        pos.extend(struct.pack("<3f", x, t*height, -(z-cz)*factor*width))
        nx, nz = nx/width, -nz/width
        ny -= nx*(.045+2*bend*t)
        length = math.sqrt(nx*nx+ny*ny+nz*nz) or 1
        norm.extend(struct.pack("<3b", *(round(v*127/length) for v in (nx,ny,nz))))
        uv.extend(struct.pack("<2f", u, 1-v))
    for i in range(0,len(indices),3):
        indices[i+1], indices[i+2] = indices[i+2], indices[i+1]
    material = f"Art/{species.title()}".encode()
    header = struct.pack("<2i", 1,len(material)) + material + struct.pack("<4i", 0,1,0,len(vertices))
    faces = struct.pack("<i",len(indices)) + struct.pack(f"<{len(indices)}i",*indices)
    (models/f"canopy{species.title()}.bytes").write_bytes(header+pos+norm+uv+b"\xff\xff\xff\xff"*len(vertices)+faces+struct.pack("<i",0))
    if species == "pine":
        view = gltf["bufferViews"][gltf["images"][0]["bufferView"]]
        start = view.get("byteOffset", 0)
        (target.parent.parent/"Art/Pine.jpg").write_bytes(binary[start:start+view["byteLength"]])


canopy_tree("fir", 7.15, .72, .37)
canopy_tree("pine", 8.70, .78, .32)
