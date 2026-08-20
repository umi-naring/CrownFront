"""Build every defender, including augment recruits, on the production rig.

The shared skeleton guarantees a complete animation vocabulary.  Each role receives an
authored equipment silhouette and its own material family; augment recruits are first-class
models rather than recoloured references to a base sprite.
"""

import bpy
import importlib.util
import math
import os


PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE_PATH = os.path.join(os.path.dirname(__file__), "build_production_tank.py")
spec = importlib.util.spec_from_file_location("crownfront_tank_source", BASE_PATH)
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


ROLE_SPECS = {
    "Tank":       ((.018, .095, .48), (.03, .26, .82), (.006, .035, .19)),
    "Melee":      ((.58, .025, .012), (.90, .12, .035), (.28, .008, .005)),
    "Archer":     ((.008, .38, .30), (.04, .68, .48), (.004, .16, .13)),
    "AreaMage":   ((.31, .025, .62), (.62, .10, .90), (.11, .004, .25)),
    "SingleMage": ((.015, .28, .72), (.06, .52, .96), (.005, .10, .31)),
    "Bombardier": ((.52, .11, .018), (.90, .31, .035), (.22, .025, .006)),
    "Lancer":     ((.01, .36, .22), (.05, .70, .40), (.003, .14, .09)),
    "Druid":      ((.12, .34, .07), (.43, .70, .16), (.04, .13, .025)),
    "Musketeer":  ((.22, .24, .31), (.60, .42, .08), (.08, .07, .05)),
    "Oracle":     ((.19, .14, .50), (.42, .36, .86), (.055, .035, .20)),
}


def role_materials(role):
    primary, accent, cape = ROLE_SPECS[role]
    return (
        base.mat(role + " primary", primary, .22, .28),
        base.mat(role + " accent", accent, .26, .24),
        base.mat(role + " cape", cape, .06, .46),
    )


def armature():
    return bpy.data.objects.get("CrownfrontArmature")


def remove_matching(prefixes):
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith(prefixes):
            bpy.data.objects.remove(obj, do_unlink=True)


def recolor_role(primary, accent, cape):
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj.name.startswith("COL_"):
            continue
        for index, material in enumerate(obj.data.materials):
            if material is None:
                continue
            if material.name.startswith(("Royal enamel", "Royal edge light")):
                obj.data.materials[index] = primary if "edge" not in material.name else accent
            elif material.name.startswith("Deep blue cape"):
                obj.data.materials[index] = cape


def parent_new(obj, bone):
    base.bone_parent(obj, armature(), bone)
    return obj


