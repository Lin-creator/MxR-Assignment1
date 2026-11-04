"""Generates the five printable models for the model selector (Blender 5.2, metres, Z-up).

Usage: blender --background --factory-startup --python make_print_models.py -- <out.fbx>

Every model has its pivot at the bottom centre, fits a 6 x 6 x 6 cm build volume and is a direct
child of the scene root (flat hierarchy, see prepare_printer.py). Exported meshes: Cube, Vase,
Gear, Pawn, Tower.
"""
import bpy, bmesh, math, sys
from mathutils import Matrix

dst = sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)


def new_object(name, bm, smooth=False):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def lathe(name, profile, steps=40):
    """Solid of revolution around Z; profile = [(radius, z)] from the bottom axis point to the top axis point."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / steps), r * math.sin(2 * math.pi * i / steps), z))
                          for i in range(steps)])
    for a, b in zip(rings, rings[1:]):
        for i in range(steps):
            j = (i + 1) % steps
            if len(a) == 1:   bm.faces.new((a[0], b[j], b[i]))
            elif len(b) == 1: bm.faces.new((a[i], a[j], b[0]))
            else:             bm.faces.new((a[i], a[j], b[j], b[i]))
    return new_object(name, bm, smooth=True)


def prism(name, outline, height):
    """Extrudes a star-shaped outline (around the origin) to the given height, caps as triangle fans."""
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, 0)) for x, y in outline]
    hi = [bm.verts.new((x, y, height)) for x, y in outline]
    c0, c1 = bm.verts.new((0, 0, 0)), bm.verts.new((0, 0, height))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bm.faces.new((c0, lo[j], lo[i]))
        bm.faces.new((c1, hi[i], hi[j]))
    return new_object(name, bm)


# 1. Cube – 4 cm, bevelled edges
bpy.ops.mesh.primitive_cube_add(size=0.04)
cube = bpy.context.object; cube.name = cube.data.name = "Cube"
bev = cube.modifiers.new("Bevel", 'BEVEL'); bev.width = 0.004; bev.segments = 3
bpy.ops.object.modifier_apply(modifier=bev.name)
cube.data.transform(Matrix.Translation((0, 0, 0.02)))   # pivot at the bottom

# 2. Vase – 6 cm high
lathe("Vase", [(0, 0), (0.019, 0), (0.023, 0.006), (0.027, 0.02), (0.026, 0.032), (0.018, 0.045),
               (0.013, 0.052), (0.016, 0.058), (0.016, 0.06), (0, 0.06)])

# 3. Gear – 12 teeth, 5.6 cm diameter, 1.5 cm thick
teeth, r_out, r_in = 12, 0.028, 0.0225
gear = []
for t in range(teeth):
    for k, (frac, r) in enumerate([(0.0, r_in), (0.15, r_out), (0.45, r_out), (0.6, r_in)]):
        a = 2 * math.pi * (t + frac) / teeth
        gear.append((r * math.cos(a), r * math.sin(a)))
prism("Gear", gear, 0.015)

# 4. Chess pawn – 5.5 cm high
lathe("Pawn", [(0, 0), (0.02, 0), (0.02, 0.004), (0.017, 0.007), (0.012, 0.009), (0.0085, 0.02),
               (0.007, 0.03), (0.011, 0.032), (0.011, 0.034), (0.007, 0.036), (0.0095, 0.039),
               (0.0105, 0.044), (0.0095, 0.049), (0.0065, 0.053), (0, 0.055)])

# 5. Twisted tower – 3 cm square, 6 cm high, quarter turn
bm = bmesh.new()
layers, side, h = 24, 0.03, 0.06
rings = []
for l in range(layers + 1):
    a = (math.pi / 2) * l / layers
    rings.append([bm.verts.new((side / 2 * math.sqrt(2) * math.cos(a + math.pi / 4 + q * math.pi / 2),
                                side / 2 * math.sqrt(2) * math.sin(a + math.pi / 4 + q * math.pi / 2),
                                h * l / layers)) for q in range(4)])
for a, b in zip(rings, rings[1:]):
    for q in range(4):
        r = (q + 1) % 4
        bm.faces.new((a[q], a[r], b[r], b[q]))
bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
new_object("Tower", bm)

for o in bpy.data.objects:
    mn = min((o.matrix_world @ v.co).z for v in o.data.vertices)
    mx = [max(abs((o.matrix_world @ v.co)[i]) for v in o.data.vertices) for i in range(3)]
    print(f"{o.name}: {len(o.data.polygons)} faces, bottom z={mn:.4f}, half-size x/y={mx[0]:.3f}/{mx[1]:.3f}, top={mx[2]:.3f}")

bpy.ops.export_scene.fbx(filepath=dst, object_types={'MESH'}, use_selection=False,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False,
    bake_anim=False, path_mode='STRIP', use_custom_props=False)
print("exported", dst)
