"""Measures the printer's screen in Unity's local space of Printer3D (used for screenNormal in
PrinterFeedback / PrinterTouchscreen): front-facing faces of the 'Display' material.

Usage: blender --background --factory-startup --python measure_screen.py -- Assets/Models/Printer3D/Printer3D.fbx

The FBX root's local space is already Y-up; Unity mirrors X when importing, so Unity = (-x, y, z).
"""
import bpy, sys
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-1])
root = bpy.data.objects["Printer3D"]; d = bpy.data.objects["Display"]
M = root.matrix_world.inverted() @ d.matrix_world; R = M.to_3x3()
U = lambda v: Vector((-v.x, v.y, v.z))
polys = [p for p in d.data.polygons if d.material_slots[p.material_index].material.name == "Display"]
fwd = Vector((0, 0.3, 1)).normalized()
front = [p for p in polys if (R @ p.normal).normalized().dot(fwd) > 0.9]
n = Vector(); c = Vector(); A = 0
for p in front:
    n += (R @ p.normal).normalized() * p.area; c += (M @ p.center) * p.area; A += p.area
n.normalize(); c /= A
nu, cu = U(n), U(c)
print("FRONT faces", len(front), "normalU", tuple(round(x,4) for x in nu), "centreU", tuple(round(x,4) for x in cu))
t = Vector((1,0,0)); b = n.cross(t).normalized()
vs = [U(M @ d.data.vertices[i].co) for p in front for i in p.vertices]
xs = [(v - cu).x for v in vs]; ys = [(v - cu).dot(U(b)) for v in vs]
print("extent x", round(min(xs),4), round(max(xs),4), "y", round(min(ys),4), round(max(ys),4))
# anything of the display mesh in front of that plane? (bezel)
allv = [U(M @ v.co) for v in d.data.vertices]
ahead = max((v - cu).dot(nu) for v in allv)
print("max height of display mesh above screen plane", round(ahead,4))
a = bpy.data.objects["DisplayAnchor"]; au = U((root.matrix_world.inverted() @ a.matrix_world).translation)
print("anchorU", tuple(round(x,4) for x in au), "anchor offset along n", round((au - cu).dot(nu),4))
# frame geometry in front of the screen plane near the screen (could hide text)
f = bpy.data.objects["Frame"]; MF = root.matrix_world.inverted() @ f.matrix_world
near = [U(MF @ v.co) for v in f.data.vertices]
near = [v for v in near if abs(v.x - cu.x) < 0.05 and abs(v.y - cu.y) < 0.04 and abs(v.z - cu.z) < 0.04]
print("frame verts near screen", len(near), "max above plane", round(max(((v - cu).dot(nu) for v in near), default=0),4))