def curve_tube(name, points, radius, material, bone):
    curve = bpy.data.curves.new(name + " Curve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return parent_new(obj, bone)


def star_points(cx, cz, outer, inner, points=5):
    result = []
    for i in range(points * 2):
        angle = math.radians(90 + i * 180 / points)
        radius = outer if i % 2 == 0 else inner
        result.append((cx + math.cos(angle) * radius, cz + math.sin(angle) * radius))
    return result


def crescent_points(cx, cz, outer=.22, inner=.15, segments=16):
    points = []
    for i in range(segments + 1):
        angle = math.radians(70 + 220 * i / segments)
        points.append((cx + math.cos(angle) * outer, cz + math.sin(angle) * outer))
    for i in range(segments, -1, -1):
        angle = math.radians(70 + 220 * i / segments)
        points.append((cx + .075 + math.cos(angle) * inner, cz + math.sin(angle) * inner))
    return points


def add_hammer(primary, accent):
    head = [(.39, .30), (.28, .08), (.34, -.20), (1.00, -.20),
            (1.08, .08), (.98, .34), (.70, .42)]
    parent_new(base.extruded_profile("Hammer Head", head, -.20, .31, primary, .045), "Weapon")
    parent_new(base.extruded_profile("Hammer Crown", star_points(.68, .05, .15, .07, 4),
                                     -.375, .025, base.GOLD, .007), "Weapon")
    parent_new(base.loft("Hammer Haft", [
        (.69, -.02, .24, .055, .052), (.69, -.02, .99, .060, .055)
    ], base.LEATHER, 16), "Weapon")


def add_bow(accent):
    curve_tube("Ranger Bow", [(.40, -.10, 1.18), (.64, -.13, .86), (.68, -.14, .50),
                              (.60, -.13, .18), (.37, -.10, -.05)], .040, accent, "Weapon")
    curve_tube("Bow String", [(.40, -.15, 1.18), (.29, -.19, .56), (.37, -.15, -.05)],
               .010, base.EYE_WHITE, "Weapon")
    arrow = [( .25, 1.13), (.27, .20), (.20, .08), (.29, -.08),
             (.38, .08), (.31, .20), (.31, 1.13)]
    parent_new(base.extruded_profile("Drawn Arrow", arrow, -.21, .027, base.STEEL, .004), "Weapon")
    quiver = base.loft("Arrow Quiver", [
        (-.58, .16, .45, .12, .11), (-.58, .16, 1.04, .15, .13)
    ], base.LEATHER, 16)
    parent_new(quiver, "Offhand")


def add_hat(primary, accent, star=False):
    brim = []
    for i in range(28):
        angle = math.tau * i / 28
        brim.append((math.cos(angle) * .55, 2.04 + math.sin(angle) * .16))
    parent_new(base.extruded_profile("Mage Hat Brim", brim, -.10, .42, primary, .018), "Head")
    hat = [(-.34, 2.06), (-.22, 2.50), (-.05, 2.88), (.15, 2.64),
           (.30, 2.13), (.22, 2.03), (-.22, 2.03)]
    parent_new(base.extruded_profile("Mage Hat Crown", hat, -.02, .34, primary, .032), "Head")
    mark = star_points(0, 2.35, .14, .06) if star else [(0, 2.48), (.11, 2.34), (0, 2.20), (-.11, 2.34)]
    parent_new(base.extruded_profile("Hat Focus", mark, -.205, .030, accent, .006), "Head")


def add_staff(primary, focus_points, focus_material):
    parent_new(base.loft("Runed Staff", [
        (.67, -.04, .10, .048, .045), (.64, -.04, 1.12, .058, .052)
    ], base.LEATHER, 16), "Weapon")
    parent_new(base.extruded_profile("Staff Focus", focus_points, -.12, .12, focus_material, .012), "Weapon")
    parent_new(base.loft("Staff Collar", [
        (.64, -.04, 1.02, .09, .08), (.64, -.04, 1.17, .09, .08)
    ], base.GOLD, 16), "Weapon")


def add_cannon(primary, accent):
    barrel = base.loft("Clockwork Cannon", [
        (.62, -.08, .12, .20, .18), (.62, -.08, .54, .24, .22),
        (.62, -.08, .88, .19, .18), (.62, -.08, 1.05, .27, .24)
    ], primary, 24)
    parent_new(barrel, "Weapon")
    gear = star_points(.61, .56, .26, .17, 8)
    parent_new(base.extruded_profile("Cannon Gear", gear, -.33, .055, accent, .010), "Weapon")
    parent_new(base.extruded_profile("Cannon Crown Seal", star_points(.61, .77, .10, .045, 5),
                                     -.367, .025, base.GOLD, .005), "Weapon")


def add_lance(accent):
    parent_new(base.loft("Emerald Lance Shaft", [
        (.68, -.05, -.22, .040, .038), (.68, -.05, 1.40, .052, .048)
    ], accent, 14), "Weapon")
    tip = [(.68, 1.82), (.51, 1.40), (.63, 1.31), (.73, 1.31), (.85, 1.40)]
    parent_new(base.extruded_profile("Emerald Lance Tip", tip, -.12, .10, base.STEEL, .010), "Weapon")
    pennant = [(.72, 1.30), (1.08, 1.14), (.72, .99)]
    parent_new(base.extruded_profile("Lance Pennant", pennant, -.11, .045, accent, .006), "Weapon")


def add_musket(accent):
    stock = [(.27, .21), (.30, .10), (.72, .16), (.88, .33), (.76, .46), (.51, .38)]
    parent_new(base.extruded_profile("Musket Stock", stock, -.19, .16, base.LEATHER, .016), "Weapon")
    barrel = [(.45, .42), (.46, .34), (1.22, .34), (1.30, .38), (1.22, .46)]
    parent_new(base.extruded_profile("Brass Musket Barrel", barrel, -.22, .10, accent, .010), "Weapon")
    parent_new(base.extruded_profile("Musket Sight", [(.94,.48),(1.01,.48),(1.01,.58),(.94,.58)],
                                     -.23, .035, base.GOLD, .004), "Weapon")


def add_druid_kit(primary, accent):
    curve_tube("Living Branch Staff", [(.66, -.03, .05), (.58, -.03, .46), (.66, -.03, .90),
                                       (.54, -.03, 1.27)], .055, base.LEATHER, "Weapon")
    for index, (x, z, angle) in enumerate(((.46,1.28,-24),(.65,1.42,18),(.80,1.25,42))):
        petal = [(x, z+.13), (x+.09, z), (x, z-.13), (x-.09, z)]
        obj = base.extruded_profile("Druid Petal " + str(index), petal, -.12, .045,
                                    accent if index == 1 else primary, .006)
        parent_new(obj, "Weapon")


def add_oracle_kit(accent):
    add_staff(accent, crescent_points(.64, 1.38), base.JEWEL)
    for index, (x, z) in enumerate(((-.55,1.15),(-.68,.94),(-.45,.78))):
        parent_new(base.extruded_profile("Oracle Rune " + str(index),
                                         [(x,z+.08),(x+.07,z),(x,z-.08),(x-.07,z)],
                                         -.30, .025, accent, .004), "Offhand")


def add_hero_parts(accent):
    crown = [(-.22, 2.27), (-.20, 2.48), (-.11, 2.61), (0, 2.43),
             (.11, 2.61), (.20, 2.48), (.22, 2.27), (.15, 2.20), (-.15, 2.20)]
    hero_crown = base.extruded_profile("HERO_Command_Crest", crown, -.485, .040, accent, .008)
    parent_new(hero_crown, "Head")
    for side, x in (("L",-.53),("R",.53)):
        wing = [(x,1.35),(x + (-.20 if side=="L" else .20),1.48),
                (x + (-.25 if side=="L" else .25),1.23),(x,1.10)]
        parent_new(base.extruded_profile("HERO_Pauldron_Wing." + side, wing,
                                         -.11, .075, accent, .009), "Arm."+side)


def equip(role, primary, accent):
    if role == "Tank":
        return
    remove_matching(("Royal Shield", "Shield Crown", "Shield Jewel",
                     "Royal Broad Sword", "Sword Fuller", "Sword Guard", "Sword Grip", "Sword Pommel",
                     "COL_Shield"))
    if role in ("Melee",):
        add_hammer(primary, accent)
    elif role == "Archer":
        add_bow(accent)
    elif role == "AreaMage":
        add_hat(primary, accent, True)
        add_staff(primary, star_points(.64, 1.36, .23, .10), accent)
    elif role == "SingleMage":
        add_hat(primary, accent, False)
        add_staff(primary, [( .64,1.58),(.82,1.36),(.64,1.14),(.46,1.36)], base.JEWEL)
    elif role == "Bombardier":
        add_cannon(primary, accent)
    elif role == "Lancer":
        add_lance(accent)
    elif role == "Druid":
        add_druid_kit(primary, accent)
    elif role == "Musketeer":
        add_musket(accent)
    elif role == "Oracle":
        add_oracle_kit(accent)


def export(role):
    fbx_path = os.path.join(PROJECT, "Assets", "Resources", "CrownfrontProduction", role + ".fbx")
    blend_path = os.path.join(PROJECT, "Assets", "ArtSource~", "CrownfrontProduction", role + ".blend")
    bpy.context.scene["unit_role"] = role
    bpy.context.scene["animation_set"] = "Idle,Walk,BasicAttack,Guard,Skill,Hurt,Ultimate,Death"
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, check_existing=False)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "OTHER"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("PRODUCTION_ROLE", role, fbx_path)


for role in ROLE_SPECS:
    base.build()
    primary, accent, cape = role_materials(role)
    recolor_role(primary, accent, cape)
    equip(role, primary, accent)
    add_hero_parts(accent)
    export(role)

# Every base.build() resets the Blender scene and writes the unadorned tank as an intermediate
# source asset.  Rebuild Tank once at the end so that intermediate export cannot overwrite its
# authored hero crest and pauldrons.
base.build()
primary, accent, cape = role_materials("Tank")
recolor_role(primary, accent, cape)
add_hero_parts(accent)
export("Tank")
