"""Build the CROWNFRONT defender roster as real, bevelled 3D model hierarchies.

The models intentionally follow the existing screen roster: crown shield knight, coral hammer
fighter, mint hood archer, violet star caster and blue orb caster.  Empty transforms form a
small runtime rig; Unity drives those transforms for walk, strike and cast poses.
"""

import bpy
import math
import os


OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Crownfront3D"))
SOURCE_OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "ArtSource~", "Crownfront3D"))


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def material(name, color, metallic=0.0, roughness=0.42):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, 1.0)
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Roughness"].default_value = roughness
    return mat


PALETTE = {
    "blue": material("Royal Blue", (0.018, 0.105, 0.52), 0.22, 0.27),
    "blue_light": material("Azure", (0.015, 0.34, 0.83), 0.10, 0.26),
    "red": material("Coral Red", (0.72, 0.055, 0.025), 0.22, 0.36),
    "mint": material("Mint Green", (0.025, 0.48, 0.42), 0.10, 0.40),
    "purple": material("Star Violet", (0.38, 0.06, 0.70), 0.17, 0.32),
    "gold": material("Crown Gold", (1.0, 0.56, 0.045), 0.56, 0.18),
    "steel": material("Polished Steel", (0.36, 0.48, 0.63), 0.86, 0.22),
    "steel_dark": material("Dark Steel", (0.09, 0.13, 0.20), 0.72, 0.28),
    "leather": material("Warm Leather", (0.19, 0.055, 0.018), 0.03, 0.58),
    "skin": material("Warm Skin", (0.92, 0.39, 0.19), 0.0, 0.44),
    "hair": material("Chestnut Hair", (0.14, 0.025, 0.01), 0.0, 0.48),
    "eye": material("Ink Eyes", (0.012, 0.006, 0.01), 0.0, 0.30),
    "orb_blue": material("Orb Blue", (0.02, 0.58, 1.0), 0.10, 0.15),
    "orb_violet": material("Orb Violet", (0.78, 0.20, 1.0), 0.12, 0.16),
}


def parent(obj, node, local):
    obj.parent = node
    obj.location = local
    return obj


def empty(name, node=None, local=(0, 0, 0)):
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    if node:
        parent(obj, node, local)
    return obj


def finish(obj, name, node, local, mat, bevel=0.0):
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if bevel > 0:
        mod = obj.modifiers.new("Soft Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if hasattr(obj.data, "polygons"):
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return parent(obj, node, local)


def sphere(name, node, local, scale, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=(0, 0, 0))
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, node, local, mat)


def cube(name, node, local, scale, mat, bevel=0.05):
    bpy.ops.mesh.primitive_cube_add(location=(0, 0, 0))
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, node, local, mat, bevel)


def cylinder(name, node, local, radius, depth, mat, vertices=16, rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=(0, 0, 0))
    obj = bpy.context.object
    if rotation:
        obj.rotation_euler = rotation
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return finish(obj, name, node, local, mat, 0.025)


def cone(name, node, local, radius1, radius2, depth, mat, vertices=16):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=(0, 0, 0))
    return finish(bpy.context.object, name, node, local, mat, 0.018)


def torus(name, node, local, major, minor, mat):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=20, minor_segments=8, location=(0, 0, 0))
    return finish(bpy.context.object, name, node, local, mat)


def make_root(label):
    root = empty("CrownfrontRig")
    root["unit_label"] = label
    body = empty("Body", root, (0, 0, 0))
    head = empty("Head", body, (0, -0.01, 1.28))
    leg_l = empty("Leg.L", body, (-0.23, 0, 0.50))
    leg_r = empty("Leg.R", body, (0.23, 0, 0.50))
    arm_l = empty("Arm.L", body, (-0.48, 0, 1.05))
    arm_r = empty("Arm.R", body, (0.48, 0, 1.05))
    weapon = empty("Weapon", arm_r, (0, -0.01, -0.16))
    offhand = empty("Offhand", arm_l, (0, -0.01, -0.16))
    return root, body, head, leg_l, leg_r, arm_l, arm_r, weapon, offhand


def add_face(head, eye_color=PALETTE["eye"]):
    sphere("Face", head, (0, -0.17, 0), (0.38, 0.32, 0.36), PALETTE["skin"])
    sphere("Eye.L", head, (-0.115, -0.47, 0.02), (0.055, 0.025, 0.075), eye_color)
    sphere("Eye.R", head, (0.115, -0.47, 0.02), (0.055, 0.025, 0.075), eye_color)
    sphere("Nose", head, (0, -0.49, -0.06), (0.045, 0.02, 0.04), PALETTE["skin"])


