"""Author the production Crownfront shield knight.

This is not the old primitive assembly pass.  Every visible part is built from explicit
quad topology: swept cross-sections for anatomy/armour, shaped shells for the helmet, a
convex multi-ring shield and hand-authored extruded profiles for heraldry and weapons.
The file also carries a real bone hierarchy, named actions and dedicated collision meshes.
"""

import bpy
import math
import os
from mathutils import Vector


PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FBX_OUT = os.path.join(PROJECT, "Assets", "Resources", "CrownfrontProduction", "Tank.fbx")
BLEND_OUT = os.path.join(PROJECT, "Assets", "ArtSource~", "CrownfrontProduction", "Tank.blend")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.armatures, bpy.data.actions):
        for block in list(datablocks):
            datablocks.remove(block)


def mat(name, color, metallic=0.0, roughness=0.38):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return material


ROYAL = mat("Royal enamel", (0.018, 0.095, 0.48), .32, .24)
ROYAL_LIGHT = mat("Royal edge light", (0.03, 0.26, 0.82), .24, .22)
GOLD = mat("Warm crown gold", (1.0, .49, .035), .72, .18)
GOLD_DARK = mat("Gold recess", (.38, .075, .008), .64, .30)
STEEL = mat("Silver steel", (.48, .60, .72), .82, .20)
STEEL_DARK = mat("Dark chain steel", (.07, .105, .17), .70, .31)
LEATHER = mat("Warm leather", (.20, .045, .012), .02, .56)
SKIN = mat("Warm skin", (.93, .42, .22), 0, .43)
HAIR = mat("Chestnut hair", (.12, .022, .008), 0, .52)
EYE_WHITE = mat("Eye white", (.94, .965, 1.0), 0, .22)
EYE_BROWN = mat("Brown iris", (.19, .045, .008), 0, .18)
INK = mat("Ink", (.006, .004, .008), 0, .22)
JEWEL = mat("Cyan jewel", (.015, .48, 1.0), .18, .13)
CAPE = mat("Deep blue cape", (.006, .035, .19), .08, .45)
COLLIDER_MAT = mat("Collider debug", (.9, .1, .1), 0, 1.0)


def mesh_object(name, verts, faces, material, smooth=True, bevel=0.0):
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if material:
        obj.data.materials.append(material)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    if bevel > 0:
        modifier = obj.modifiers.new("Edge softness", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    return obj


def loft(name, rings, material, sectors=24, cap=True, bevel=0.0):
    """Build a custom swept surface.

    A ring is (x, y, z, radius_x, radius_y).  It is suitable for tapered armour and
    anatomy because every transition is explicit topology rather than a scaled primitive.
    """
    verts = []
    for cx, cy, cz, rx, ry in rings:
        for index in range(sectors):
            angle = math.tau * index / sectors
            verts.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry, cz))
    faces = []
    for ring in range(len(rings) - 1):
        base = ring * sectors
        nxt = (ring + 1) * sectors
        for index in range(sectors):
            other = (index + 1) % sectors
            faces.append((base + index, base + other, nxt + other, nxt + index))
    if cap:
        faces.append(tuple(reversed(range(sectors))))
        last = (len(rings) - 1) * sectors
        faces.append(tuple(last + index for index in range(sectors)))
    return mesh_object(name, verts, faces, material, True, bevel)


def head_mesh(name, center, radii, material, segments=32, rings=18):
    """Chibi head with a flatter facial plane and fuller rear cranium."""
    cx, cy, cz = center
    rx, ry, rz = radii
    verts = []
    for j in range(rings + 1):
        phi = -math.pi * .5 + math.pi * j / rings
        cp = math.cos(phi)
        sp = math.sin(phi)
        for i in range(segments):
            theta = math.tau * i / segments
            x = math.cos(theta) * cp
            y = math.sin(theta) * cp
            # Face points toward -Y.  Flatten only that hemisphere while leaving cheeks round.
            face_flatten = .78 if y < -.12 else 1.0
            cheek = 1.0 + .06 * max(0.0, -sp)
            verts.append((cx + x * rx * cheek, cy + y * ry * face_flatten, cz + sp * rz))
    faces = []
    for j in range(rings):
        for i in range(segments):
            ni = (i + 1) % segments
            a = j * segments + i
            b = j * segments + ni
            c = (j + 1) * segments + ni
            d = (j + 1) * segments + i
            faces.append((a, b, c, d))
    return mesh_object(name, verts, faces, material, True)


