"""Fit rigid poles to the authored Ski_Backflip mittens on the export copy."""
import math
import bpy
from mathutils import Vector


def add_poles(armature, mesh):
    # Work in the first posed frame so the shafts trail the skier downhill.
    # Convert the result back into binding space before weighting it to the hand.
    result = []
    palette = []
    for label, color in [
        ("Pole rubber", (.025, .035, .05, 1)),
        ("Pole shaft", (.10, .18, .25, 1)),
        ("Pole vermilion", (.95, .21, .035, 1)),
        ("Pole alloy", (.47, .56, .62, 1)),
    ]:
        mat = bpy.data.materials.new(label)
        mat.diffuse_color = color
        palette.append(mat)

    for suffix in ("L", "R"):
        bone = "hand." + suffix
        index = mesh.vertex_groups[bone].index
        points = [mesh.matrix_world @ v.co for v in mesh.data.vertices
                  if any(g.group == index and g.weight > .8 for g in v.groups)]
        if not points:
            raise ValueError("Missing mitten vertices for " + bone)
        palm = Vector([(min(p[i] for p in points) + max(p[i] for p in points)) * .5 for i in range(3)])
        skin = (armature.matrix_world @ armature.pose.bones[bone].matrix @
                armature.data.bones[bone].matrix_local.inverted() @ armature.matrix_world.inverted())
        centre = skin @ palm
        to_bind = skin.inverted()
        side = 1 if suffix == "L" else -1
        tip = Vector((-.74, .19 * side, -.77)).normalized()
        radial = tip.cross(Vector((0, 1, 0))).normalized()
        across = tip.cross(radial).normalized()
        vertices, faces, materials = [], [], []

        def point(location):
            vertices.append(to_bind @ location)
            return len(vertices) - 1

        def face(indices, material):
            faces.append(indices)
            materials.append(material)

        def tube(rows, material, segments=12):
            first = len(vertices)
            for along, radius in rows:
                for j in range(segments):
                    a = math.tau * j / segments
                    point(centre + tip * along + radius * (radial * math.cos(a) + across * math.sin(a)))
            face(tuple(first + j for j in reversed(range(segments))), material)
            for i in range(len(rows) - 1):
                for j in range(segments):
                    a = first + i * segments + j
                    b = first + i * segments + (j + 1) % segments
                    face((a, b, b + segments, a + segments), material)
            last = first + (len(rows) - 1) * segments
            face(tuple(last + j for j in range(segments)), material)

        # A flared padded handle, reflective band and powder basket remain
        # identifiable from the downhill camera without a separate draw per part.
        tube([(-.087, .016), (-.078, .023), (-.055, .019), (.065, .020), (.080, .015)], 0)
        tube([(-.0875, .016), (-.080, .0235)], 2)
        tube([(.078, .013), (.40, .011), (.95, .009), (1.04, .004)], 1)
        tube([(.14, .013), (.30, .0118)], 2)
        tube([(.32, .0119), (.35, .0116)], 3)
        tube([(.935, .010), (.945, .047), (.952, .046), (.962, .010)], 0)
        tube([(1.010, .007), (1.065, .002)], 3)

        name = "Pole." + suffix
        data = bpy.data.meshes.new(name)
        data.from_pydata(vertices, [], faces)
        data.update()
        pole = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(pole)
        for mat in palette:
            data.materials.append(mat)
        for polygon, material in zip(data.polygons, materials):
            polygon.material_index = material
            polygon.use_smooth = len(polygon.vertices) == 4
        pole.parent = armature
        pole.matrix_parent_inverse = armature.matrix_world.inverted()
        group = pole.vertex_groups.new(name=bone)
        group.add(list(range(len(vertices))), 1, "REPLACE")
        modifier = pole.modifiers.new("Grip fixed to mitten", "ARMATURE")
        modifier.object = armature
        result.append(pole)
    return result
