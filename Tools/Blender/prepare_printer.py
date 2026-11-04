"""Prepare the Sketchfab '3D Printer' (Gregsterius, CC BY 4.0) for Unity.

Usage (from repo root):
  blender --background --factory-startup --python Tools/Blender/prepare_printer.py -- \
      SourceAssets/Printer3D/Printer.fbx Assets/Models/Printer3D/Printer3D.fbx

- groups ~80 loose parts into moving assemblies (Frame, Bed, Gantry, PrintHead, Display, cables)
- joins each assembly into one mesh (fewer draw calls on Quest)
- bakes transforms, scales to a real-world height of 0.45 m, puts the root pivot at floor centre
- sets pivots: Bed = bed surface centre, PrintHead = nozzle tip, cables = their fixed end
- flat hierarchy (all parts direct children of Printer3D)
- gives the front screen a soft blue emissive glow
- adds a DisplayAnchor empty at the centre of the front screen (position only)
"""
import bpy, sys, os
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import classify, bbox

src, dst = sys.argv[-2], sys.argv[-1]
TARGET_HEIGHT = 0.45  # metres

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

# drop empty geometry
for o in [o for o in meshes if len(o.data.polygons) == 0]:
    meshes.remove(o); bpy.data.objects.remove(o)

# overall bounds (source units) -> root pivot at bottom centre, uniform scale to target height
mins = Vector((1e9,)*3); maxs = Vector((-1e9,)*3)
for o in meshes:
    mn, mx = bbox(o)
    mins = Vector(map(min, mins, mn)); maxs = Vector(map(max, maxs, mx))
root_pt = Vector(((mins.x+maxs.x)/2, (mins.y+maxs.y)/2, mins.z))
s = TARGET_HEIGHT / (maxs.z - mins.z)
to_out = Matrix.Scale(s, 4) @ Matrix.Translation(-root_pt)
print(f"source bounds {mins} .. {maxs}, scale {s:.5f}")

fallback = bpy.data.materials.get("White metal")

# Screen backlight: dark glass with a soft blue glow. FBX carries EmissiveColor/EmissiveFactor,
# URP's FBX importer maps them to _EmissionColor and enables the _EMISSION keyword
# (which also keeps the emission shader variant in Quest builds).
SCREEN_BASE = (0.02, 0.03, 0.05, 1.0)
SCREEN_EMISSION = (0.25, 0.55, 1.0, 1.0)
SCREEN_STRENGTH = 0.6
screen = bpy.data.materials["Display"].node_tree.nodes["Principled BSDF"]
screen.inputs["Base Color"].default_value = SCREEN_BASE
screen.inputs["Emission Color"].default_value = SCREEN_EMISSION
screen.inputs["Emission Strength"].default_value = SCREEN_STRENGTH
screen.inputs["Roughness"].default_value = 0.1
groups = {}
for o in meshes:
    groups.setdefault(classify(o), []).append(o)       # classify in source space
    if o.data.users > 1: o.data = o.data.copy()          # don't transform shared meshes twice
    if not o.material_slots or not any(sl.material for sl in o.material_slots):
        o.data.materials.clear(); o.data.materials.append(fallback)
    mw = o.matrix_world.copy()
    o.parent = None
    o.data.transform(to_out @ mw)                        # bake everything into vertices
    # (Mesh.transform already fixes face winding for negative scale)
    o.matrix_world = Matrix.Identity(4)

def join(name, objs):
    with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs):
        bpy.ops.object.join()
    o = objs[0]; o.name = o.data.name = name
    return o

def set_origin(o, p):
    o.data.transform(Matrix.Translation(-p)); o.location = p
    bpy.context.view_layer.update()

parts = {g: (join(g, objs) if len(objs) > 1 else objs[0]) for g, objs in groups.items()}
for g, o in parts.items(): o.name = o.data.name = g

def bb(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))

# pivots
mn, mx = bb(parts["Bed"]);       set_origin(parts["Bed"], Vector(((mn.x+mx.x)/2, (mn.y+mx.y)/2, mx.z)))
mn, mx = bb(parts["Gantry"]);    set_origin(parts["Gantry"], (mn+mx)/2)
mn, mx = bb(parts["PrintHead"]); set_origin(parts["PrintHead"], Vector(((mn.x+mx.x)/2, (mn.y+mx.y)/2, mn.z)))  # nozzle tip
mn, mx = bb(parts["ZCable"]);    set_origin(parts["ZCable"], Vector(((mn.x+mx.x)/2, (mn.y+mx.y)/2, mn.z)))      # fixed bottom end
mn, mx = bb(parts["FilamentTube"]); set_origin(parts["FilamentTube"], Vector((mx.x, (mn.y+mx.y)/2, (mn.z+mx.z)/2)))  # fixed motor end

# display anchor: centre + normal of the faces using the 'Display' material
d = parts["Display"]
idx = [i for i, sl in enumerate(d.material_slots) if sl.material and sl.material.name == "Display"][0]
faces = [p for p in d.data.polygons if p.material_index == idx]
area = sum(p.area for p in faces)
centre = sum((p.center * p.area for p in faces), Vector()) / area
normal = sum((p.normal * p.area for p in faces), Vector()).normalized()
set_origin(d, centre)
anchor = bpy.data.objects.new("DisplayAnchor", None); bpy.context.scene.collection.objects.link(anchor)
anchor.location = centre + normal * 0.0005                    # 0.5 mm in front of the screen
# Position only: empty rotations do not survive bake_space_transform reliably.
# Screen normal in Unity space = (-n.x, n.z, -n.y) -> faces +Z (forward), tilted up ~15 deg.
anchor.empty_display_size = 0.02
print("display centre", centre, "normal", normal)

# hierarchy: root at floor centre
root = bpy.data.objects.new("Printer3D", None); bpy.context.scene.collection.objects.link(root)
def parent(child, par):
    bpy.context.view_layer.update()
    w = child.matrix_world.copy(); child.parent = par; child.matrix_world = w
# flat hierarchy on purpose: Blender's "Apply Transform" (bake_space_transform) breaks nested children,
# so the controller script moves PrintHead/FilamentTube together with the Gantry instead
for o in list(parts.values()) + [anchor]: parent(o, root)

bpy.context.view_layer.update()
print("\n=== RESULT (metres) ===")
for o in [root] + list(root.children_recursive):
    tris = sum(len(p.vertices)-2 for p in o.data.polygons) if o.type == 'MESH' else 0
    print(f"{o.name:14} parent={o.parent.name if o.parent else '-':10} loc={tuple(round(v,4) for v in o.location)} tris={tris} mats={[m.name for m in o.data.materials] if o.type=='MESH' else ''}")
mn, mx = bb(parts["Frame"]); print("frame size (m):", tuple(round(v,3) for v in (mx-mn)))
print("bed top z:", round(parts["Bed"].matrix_world.translation.z,4), "nozzle z:", round(parts["PrintHead"].matrix_world.translation.z,4))

bpy.ops.export_scene.fbx(filepath=dst, object_types={'MESH', 'EMPTY'}, use_selection=False,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False,
    bake_anim=False, path_mode='STRIP', use_custom_props=False)
print("exported", dst)