def extruded_profile(name, points, y_front, depth, material, bevel=.012):
    count = len(points)
    back = y_front + depth
    verts = [(x, y_front, z) for x, z in points] + [(x, back, z) for x, z in points]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    for i in range(count):
        j = (i + 1) % count
        faces.append((i, j, count + j, count + i))
    return mesh_object(name, verts, faces, material, False, bevel)


def ellipse_points(cx, cz, rx, rz, segments=18):
    return [(cx + math.cos(math.tau * i / segments) * rx,
             cz + math.sin(math.tau * i / segments) * rz) for i in range(segments)]


def curved_shield():
    source_outline = [
        (-.66, 1.73), (-.83, 1.53), (-.88, 1.14), (-.82, .63),
        (-.62, .27), (-.29, .02), (0, -.10), (.29, .02), (.62, .27),
        (.82, .63), (.88, 1.14), (.83, 1.53), (.66, 1.73), (0, 1.91),
    ]
    shield_center_x = -.70
    shield_center_z = .83
    outline = [(x * .80, shield_center_z + (z - .90) * .84) for x, z in source_outline]
    loops = (1.0, .86, .70, .42, .0)
    verts = []
    for loop_index, scale in enumerate(loops):
        y = -.53 - loop_index * .025 - (1.0 - scale) * .10
        if scale == 0:
            verts.append((shield_center_x, y, shield_center_z + .04))
            continue
        center_z = shield_center_z
        for x, z in outline:
            verts.append((shield_center_x + x * scale, y, center_z + (z - .90) * scale))
    faces = []
    outline_count = len(outline)
    for loop_index in range(len(loops) - 2):
        first = loop_index * outline_count
        second = (loop_index + 1) * outline_count
        for i in range(outline_count):
            j = (i + 1) % outline_count
            faces.append((first + i, first + j, second + j, second + i))
    inner = (len(loops) - 2) * outline_count
    peak = len(verts) - 1
    for i in range(outline_count):
        j = (i + 1) % outline_count
        faces.append((inner + i, inner + j, peak))
    shield = mesh_object("Royal Shield Field", verts, faces, ROYAL, True, .018)

    rim_outer = [(shield_center_x + x, shield_center_z + (z - shield_center_z)) for x, z in outline]
    rim_inner = [(shield_center_x + x * .87, shield_center_z + (z - shield_center_z) * .87) for x, z in outline]
    ring_points = rim_outer + list(reversed(rim_inner))
    extruded_profile("Royal Shield Rim", ring_points, -.566, .075, GOLD, .018)

    source_crest = [
        (-.75, 1.47), (-.66, 1.27), (-.64, .91), (-.83, 1.04),
        (-.93, .90), (-.78, .72), (-.50, .51), (-.22, .72),
        (-.07, .90), (-.17, 1.04), (-.36, .91), (-.34, 1.27),
        (-.25, 1.47), (-.50, 1.62),
    ]
    crest = [(shield_center_x + (x + .50) * .74, shield_center_z + (z - .90) * .78)
             for x, z in source_crest]
    extruded_profile("Shield Crown Emblem", crest, -.690, .038, GOLD, .010)
    gem = [(shield_center_x, .73), (shield_center_x + .09, .61),
           (shield_center_x, .49), (shield_center_x - .09, .61)]
    extruded_profile("Shield Jewel", gem, -.716, .028, JEWEL, .006)
    return shield


