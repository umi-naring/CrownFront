import bpy
import math
import os
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FBX = os.path.join(ROOT, "Assets", "Resources", "CrownfrontProduction", "Tank.fbx")
OUT = os.path.join(ROOT, "work", "production-tank-preview.png")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=FBX)

def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.context.scene.render.engine = 'BLENDER_EEVEE'
bpy.context.scene.render.resolution_x = 720
bpy.context.scene.render.resolution_y = 720
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.world.color = (0.04, 0.07, 0.12)

bpy.ops.object.camera_add(location=(0.0, -8.5, 2.8))
camera = bpy.context.object
look_at(camera, (0, 0, 1.05))
bpy.context.scene.camera = camera

for location, energy, color, size in [((-4,-5,7), 1500, (1.0,.78,.52), 4), ((4,-2,4), 900, (.35,.55,1), 3)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.color = color
    light.data.shape = 'DISK'
    light.data.size = size
    look_at(light, (0,0,1))

bpy.ops.mesh.primitive_plane_add(size=14, location=(0,0,0))
floor = bpy.context.object
mat = bpy.data.materials.new('Floor')
mat.diffuse_color = (.055,.08,.11,1)
floor.data.materials.append(mat)

bpy.context.scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('RENDERED', OUT)