def add_body(body, leg_l, leg_r, arm_l, arm_r, main, trim):
    # boots and articulated limbs
    sphere("Boot.L", leg_l, (0, -0.04, -0.26), (0.22, 0.28, 0.14), PALETTE["steel_dark"])
    sphere("Boot.R", leg_r, (0, -0.04, -0.26), (0.22, 0.28, 0.14), PALETTE["steel_dark"])
    cylinder("Greave.L", leg_l, (0, 0, -0.02), 0.15, 0.50, trim)
    cylinder("Greave.R", leg_r, (0, 0, -0.02), 0.15, 0.50, trim)
    sphere("Torso", body, (0, 0.02, 0.93), (0.48, 0.30, 0.54), main)
    cube("Belt", body, (0, -0.02, 0.73), (0.39, 0.20, 0.07), PALETTE["leather"], 0.035)
    cube("Buckle", body, (0, -0.245, 0.74), (0.09, 0.03, 0.10), PALETTE["gold"], 0.02)
    cylinder("Armour Arm.L", arm_l, (0, 0, -0.13), 0.14, 0.44, main)
    cylinder("Armour Arm.R", arm_r, (0, 0, -0.13), 0.14, 0.44, main)
    sphere("Glove.L", arm_l, (0, -0.05, -0.40), (0.16, 0.16, 0.16), PALETTE["leather"])
    sphere("Glove.R", arm_r, (0, -0.05, -0.40), (0.16, 0.16, 0.16), PALETTE["leather"])


def add_crown(head):
    torus("Crown Band", head, (0, -0.02, 0.30), 0.30, 0.045, PALETTE["gold"])
    for x, height in ((-0.20, 0.22), (0.0, 0.30), (0.20, 0.22)):
        cone("Crown Point", head, (x, -0.02, 0.48), 0.065, 0.0, height, PALETTE["gold"], 8)


def add_shield(offhand):
    # a thick bevelled octagonal shield, not a flat card
    cylinder("Tower Shield Gold Rim", offhand, (-0.18, -0.20, -0.06), 0.48, 0.12, PALETTE["gold"], 8,
             (math.radians(90), 0, 0))
    face = cylinder("Tower Shield Blue Face", offhand, (-0.18, -0.27, -0.06), 0.39, 0.075, PALETTE["blue"], 8,
                    (math.radians(90), 0, 0))
    face.scale.z = 1.24
    sphere("Shield Jewel", offhand, (-0.18, -0.33, -0.05), (0.105, 0.035, 0.145), PALETTE["orb_blue"])


def add_sword(weapon):
    cube("Sword Blade", weapon, (0.10, -0.04, -0.47), (0.075, 0.055, 0.42), PALETTE["steel"], 0.026)
    cone("Sword Tip", weapon, (0.10, -0.04, -0.91), 0.11, 0.0, 0.22, PALETTE["steel"], 4)
    cube("Sword Guard", weapon, (0.10, -0.04, -0.08), (0.25, 0.05, 0.045), PALETTE["gold"], 0.018)


def build_tank():
    root, body, head, ll, lr, al, ar, weapon, offhand = make_root("Crown Shield Knight")
    add_body(body, ll, lr, al, ar, PALETTE["blue"], PALETTE["steel"])
    add_face(head)
    sphere("Helmet Dome", head, (0, 0.01, 0.22), (0.43, 0.36, 0.25), PALETTE["blue"])
    cube("Helmet Brow", head, (0, -0.44, 0.22), (0.31, 0.035, 0.06), PALETTE["gold"], 0.018)
    add_crown(head)
    add_shield(offhand)
    add_sword(weapon)
    return root


def build_hammer():
    root, body, head, ll, lr, al, ar, weapon, offhand = make_root("Coral Hammer Fighter")
    add_body(body, ll, lr, al, ar, PALETTE["red"], PALETTE["steel_dark"])
    add_face(head)
    sphere("Red Helmet", head, (0, 0.02, 0.22), (0.43, 0.36, 0.25), PALETTE["red"])
    cube("Helmet Crest", head, (0, -0.04, 0.48), (0.10, 0.20, 0.11), PALETTE["gold"], 0.025)
    cylinder("Hammer Handle", weapon, (0.08, -0.02, -0.40), 0.055, 0.78, PALETTE["leather"])
    cube("Hammer Head", weapon, (0.08, -0.03, -0.79), (0.27, 0.16, 0.14), PALETTE["red"], 0.06)
    cylinder("Hammer Band", weapon, (0.08, -0.19, -0.79), 0.11, 0.16, PALETTE["steel"], 12, (math.radians(90), 0, 0))
    return root