def helmet_shell():
    """A partial helmet shell authored as latitude rings with an open face."""
    verts = []
    faces = []
    segments = 28
    latitude_steps = 11
    for j in range(latitude_steps + 1):
        phi = math.radians(5 + 78 * j / latitude_steps)
        z = 1.73 + math.sin(phi) * .47
        horizontal = math.cos(phi)
        for i in range(segments):
            theta = math.tau * i / segments
            x = math.cos(theta) * .48 * horizontal
            y = .02 + math.sin(theta) * .40 * horizontal
            # Pull the brow forward while keeping an open lower face.
            if y < -.10:
                y -= .035 * (j / latitude_steps)
            verts.append((x, y, z))
    for j in range(latitude_steps):
        for i in range(segments):
            ni = (i + 1) % segments
            a = j * segments + i
            b = j * segments + ni
            c = (j + 1) * segments + ni
            d = (j + 1) * segments + i
            faces.append((a, b, c, d))
    shell = mesh_object("Royal Helmet Shell", verts, faces, ROYAL, True)
    # A shaped brow band hides the shell opening and carries the crown.
    brow = [(-.42, 1.91), (-.34, 2.09), (-.18, 2.19), (0, 2.23),
            (.18, 2.19), (.34, 2.09), (.42, 1.91), (.36, 1.84),
            (0, 1.94), (-.36, 1.84)]
    extruded_profile("Helmet Brow", brow, -.39, .095, GOLD, .016)
    crown = [(-.36, 1.96), (-.34, 2.22), (-.23, 2.36), (-.13, 2.17),
             (0, 2.47), (.13, 2.17), (.23, 2.36), (.34, 2.22),
             (.36, 1.96), (.24, 1.87), (-.24, 1.87)]
    extruded_profile("Crown Crest", crown, -.405, .080, GOLD, .016)
    extruded_profile("Crown Gem", [(0, 2.26), (.07, 2.15), (0, 2.04), (-.07, 2.15)],
                     -.462, .032, JEWEL, .005)
    return shell


def face_details():
    for x in (-.135, .135):
        extruded_profile("Eye White", ellipse_points(x, 1.78, .055, .069), -.384, .016, EYE_WHITE, .004)
        extruded_profile("Iris", ellipse_points(x, 1.78, .037, .051), -.397, .012, EYE_BROWN, .003)
        extruded_profile("Pupil", ellipse_points(x, 1.78, .015, .026), -.407, .008, INK, .002)
        extruded_profile("Eye Highlight", ellipse_points(x-.010, 1.801, .007, .010, 12),
                         -.414, .005, EYE_WHITE, .001)
        brow = [(x - .07, 1.91), (x, 1.94), (x + .07, 1.90), (x + .065, 1.875), (x, 1.895), (x - .065, 1.88)]
        extruded_profile("Determined Brow", brow, -.425, .020, HAIR, .004)
    head_mesh("Nose", (0, -.412, 1.69), (.041, .022, .044), SKIN, 14, 8)
    mouth = [(-.065, 1.605), (0, 1.588), (.065, 1.605), (.060, 1.620), (0, 1.607), (-.060, 1.620)]
    extruded_profile("Mouth", mouth, -.416, .012, INK, .003)


def armour_body():
    torso = loft("Armoured Torso", [
        (0, .02, .62, .32, .24),
        (0, .01, .78, .43, .30),
        (0, .00, 1.12, .49, .33),
        (0, .01, 1.40, .39, .28),
    ], ROYAL, 28)
    # Raised breastplate, collar, chain faulds and cape give readable material separation.
    chest = [(-.37, 1.34), (-.46, 1.12), (-.39, .77), (-.22, .61),
             (0, .55), (.22, .61), (.39, .77), (.46, 1.12), (.37, 1.34), (0, 1.47)]
    extruded_profile("Raised Breastplate", chest, -.337, .072, ROYAL_LIGHT, .018)
    collar = [(-.38, 1.39), (-.22, 1.48), (0, 1.43), (.22, 1.48),
              (.38, 1.39), (.34, 1.31), (0, 1.35), (-.34, 1.31)]
    extruded_profile("Gold Collar", collar, -.383, .045, GOLD, .011)
    chest_mark = [(-.13, 1.19), (-.06, 1.08), (-.06, .94), (-.15, 1.01),
                  (-.21, .91), (-.11, .81), (0, .74), (.11, .81),
                  (.21, .91), (.15, 1.01), (.06, .94), (.06, 1.08),
                  (.13, 1.19), (0, 1.31)]
    extruded_profile("Cuirass Crown Emblem", chest_mark, -.427, .028, GOLD, .007)
    extruded_profile("Cuirass Jewel", [(0, .98), (.055, .90), (0, .82), (-.055, .90)],
                     -.449, .020, JEWEL, .004)
    for index, x in enumerate((-.27, 0, .27)):
        fauld = [(x-.13, .73), (x-.16, .48), (x, .40), (x+.16, .48), (x+.13, .73)]
        extruded_profile("Chain Fauld " + str(index), fauld, -.30, .055, STEEL_DARK, .012)
    cape = [(-.43, 1.38), (-.52, .94), (-.47, .35), (-.27, .14),
            (0, .08), (.27, .14), (.47, .35), (.52, .94), (.43, 1.38), (0, 1.49)]
    extruded_profile("Royal Cape", cape, .26, .070, CAPE, .025)
    return torso


