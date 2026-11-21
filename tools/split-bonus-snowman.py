"""Bake three capped pieces from the game's textured snowman, including its hat, face and arms.

Run from the repository root with Blender -b --python tools/split-bonus-snowman.py.
The export keeps Unity's original vertex positions, UV seams, normals and course mesh format.
"""
from collections import defaultdict
from pathlib import Path
from struct import calcsize, pack, unpack
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

models = Path("unity/OwlSlope/Assets/Resources/Race/Models")


def load_skin():
    with (models / "snowman.bytes").open("rb") as stream:
        def read(fmt):
            return unpack("<" + fmt, stream.read(calcsize("<" + fmt)))
        assert read("i") == (1,)
        texture = stream.read(read("i")[0])
        flags = read("3i")
        count, = read("i")
        positions = [read("3f") for _ in range(count)]
        normals = [read("3b") for _ in range(count)]
        uvs = [read("2f") for _ in range(count)]
        colors = [read("4B") for _ in range(count)]
        vertices = [p + n + uv + color for p, n, uv, color in zip(positions, normals, uvs, colors)]
        triangles = [read("3i") for _ in range(read("i")[0] // 3)]
        return texture, flags, [[vertices[i] for i in tri] for tri in triangles]


def clip(face, height, side):
    result = []
    for a, b in zip(face[-1:] + face[:-1], face):
        inside_a = (a[1] - height) * side >= 0
        inside_b = (b[1] - height) * side >= 0
        if inside_a != inside_b:
            t = (height - a[1]) / (b[1] - a[1])
            result.append(tuple(x + t * (y - x) for x, y in zip(a, b)))
        if inside_b:
            result.append(b)
    return result


def cap(edges, height, normal):
    neighbors = defaultdict(set)
    for a, b in edges:
        neighbors[a].add(b)
        neighbors[b].add(a)
    loops = []
    while neighbors:
        first = next(iter(neighbors))
        loop = [first]
        previous, current = first, next(iter(neighbors[first]))
        while current != first:
            loop.append(current)
            (forward,) = neighbors[current] - {previous}
            previous, current = current, forward
        for key in loop:
            del neighbors[key]
        loops.append([Vector((x, z, 0)) for x, z in loop])
    points = [v for loop in loops for v in loop]
    faces = []
    for triangle in tessellate_polygon(loops):
        a, b, c = [points[i] for i in triangle]
        if (b - a).cross(c - a).z * normal > 0:
            b, c = c, b
        faces.append([(p.x, height, p.y, 0, normal*127, 0, 0, 0, 216, 237, 246, 255) for p in (a, b, c)])
    return faces


def write_part(stream, texture, flags, faces):
    unique = {}
    indices = []
    for tri in faces:
        for vertex in tri:
            if vertex not in unique:
                unique[vertex] = len(unique)
            indices.append(unique[vertex])
    vertices = list(unique)
    stream.write(pack("<i", len(texture)) + texture + pack("<4i", *flags, len(vertices)))
    for v in vertices:
        stream.write(pack("<3f", *v[:3]))
    for v in vertices:
        normal = Vector(v[3:6]).normalized()
        stream.write(pack("<3b", *(round(127 * n) for n in normal)))
    for v in vertices:
        stream.write(pack("<2f", *v[6:8]))
    for v in vertices:
        stream.write(pack("<4B", *(round(n) for n in v[8:])))
    stream.write(pack("<i", len(indices)) + pack("<%di" % len(indices), *indices) + pack("<i", 0))


texture, flags, skin = load_skin()
for name, cuts in [("Base", [(.94, -1)]), ("Middle", [(.94, 1), (1.69, -1)]), ("Head", [(1.69, 1)])]:
    sides, caps = [], []
    edges = defaultdict(set)
    for source in skin:
        face = source
        for height, side in cuts:
            face = clip(face, height, side)
        if len(face) < 3:
            continue
        for height, side in cuts:
            for a, b in zip(face[-1:] + face[:-1], face):
                if abs(a[1] - height) < 1e-7 and abs(b[1] - height) < 1e-7:
                    pair = [(round(v[0], 6), round(v[2], 6)) for v in (a, b)]
                    if pair[0] != pair[1]:
                        edges[height].add(tuple(sorted(pair)))
        sides.extend([[face[0], face[i], face[i+1]] for i in range(1, len(face) - 1)])
    for height, side in cuts:
        caps.extend(cap(edges[height], height, -side))
    assert sides and caps
    with (models / ("snowman" + name + ".bytes")).open("wb") as output:
        output.write(pack("<i", 2))
        write_part(output, texture, flags, sides)
        write_part(output, b"", (0, 1, 0), caps)
    print(name, len(sides), "textured faces,", len(caps), "snow cap faces")