def build_archer():
    root, body, head, ll, lr, al, ar, weapon, offhand = make_root("Mint Hood Archer")
    add_body(body, ll, lr, al, ar, PALETTE["mint"], PALETTE["leather"])
    add_face(head)
    sphere("Mint Hood", head, (0, 0.02, 0.18), (0.46, 0.38, 0.31), PALETTE["mint"])
    cube("Hood Trim", head, (0, -0.44, 0.16), (0.30, 0.035, 0.045), PALETTE["gold"], 0.012)
    # bow made from bevelled arcs/limbs, with an actual hand-held depth
    cylinder("Bow Grip", weapon, (0.06, -0.09, -0.38), 0.038, 0.52, PALETTE["leather"])
    upper = cylinder("Bow Upper", weapon, (0.17, -0.10, -0.18), 0.033, 0.40, PALETTE["gold"])
    upper.rotation_euler.x = math.radians(-30)
    lower = cylinder("Bow Lower", weapon, (0.17, -0.10, -0.58), 0.033, 0.40, PALETTE["gold"])
    lower.rotation_euler.x = math.radians(30)
    cube("Arrow", weapon, (0.0, -0.22, -0.38), (0.018, 0.02, 0.42), PALETTE["steel"], 0.006)
    return root


def add_wizard_hat(head, main):
    cylinder("Hat Brim", head, (0, 0.0, 0.32), 0.40, 0.08, main)
    cone("Wizard Hat", head, (0, 0.02, 0.66), 0.29, 0.03, 0.70, main)
    sphere("Hat Gem", head, (0, -0.31, 0.46), (0.09, 0.035, 0.10), PALETTE["gold"])


def add_staff(weapon, orb):
    cylinder("Staff Shaft", weapon, (0.08, -0.02, -0.38), 0.045, 0.76, PALETTE["leather"])
    sphere("Focus Orb", weapon, (0.08, -0.05, 0.04), (0.16, 0.16, 0.16), orb)
    cylinder("Orb Ring", weapon, (0.08, -0.06, 0.04), 0.20, 0.035, PALETTE["gold"], 16, (math.radians(90), 0, 0))


def build_area_mage():
    root, body, head, ll, lr, al, ar, weapon, offhand = make_root("Star Powder Mage")
    add_body(body, ll, lr, al, ar, PALETTE["purple"], PALETTE["steel_dark"])
    add_face(head, PALETTE["orb_violet"])
    add_wizard_hat(head, PALETTE["purple"])
    add_staff(weapon, PALETTE["orb_violet"])
    sphere("Star Emblem", body, (0, -0.33, 1.05), (0.10, 0.035, 0.10), PALETTE["gold"])
    return root


def build_orb_mage():
    root, body, head, ll, lr, al, ar, weapon, offhand = make_root("Glass Orb Mage")
    add_body(body, ll, lr, al, ar, PALETTE["blue_light"], PALETTE["steel_dark"])
    add_face(head, PALETTE["orb_blue"])
    sphere("Azure Hood", head, (0, 0.02, 0.18), (0.47, 0.39, 0.32), PALETTE["blue_light"])
    cube("Azure Hood Trim", head, (0, -0.44, 0.16), (0.30, 0.035, 0.045), PALETTE["gold"], 0.012)
    add_staff(weapon, PALETTE["orb_blue"])
    return root


def export_model(name, builder):
    clear_scene()
    builder()
    bpy.ops.object.select_all(action="SELECT")
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("EXPORTED", path)
    # Keep an editable Blender source per hero outside Unity's import tree.  Future mesh,
    # material and animation passes never have to reverse-engineer an exported FBX.
    os.makedirs(SOURCE_OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_OUT, name + ".blend"), check_existing=False)


# -----------------------------------------------------------------------------
# Polished CROWNFRONT pass
#
# The first pass above was deliberately kept as an editable construction study.  The game
# ships the following second pass instead: its silhouettes are custom extruded meshes and
# layered armour pieces, not a stack of proxy primitives.  Keeping it in this Blender source
# makes every role reproducible and gives Unity a genuine pivot hierarchy for its runtime
# walk/strike poses.

def prism_profile(name, node, local, points, depth, mat, bevel=0.018):
    """Create a softly bevelled X/Z profile with real thickness on the Y axis."""
    half = depth * .5
    verts = [(x, -half, z) for x, z in points] + [(x, half, z) for x, z in points]
    count = len(points)
    faces = [tuple(range(count)), tuple(range(count, count * 2))]
    for i in range(count):
        j = (i + 1) % count
        faces.append((i, j, count + j, count + i))
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, node, local, mat, bevel)