def make_limbs():
    # Locally curved/tapered profiles make joints read even in the tactical camera.
    parts = {}
    for side, x in (("L", -.24), ("R", .24)):
        parts["Leg." + side] = loft("Leg Armour." + side, [
            (x, .02, .22, .18, .17),
            (x, .02, .39, .16, .15),
            (x, .02, .63, .15, .14),
        ], STEEL_DARK, 18)
        parts["Boot." + side] = loft("Gold Boot." + side, [
            (x, -.10, .05, .23, .31),
            (x, -.10, .16, .24, .29),
            (x, -.02, .25, .19, .19),
        ], GOLD, 20)
    for side, x in (("L", -.50), ("R", .50)):
        parts["Arm." + side] = loft("Armour Arm." + side, [
            (x, .00, .62, .15, .14),
            (x, .00, .86, .14, .13),
            (x, .00, 1.12, .18, .17),
        ], ROYAL, 18)
        parts["Hand." + side] = head_mesh("Leather Gauntlet." + side,
                                           (x, -.04, .56), (.17, .16, .17), LEATHER, 18, 10)
        parts["Pauldron." + side] = loft("Gold Pauldron." + side, [
            (x, .01, 1.06, .20, .19),
            (x, .01, 1.18, .25, .23),
            (x, .01, 1.30, .15, .17),
        ], GOLD, 20)
    return parts


def make_sword():
    blade = [(.56, .65), (.59, .34), (.62, -.04), (.71, -.30),
             (.80, -.04), (.83, .34), (.80, .65)]
    sword = extruded_profile("Royal Broad Sword", blade, -.12, .090, STEEL, .010)
    fuller = [(.675, .52), (.682, .06), (.71, -.16), (.738, .06), (.745, .52)]
    extruded_profile("Sword Fuller", fuller, -.176, .024, ROYAL_LIGHT, .004)
    guard = [(.50, .68), (.48, .75), (.64, .80), (.71, .89), (.78, .80), (.94, .75), (.92, .68)]
    extruded_profile("Sword Guard", guard, -.13, .110, GOLD, .012)
    loft("Sword Grip", [
        (.71, -.05, .82, .052, .050),
        (.71, -.05, 1.02, .052, .050),
    ], LEATHER, 14)
    head_mesh("Sword Pommel", (.71, -.05, 1.08), (.09, .075, .09), GOLD, 14, 8)
    return sword


def create_armature():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "CrownfrontArmature"
    data = armature.data
    data.name = "Crownfront Shield Knight Rig"
    root = data.edit_bones[0]
    root.name = "Root"
    root.head = (0, 0, 0)
    root.tail = (0, 0, .35)

    def bone(name, head, tail, parent_name):
        item = data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.parent = data.edit_bones[parent_name]
        return item

    bone("Body", (0, 0, .35), (0, 0, 1.38), "Root")
    bone("Head", (0, 0, 1.38), (0, 0, 2.12), "Body")
    bone("Leg.L", (-.24, 0, .56), (-.24, 0, .08), "Body")
    bone("Leg.R", (.24, 0, .56), (.24, 0, .08), "Body")
    bone("Arm.L", (-.42, 0, 1.22), (-.55, 0, .62), "Body")
    bone("Arm.R", (.42, 0, 1.22), (.55, 0, .62), "Body")
    bone("Offhand", (-.55, 0, .72), (-.55, -.2, .55), "Arm.L")
    bone("Weapon", (.55, 0, .72), (.70, -.1, .48), "Arm.R")
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def bone_parent(obj, armature, bone_name):
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


