import bpy
import os
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORKSPACE = os.path.abspath(os.path.join(ROOT, ".."))
FBX = os.path.join(WORKSPACE, "tmp", "quaternius-knight", "KnightCharacter.fbx")
OUT = os.path.join(ROOT, "work", "quaternius-knight-preview.png")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=FBX)

for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        low = tuple(round(min(c[i] for c in corners), 3) for i in range(3))
        high = tuple(round(max(c[i] for c in corners), 3) for i in range(3))
        print("MESH", obj.name, "loc", tuple(round(v, 3) for v in obj.location), "scale", tuple(round(v, 3) for v in obj.scale), "dim", tuple(round(v, 3) for v in obj.dimensions), "bounds", low, high, "hide", obj.hide_render, "materials", [m.name if m else None for m in obj.data.materials])

def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
bpy.context.scene.display.shading.light = "STUDIO"
bpy.context.scene.display.shading.color_type = "MATERIAL"
bpy.context.scene.render.resolution_x = 720
bpy.context.scene.render.resolution_y = 720
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.world.color = (.04, .07, .12)

bpy.ops.object.camera_add(location=(4.8, -8.5, 3.8))
camera = bpy.context.object
look_at(camera, (0, 0, 1.0))
bpy.context.scene.camera = camera

for location, energy, color, size in [((-4, -5, 7), 1500, (1.0, .78, .52), 4), ((4, -2, 4), 900, (.35, .55, 1), 3)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.color = color
    light.data.shape = "DISK"
    light.data.size = size
    look_at(light, (0, 0, 1))

bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, 0))
floor = bpy.context.object
mat = bpy.data.materials.new("Floor")
mat.diffuse_color = (.055, .08, .11, 1)
floor.data.materials.append(mat)

bpy.context.scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