def prism_scaled(name, node, local, points, sx, sz, depth, mat, bevel=0.018):
    return prism_profile(name, node, local, [(x * sx, z * sz) for x, z in points], depth, mat, bevel)


def domed_profile(name, node, local, points, depth, bulge, mat, bevel=0.014):
    """A convex front for shields/crests; it catches light like an object, never a card."""
    half = depth * .5
    count = len(points)
    verts = [(x, half, z) for x, z in points] + [(x, -half, z) for x, z in points]
    verts.append((0, -half - bulge, 0))
    peak = len(verts) - 1
    faces = [tuple(range(count))]
    for i in range(count):
        j = (i + 1) % count
        faces.append((count + i, count + j, peak))
        faces.append((i, j, count + j, count + i))
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, node, local, mat, bevel)


def arc_tube(name, node, local, points, radius, mat, resolution=2):
    curve = bpy.data.curves.new(name + " Curve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bp, co in zip(spline.bezier_points, points):
        bp.co = co
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    return parent(obj, node, local)


def polished_root(label):
    root = empty("CrownfrontRig")
    root["unit_label"] = label
    root["art_pass"] = "Crownfront bespoke v2"
    body = empty("Body", root, (0, 0, 0))
    head = empty("Head", body, (0, -0.025, 1.52))
    leg_l = empty("Leg.L", body, (-0.235, 0, 0.54))
    leg_r = empty("Leg.R", body, (0.235, 0, 0.54))
    arm_l = empty("Arm.L", body, (-0.50, -0.015, 1.11))
    arm_r = empty("Arm.R", body, (0.50, -0.015, 1.11))
    weapon = empty("Weapon", arm_r, (0, -0.02, -0.18))
    offhand = empty("Offhand", arm_l, (0, -0.02, -0.15))
    return root, body, head, leg_l, leg_r, arm_l, arm_r, weapon, offhand


WHITE = material("Eye White", (0.93, 0.96, 1.0), 0.0, 0.24)
BROWN = material("Iris Brown", (0.19, 0.052, 0.012), 0.0, 0.20)
GOLD_DARK = material("Crown Gold Shadow", (0.46, 0.14, 0.008), 0.68, 0.28)
CAPE_BLUE = material("Cape Royal Blue", (0.012, 0.09, 0.40), 0.10, 0.45)
CAPE_RED = material("Cape Coral Red", (0.42, 0.018, 0.012), 0.06, 0.46)
MINT_DARK = material("Hood Forest Shadow", (0.008, 0.20, 0.17), 0.0, 0.47)
PURPLE_DARK = material("Robe Violet Shadow", (0.115, 0.012, 0.25), 0.0, 0.46)


def polished_face(head, eye_tint=BROWN, hair=PALETTE["hair"]):
    sphere("Face", head, (0, -0.16, -0.015), (0.405, 0.335, 0.365), PALETTE["skin"])
    # The facial plane is intentionally layered: whites, iris, pupil, brows and mouth still
    # read on a small isometric camera instead of collapsing into the old two black dots.
    for x in (-0.125, 0.125):
        sphere("Eye White", head, (x, -0.474, 0.025), (0.060, 0.018, 0.070), WHITE)
        sphere("Iris", head, (x, -0.500, 0.024), (0.044, 0.014, 0.052), eye_tint)
        sphere("Pupil", head, (x, -0.516, 0.024), (0.016, 0.008, 0.024), PALETTE["eye"])
        arc_tube("Brow", head, (0, 0, 0), [(x - .058, -.502, .125), (x, -.520, .144), (x + .058, -.502, .125)], .014, hair)
    arc_tube("Mouth", head, (0, 0, 0), [(-.064, -.505, -.145), (0, -.518, -.154), (.064, -.505, -.145)], .010, hair)
    sphere("Nose", head, (0, -.505, -.060), (.043, .021, .047), PALETTE["skin"])


def add_plated_legs(leg_l, leg_r, main, accent):
    for side, leg in (("L", leg_l), ("R", leg_r)):
        sphere("Boot " + side, leg, (0, -.09, -.255), (.245, .31, .155), accent)
        sphere("Knee " + side, leg, (0, -.035, .035), (.175, .18, .175), main)
        cylinder("Greave " + side, leg, (0, 0, -.035), .145, .49, main, 16)
        arc_tube("Greave Rim " + side, leg, (0, 0, 0), [(-.118, -.10, .11), (0, -.17, .13), (.118, -.10, .11)], .020, accent)


def add_knight_torso(body, leg_l, leg_r, arm_l, arm_r, main, cape, trim=PALETTE["gold"]):
    add_plated_legs(leg_l, leg_r, main, trim)
    # Back cape is a shaped, thick silhouette rather than a flat rectangle.
    cape_pts = [(-.47, 1.40), (-.55, .62), (-.39, .26), (0, .16), (.39, .26), (.55, .62), (.47, 1.40), (0, 1.55)]
    prism_profile("Layered Cape", body, (0, .265, 0), cape_pts, .10, cape, .035)
    sphere("Armour Core", body, (0, .01, 1.02), (.49, .33, .57), main)
    chest = prism_profile("Raised Chestplate", body, (0, -.335, 1.07), [(-.39,.30),(-.45,.06),(-.30,-.42),(0,-.52),(.30,-.42),(.45,.06),(.39,.30),(0,.43)], .075, main, .03)
    chest_crest = [(-.15,.18),(-.08,.07),(-.08,-.10),(-.17,-.02),(-.22,-.14),(-.12,-.25),(0,-.31),(.12,-.25),(.22,-.14),(.17,-.02),(.08,-.10),(.08,.07),(.15,.18),(0,.28)]
    prism_profile("Cuirass Crown Crest", body, (0, -.393, 1.07), chest_crest, .026, trim, .008)
    prism_profile("Cuirass Jewel", body, (0, -.418, .92), [(0,.065),(.055,0),(0,-.065),(-.055,0)], .018, PALETTE["orb_blue"], .005)
    for index, x in enumerate((-.26, 0, .26)):
        prism_profile("Armour Fauld " + str(index), body, (x, -.34, .60), [(-.13,.12),(-.16,-.10),(.16,-.10),(.13,.12)], .055, PALETTE["steel"], .014)
    # Layered gold collar and belt establish the blue/gold Crownfront silhouette.
    arc_tube("Gold Collar", body, (0, -.37, 1.37), [(-.37, 0, -.015), (0, -.015, .10), (.37, 0, -.015)], .047, trim)
    cube("Wide Belt", body, (0, -.02, .72), (.40, .23, .075), PALETTE["leather"], .038)
    prism_profile("Buckle", body, (0, -.275, .73), [(-.09,.11),(-.12,0),(-.09,-.11),(.09,-.11),(.12,0),(.09,.11)], .05, trim, .012)
    # Shoulder plates and bracers are separate meshes so the arm pivots can animate cleanly.
    for side, arm in (("L", arm_l), ("R", arm_r)):
        sphere("Pauldron " + side, arm, (0, .015, .09), (.23, .25, .20), main)
        arc_tube("Pauldron Rim " + side, arm, (0, 0, .075), [(-.15,-.10,.04),(0,-.16,.17),(.15,-.10,.04)], .025, trim)
        cylinder("Bracer " + side, arm, (0, 0, -.20), .145, .38, main, 16)
        sphere("Glove " + side, arm, (0, -.06, -.43), (.165, .17, .17), PALETTE["leather"])


def knight_helmet(head, main, trim=PALETTE["gold"]):
    sphere("Helmet Dome", head, (0, .025, .245), (.465, .385, .305), main)
    arc_tube("Helmet Gold Rim", head, (0, 0, 0), [(-.38,-.35,.16),(-.25,-.44,.31),(0,-.47,.36),(.25,-.44,.31),(.38,-.35,.16)], .034, trim)
    for x in (-.25, .25):
        sphere("Helmet Rivet", head, (x, -.39, .245), (.035,.020,.035), trim)
    # A raised crest and deliberate forehead crown mirror the original 2D knight rather than
    # borrowing an unrelated generic helmet.
    arc_tube("Helmet Crest", head, (0, .015, .35), [(0, -.05, .10), (0, .01, .34), (0, .05, .47)], .052, trim)
    crown = [(-.35,-.08),(-.34,.18),(-.22,.31),(-.13,.12),(0,.42),(.13,.12),(.22,.31),(.34,.18),(.35,-.08),(.24,-.18),(-.24,-.18)]
    prism_scaled("Crown Forehead", head, (0, -.445, .285), crown, .84, .69, .075, trim, .018)
    # Blue jewel in the crown and side cheek guards retain visual identity at a glance.
    prism_profile("Crown Jewel", head, (0, -.497, .28), [(0,.073),(.052,0),(0,-.073),(-.052,0)], .035, PALETTE["orb_blue"], .008)
    for x, sign in ((-.36,-1),(.36,1)):
        prism_profile("Cheek Guard", head, (x, -.33, -.045), [(0,.18),(sign*.12,.08),(sign*.12,-.22),(0,-.30)], .10, trim, .015)


def shield_profile(offhand, main=PALETTE["blue"]):
    outline = [(-.46,.62),(-.58,.38),(-.61,-.08),(-.52,-.48),(-.25,-.73),(0,-.80),(.25,-.73),(.52,-.48),(.61,-.08),(.58,.38),(.46,.62),(0,.76)]
    rim = prism_profile("Royal Shield Gold Rim", offhand, (-.13, -.32, -.13), outline, .14, PALETTE["gold"], .032)
    inner_points = [(x * .82, z * .82) for x, z in outline]
    inner = domed_profile("Royal Shield Blue Field", offhand, (-.13, -.405, -.13), inner_points, .085, .075, main, .020)
    crest = [(-.22,.28),(-.08,.12),(-.08,-.12),(-.21,-.02),(-.29,-.14),(-.18,-.25),(0,-.39),(.18,-.25),(.29,-.14),(.21,-.02),(.08,-.12),(.08,.12),(.22,.28),(0,.40)]
    prism_profile("Shield Crown Crest", offhand, (-.13, -.570, -.13), crest, .036, PALETTE["gold"], .010)
    prism_profile("Shield Gem", offhand, (-.13, -.600, -.34), [(0,.09),(.08,0),(0,-.09),(-.08,0)], .025, PALETTE["orb_blue"], .006)
    return rim, inner


def knight_sword(weapon):
    # A tapered broad blade uses a profile mesh to keep a proper silhouette in every rotation.
    blade = [(-.095,.42),(-.13,.20),(-.105,-.38),(0,-.66),(.105,-.38),(.13,.20),(.095,.42)]
    obj = prism_profile("Knight Broad Sword", weapon, (.10, -.04, -.45), blade, .085, PALETTE["steel"], .012)
    obj.rotation_euler.y = math.radians(-10)
    prism_profile("Sword Fuller", weapon, (.10, -.095, -.45), [(-.022,.30),(-.028,-.30),(0,-.48),(.028,-.30),(.022,.30)], .018, PALETTE["blue_light"], .004)
    cube("Sword Guard", weapon, (.10, -.03, -.015), (.25, .055, .045), PALETTE["gold"], .020)
    cylinder("Sword Grip", weapon, (.10, -.03, .145), .052, .22, PALETTE["leather"], 12)
    sphere("Sword Pommel", weapon, (.10, -.03, .28), (.095,.085,.095), PALETTE["gold"])


def build_tank_polished():
    root, body, head, ll, lr, al, ar, weapon, offhand = polished_root("Royal Crown Shield Knight")
    add_knight_torso(body, ll, lr, al, ar, PALETTE["blue"], CAPE_BLUE)
    polished_face(head)
    knight_helmet(head, PALETTE["blue"])
    shield_profile(offhand)
    knight_sword(weapon)
    return root


def build_hammer_polished():
    root, body, head, ll, lr, al, ar, weapon, offhand = polished_root("Coral Siege Hammer")
    add_knight_torso(body, ll, lr, al, ar, PALETTE["red"], CAPE_RED, PALETTE["gold"])
    polished_face(head)
    knight_helmet(head, PALETTE["red"])
    # Keep the crown insignia but replace the shield with the oversized red siege hammer.
    cylinder("Hammer Shaft", weapon, (.04, -.01, -.35), .060, 1.10, PALETTE["leather"], 14)
    hammer = prism_profile("Coral Hammer Head", weapon, (.04, -.02, -.88), [(-.34,.19),(-.39,.05),(-.31,-.17),(.31,-.17),(.39,.05),(.34,.19)], .38, PALETTE["red"], .055)
    cube("Hammer Gold Band", weapon, (.04,-.23,-.88), (.30,.035,.075), PALETTE["gold"], .018)
    prism_profile("Hammer Crown Mark", weapon, (.04,-.245,-.88), [(-.13,.05),(-.04,.12),(0,.04),(.04,.12),(.13,.05),(.10,-.06),(-.10,-.06)], .018, PALETTE["gold"], .006)
    return root


def ranger_torso(body, ll, lr, al, ar):
    add_plated_legs(ll, lr, PALETTE["leather"], PALETTE["gold"])
    cape_pts = [(-.48,1.42),(-.56,.64),(-.44,.20),(0,.08),(.44,.20),(.56,.64),(.48,1.42),(0,1.58)]
    prism_profile("Ranger Hood Cape", body, (0,.25,0), cape_pts, .09, MINT_DARK, .032)
    sphere("Ranger Tunic", body, (0,0,1.0), (.47,.31,.57), PALETTE["mint"])
    prism_profile("Ranger Leather Vest", body, (0,-.325,1.03), [(-.36,.30),(-.41,-.24),(-.26,-.46),(0,-.49),(.26,-.46),(.41,-.24),(.36,.30),(0,.39)], .07, MINT_DARK, .025)
    cube("Ranger Belt", body, (0,-.015,.72), (.40,.22,.07), PALETTE["leather"], .032)
    prism_profile("Ranger Buckle", body, (0,-.255,.72), [(-.1,.1),(-.1,-.1),(.1,-.1),(.1,.1)], .04, PALETTE["gold"], .008)
    for side, arm in (("L",al),("R",ar)):
        cylinder("Ranger Sleeve "+side, arm, (0,0,-.16), .145,.43, PALETTE["mint"],16)
        sphere("Ranger Glove "+side, arm, (0,-.05,-.42), (.16,.16,.16), PALETTE["leather"])


def ranger_hood(head):
    sphere("Ranger Hood", head, (0,.03,.18), (.50,.40,.37), PALETTE["mint"])
    # Hood opening frames the face in a warm gold edge.
    arc_tube("Hood Gold Edge", head, (0,0,0), [(-.34,-.435,.17),(-.29,-.50,-.12),(0,-.535,-.28),(.29,-.50,-.12),(.34,-.435,.17)], .034, PALETTE["gold"])
    prism_profile("Hood Crest", head, (0,-.425,.30), [(-.14,.06),(0,.18),(.14,.06),(.08,-.08),(-.08,-.08)], .035, PALETTE["gold"], .009)


def ranger_bow(weapon, offhand):
    # Curved bow with physical limbs/string, a held arrow and a visible rear quiver.
    arc_tube("Ranger Bow", weapon, (.08,-.04,-.37), [(0,0,.38),(.15,-.02,.20),(.18,-.03,0),(.15,-.02,-.20),(0,0,-.38)], .041, PALETTE["gold"], 3)
    arc_tube("Bow String", weapon, (.08,-.075,-.37), [(0,0,.38),(-.075,0,0),(0,0,-.38)], .012, WHITE, 1)
    prism_profile("Drawn Arrow", weapon, (-.08,-.11,-.37), [(-.018,.48),(-.018,-.39),(-.07,-.48),(0,-.60),(.07,-.48),(.018,-.39),(.018,.48)], .025, PALETTE["steel"], .004)
    cylinder("Quiver", offhand, (.27,.19,-.05), .12,.55, PALETTE["leather"],12)
    for x in (-.06,.0,.06):
        prism_profile("Quiver Arrow", offhand, (.27+x,.12,.27), [(-.014,.23),(.014,.23),(.014,-.18),(-.014,-.18)], .014, PALETTE["mint"], .003)


def build_archer_polished():
    root, body, head, ll, lr, al, ar, weapon, offhand = polished_root("Mint Crown Ranger")
    ranger_torso(body,ll,lr,al,ar)
    polished_face(head, PALETTE["mint"])
    ranger_hood(head)
    ranger_bow(weapon,offhand)
    return root


def mage_torso(body, ll, lr, al, ar, main, dark):
    # Wide tiered robe reads independently from a generic character even at tactical scale.
    sphere("Mage Tunic", body, (0,.01,1.03), (.45,.30,.56), main)
    robe = [(-.49,.47),(-.57,.10),(-.62,-.54),(-.42,-.68),(.42,-.68),(.62,-.54),(.57,.10),(.49,.47),(0,.58)]
    prism_profile("Layered Mage Robe", body, (0,-.02,.52), robe, .43, main, .040)
    prism_scaled("Robe Inner Shadow", body, (0,-.235,.50), robe, .73,.82,.045,dark,.018)
    cube("Mage Belt", body, (0,-.19,.77), (.41,.07,.065), PALETTE["leather"], .025)
    prism_profile("Mage Buckle", body, (0,-.275,.77), [(-.09,.09),(-.12,0),(-.09,-.09),(.09,-.09),(.12,0),(.09,.09)], .03, PALETTE["gold"],.008)
    for side, arm in (("L",al),("R",ar)):
        cylinder("Mage Sleeve "+side, arm,(0,0,-.15),.18,.48,main,16)
        sphere("Mage Hand "+side,arm,(0,-.05,-.43),(.15,.15,.15),PALETTE["skin"])


def star_hat(head, main):
    cylinder("Wide Hat Brim", head, (0,.015,.31), .48,.095,main,28)
    cone("Tall Folded Hat", head, (0,.04,.73), .31,.035,.82,main,28)
    arc_tube("Hat Gold Band",head,(0,0,0),[(-.28,-.37,.47),(0,-.44,.43),(.28,-.37,.47)],.040,PALETTE["gold"])
    # A bent tip helps the wizard silhouette avoid the old stiff cone look.
    sphere("Hat Tip",head,(.14,.03,1.12),(.12,.12,.12),main)
    prism_profile("Hat Star",head,(0,-.41,.53),[(0,.16),(.045,.05),(.16,.05),(.07,-.035),(.10,-.15),(0,-.08),(-.10,-.15),(-.07,-.035),(-.16,.05),(-.045,.05)],.030,PALETTE["gold"],.007)


def staff_with_star(weapon, orb, star=True):
    cylinder("Runed Staff",weapon,(.08,-.02,-.40),.052,.92,PALETTE["leather"],16)
    cylinder("Staff Gold Grip",weapon,(.08,-.02,-.17),.072,.12,PALETTE["gold"],16)
    if star:
        pts=[]
        for i in range(10):
            a=math.radians(90+i*36)
            r=.22 if i%2==0 else .095
            pts.append((math.cos(a)*r,math.sin(a)*r))
        prism_profile("Star Staff Focus",weapon,(.08,-.04,.14),pts,.10,PALETTE["gold"],.020)
        sphere("Star Core",weapon,(.08,-.105,.14),(.09,.045,.09),orb)
    else:
        cylinder("Orb Gold Ring",weapon,(.08,-.04,.13),.22,.05,PALETTE["gold"],24,(math.radians(90),0,0))
        sphere("Arcane Orb",weapon,(.08,-.05,.13),(.18,.18,.18),orb)


def build_area_mage_polished():
    root, body, head, ll, lr, al, ar, weapon, offhand = polished_root("Violet Star Powder Mage")
    mage_torso(body,ll,lr,al,ar,PALETTE["purple"],PURPLE_DARK)
    polished_face(head, PALETTE["orb_violet"])
    star_hat(head,PALETTE["purple"])
    staff_with_star(weapon,PALETTE["orb_violet"],True)
    for x,z in ((-.37,.30),(.36,.10),(-.30,.02)):
        prism_profile("Floating Star",offhand,(x,-.18,z),[(0,.07),(.02,.02),(.07,0),(.02,-.02),(0,-.07),(-.02,-.02),(-.07,0),(-.02,.02)],.02,PALETTE["gold"],.004)
    return root


def orb_hood(head):
    sphere("Azure Hood",head,(0,.025,.18),(.51,.41,.39),PALETTE["blue_light"])
    arc_tube("Azure Hood Gold Edge",head,(0,0,0),[(-.35,-.44,.17),(-.29,-.51,-.14),(0,-.55,-.29),(.29,-.51,-.14),(.35,-.44,.17)],.035,PALETTE["gold"])
    prism_profile("Hood Gem",head,(0,-.46,.34),[(0,.12),(.09,0),(0,-.12),(-.09,0)],.035,PALETTE["orb_blue"],.008)
    for x in (-.17,.17):
        arc_tube("Silver Hair",head,(0,0,0),[(x,-.48,.13),(x*.9,-.52,-.08),(x*.8,-.49,-.21)],.030,PALETTE["steel"])


def build_orb_mage_polished():
    root, body, head, ll, lr, al, ar, weapon, offhand = polished_root("Azure Orb Adept")
    mage_torso(body,ll,lr,al,ar,PALETTE["blue_light"],CAPE_BLUE)
    polished_face(head, PALETTE["orb_blue"], PALETTE["steel"])
    orb_hood(head)
    staff_with_star(weapon,PALETTE["orb_blue"],False)
    # Open offhand and a small spell ring communicate a spellcaster's active basic attack.
    sphere("Open Palm",offhand,(0,-.12,-.22),(.19,.075,.18),PALETTE["skin"])
    arc_tube("Orb Spell Ring",offhand,(0,-.22,-.22),[(0,.0,.22),(.20,0,0),(0,0,-.22),(-.20,0,0),(0,0,.22)],.018,PALETTE["orb_blue"],2)
    return root


os.makedirs(OUT, exist_ok=True)
os.makedirs(SOURCE_OUT, exist_ok=True)
export_model("Tank", build_tank_polished)
export_model("Melee", build_hammer_polished)
export_model("Archer", build_archer_polished)
export_model("AreaMage", build_area_mage_polished)
export_model("SingleMage", build_orb_mage_polished)