def create_actions(armature):
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)

    def pose_action(name, frames, loop):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        armature.animation_data_create()
        armature.animation_data.action = action
        for frame, rotations, positions in frames:
            for bone_name, angles in rotations.items():
                pose_bone = armature.pose.bones[bone_name]
                pose_bone.rotation_mode = "XYZ"
                pose_bone.rotation_euler = tuple(math.radians(v) for v in angles)
                pose_bone.keyframe_insert("rotation_euler", frame=frame, group=bone_name)
            for bone_name, position in positions.items():
                pose_bone = armature.pose.bones[bone_name]
                pose_bone.location = position
                pose_bone.keyframe_insert("location", frame=frame, group=bone_name)
        action["loop"] = loop
        return action

    idle = [
        (1, {"Head": (0, -3, 0), "Body": (0, 0, -1)}, {"Body": (0, 0, 0)}),
        (18, {"Head": (0, 3, 0), "Body": (0, 0, 1)}, {"Body": (0, 0, .025)}),
        (36, {"Head": (0, -3, 0), "Body": (0, 0, -1)}, {"Body": (0, 0, 0)}),
    ]
    walk = [
        (1, {"Leg.L": (28, 0, 0), "Leg.R": (-28, 0, 0), "Arm.L": (-12, 0, 0), "Arm.R": (18, 0, 0)}, {"Body": (0, 0, 0)}),
        (7, {"Leg.L": (0, 0, 0), "Leg.R": (0, 0, 0), "Arm.L": (0, 0, 0), "Arm.R": (0, 0, 0)}, {"Body": (0, 0, .045)}),
        (13, {"Leg.L": (-28, 0, 0), "Leg.R": (28, 0, 0), "Arm.L": (12, 0, 0), "Arm.R": (-18, 0, 0)}, {"Body": (0, 0, 0)}),
        (19, {"Leg.L": (0, 0, 0), "Leg.R": (0, 0, 0), "Arm.L": (0, 0, 0), "Arm.R": (0, 0, 0)}, {"Body": (0, 0, .045)}),
        (25, {"Leg.L": (28, 0, 0), "Leg.R": (-28, 0, 0), "Arm.L": (-12, 0, 0), "Arm.R": (18, 0, 0)}, {"Body": (0, 0, 0)}),
    ]
    attack = [
        (1, {"Body": (0, 0, 0), "Arm.R": (-18, -8, 8), "Weapon": (-16, 0, 0), "Arm.L": (0, 0, 0)}, {}),
        (7, {"Body": (0, -9, -4), "Arm.R": (-68, 12, 22), "Weapon": (-35, 0, 0), "Arm.L": (-8, 0, 5)}, {}),
        (11, {"Body": (0, 10, 4), "Arm.R": (42, -14, -32), "Weapon": (24, 0, 0), "Arm.L": (8, 0, -6)}, {}),
        (18, {"Body": (0, 0, 0), "Arm.R": (-18, -8, 8), "Weapon": (-16, 0, 0), "Arm.L": (0, 0, 0)}, {}),
    ]
    guard = [
        (1, {"Arm.L": (-12, 0, -8), "Offhand": (0, 0, 0), "Body": (0, 0, 0)}, {}),
        (6, {"Arm.L": (-38, 8, 8), "Offhand": (-10, 0, -8), "Body": (0, -5, 0)}, {}),
        (14, {"Arm.L": (-28, 4, 3), "Offhand": (-5, 0, -4), "Body": (0, 0, 0)}, {}),
    ]
    skill = [
        (1, {"Body": (0, 0, 0), "Arm.L": (-20, 0, -6), "Offhand": (0, 0, 0)}, {}),
        (8, {"Body": (0, -12, 0), "Arm.L": (-72, 8, 14), "Offhand": (-18, 0, -12)}, {}),
        (13, {"Body": (0, 14, 0), "Arm.L": (34, -8, -16), "Offhand": (20, 0, 10)}, {}),
        (22, {"Body": (0, 0, 0), "Arm.L": (-20, 0, -6), "Offhand": (0, 0, 0)}, {}),
    ]
    hurt = [
        (1, {"Body": (0, 0, 0), "Head": (0, 0, 0)}, {}),
        (4, {"Body": (-7, 0, 9), "Head": (8, 0, -8)}, {"Body": (0, .03, -.04)}),
        (10, {"Body": (0, 0, 0), "Head": (0, 0, 0)}, {"Body": (0, 0, 0)}),
    ]
    ultimate = [
        (1, {"Body": (0, 0, 0), "Arm.L": (-20, 0, -8), "Arm.R": (-20, 0, 8)}, {}),
        (10, {"Body": (0, -14, 0), "Arm.L": (-88, 10, 24), "Arm.R": (-70, -10, -24)}, {"Body": (0, 0, .05)}),
        (19, {"Body": (0, 18, 0), "Arm.L": (52, -8, -28), "Arm.R": (48, 8, 28)}, {"Body": (0, -.04, .12)}),
        (34, {"Body": (0, 0, 0), "Arm.L": (-20, 0, -8), "Arm.R": (-20, 0, 8)}, {"Body": (0, 0, 0)}),
    ]
    death = [
        (1, {"Body": (0, 0, 0), "Head": (0, 0, 0)}, {}),
        (18, {"Body": (68, 0, -12), "Head": (-24, 0, 8), "Arm.L": (35, 0, 0), "Arm.R": (-30, 0, 0)}, {"Body": (0, .04, -.16)}),
    ]
    for name, frames, loop in (("Idle", idle, True), ("Walk", walk, True),
                               ("BasicAttack", attack, False), ("Guard", guard, False),
                               ("Skill", skill, False), ("Hurt", hurt, False),
                               ("Ultimate", ultimate, False), ("Death", death, False)):
        pose_action(name, frames, loop)
    armature.animation_data.action = bpy.data.actions["Idle"]


