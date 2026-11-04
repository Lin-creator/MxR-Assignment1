# Part classification for the Sketchfab "3D Printer" model (source units, Z-up).
from mathutils import Vector

# Explicit lists for the parts that can't be told apart by position alone
HEAD = ["Cube.005","Cube.007","Cube.017","Cube.020","Cube.021","Cube.022","Cube.023","Cube.024",
        "Cylinder.018","Cylinder.032","Cylinder.033","Cylinder.034","Cylinder.035","Cylinder.037","Sphere.009"]
SPECIAL = {"Cube.008": "Display", "BezierCurve.001": "ZCable", "BezierCurve": "FilamentTube"}

def bbox(o):
    ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return (Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws))),
            Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws))))

def classify(o):
    if o.name in SPECIAL: return SPECIAL[o.name]
    if o.name in HEAD: return "PrintHead"
    mn, mx = bbox(o)
    c = (mn + mx) / 2
    if mn.z >= 21.5: return "Gantry"                       # X-axis beam at the top (moves in Z)
    if mn.z >= 5.3 and mx.z <= 8.3 and abs(c.y) < 3 and (mx.x - mn.x) < 15 and (mx.y - mn.y) < 15:
        return "Bed"                                        # bed plate + carriage (moves in Y)
    return "Frame"                                          # static