def collider_capsule(name, center, radii, rings=8, sectors=12):
    cx, cy, cz = center
    rx, ry, rz = radii
    obj = head_mesh(name, center, radii, COLLIDER_MAT, sectors, rings)
    obj["crownfront_collider"] = True
    obj.hide_render = True
    return obj


def build():
    clear_scene()
    armature = create_armature()

    torso = armour_body()
    head = head_mesh("Knight Face", (0, -.035, 1.77), (.47, .39, .43), SKIN)
    helmet = helmet_shell()
    face_details()
    limbs = make_limbs()
    shield = curved_shield()
    sword = make_sword()

    body_parts = [torso]
    head_parts = [head, helmet] + [obj for obj in bpy.context.scene.objects if obj != armature
                                  if obj.name.startswith(("Eye", "Iris", "Pupil", "Determined", "Nose", "Mouth",
                                                          "Helmet", "Crown"))]
    for obj in body_parts:
        bone_parent(obj, armature, "Body")
    for obj in head_parts:
        if obj.parent is None:
            bone_parent(obj, armature, "Head")
    for key, obj in limbs.items():
        bone = "Leg.L" if ".L" in key and ("Leg" in key or "Boot" in key) else \
               "Leg.R" if ".R" in key and ("Leg" in key or "Boot" in key) else \
               "Arm.L" if ".L" in key else "Arm.R"
        bone_parent(obj, armature, bone)
    for obj in bpy.context.scene.objects:
        if obj.parent is not None or obj == armature:
            continue
        if obj.name.startswith(("Royal Shield", "Shield ")):
            bone_parent(obj, armature, "Offhand")
        elif obj.name.startswith(("Royal Broad Sword", "Sword ")):
            bone_parent(obj, armature, "Weapon")
        elif obj.name.startswith(("Raised", "Gold Collar", "Cuirass", "Chain Fauld", "Royal Cape")):
            bone_parent(obj, armature, "Body")

    body_col = collider_capsule("COL_Body", (0, .02, .96), (.48, .31, .66))
    shield_col = collider_capsule("COL_Shield", (-.70, -.49, .84), (.62, .12, .73))
    bone_parent(body_col, armature, "Body")
    bone_parent(shield_col, armature, "Offhand")
    create_actions(armature)

    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 36
    bpy.context.scene.render.fps = 30
    bpy.context.scene["asset_family"] = "CrownfrontProduction"
    bpy.context.scene["design_reference"] = "defender-atlas-v1 shield knight"
    bpy.context.scene["collision_meshes"] = "COL_Body,COL_Shield"

    os.makedirs(os.path.dirname(BLEND_OUT), exist_ok=True)
    os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT, check_existing=False)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=FBX_OUT,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("PRODUCTION_TANK", FBX_OUT)
    print("PRODUCTION_BLEND", BLEND_OUT)


build()
