"""Map scenery pieces (trees, rocks, ore, bridges...). One Body part each, exported to Resources/Models/Scenery.

Marker colours the game replaces per map theme:
  LEAF  (0, 255, 0)   foliage            -> theme leaf colour with per-instance variation
  ROCK  (0, 0, 255)   stone              -> theme rock colour
Vertex alpha below 1 on scenery means 'sways in the wind' (1 - alpha = sway weight, rising towards the top)."""
import math
import random

import bmesh
import bpy
from mathutils import Vector

from kit import _bevel, _new_obj, _smooth, box, capsule, cyl, mat, poly_prism, sphere

SCENERY = {}

LEAF = (0.0, 1.0, 0.0)
ROCK = (0.0, 0.0, 1.0)


def scenery(name):
    def reg(fn):
        SCENERY[name] = fn
        return fn
    return reg


def leaf_mat(shade=1.0, sway=0.0):
    m = mat("LEAF_%d_%d" % (int(shade * 100), int(sway * 100)), LEAF, 0.8)
    m["rts_rgb"] = [0.0, shade, 0.0]   # green channel carries the shade, red/blue stay 0 for the marker
    m["rts_sway"] = sway
    return m


def rock_mat(shade=1.0):
    m = mat("ROCK_%d" % int(shade * 100), ROCK, 0.9)
    m["rts_rgb"] = [0.0, 0.0, shade]
    return m


def wood(shade=1.0, sway=0.0):
    m = mat("bark_%d_%d" % (int(shade * 100), int(sway * 100)), (0.36 * shade, 0.25 * shade, 0.15 * shade), 0.9)
    m["rts_sway"] = sway
    return m


def blob(r, loc, m, seed, rough=0.25, seg=10, rings=7, scale=(1, 1, 1), flat_bottom=False):
    """Lumpy sphere (foliage clumps, boulders)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r)
    for v in bm.verts:
        n = v.co.normalized()
        k = 1 + (rnd.random() - 0.5) * 2 * rough
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) * k
        if flat_bottom and v.co.z < -r * 0.2 * scale[2]:
            v.co.z = -r * 0.2 * scale[2]
    ob = _new_obj("blob", bm, m, "Body")
    ob.location = loc
    return ob


# ============================================================================================ trees

@scenery("tree_broad")
def tree_broad(seed):
    rnd = random.Random(seed)
    h = 0.32 + rnd.random() * 0.08
    cyl(0.045, h, (0, 0, h / 2), wood(1.0, 0.0), seg=7, r2=0.03, bevel=0, name="trunk")
    for k in range(3):
        a = k * math.tau / 3 + rnd.random()
        capsule((0, 0, h * 0.7), (math.cos(a) * 0.12, math.sin(a) * 0.12, h + 0.05), 0.015, wood(0.9, 0.3), seg=5, name="branch")
    for k in range(4):
        a = k * math.tau / 4 + rnd.random() * 0.8
        d = 0.0 if k == 0 else 0.13
        z = h + 0.12 + (0.1 if k == 0 else rnd.random() * 0.06)
        blob(0.17 if k == 0 else 0.13, (math.cos(a) * d, math.sin(a) * d, z), leaf_mat(0.85 + 0.25 * rnd.random(), 0.8 + 0.2 * (k == 0)),
             seed + k, rough=0.18, scale=(1, 1, 0.85))


@scenery("tree_pine")
def tree_pine(seed):
    rnd = random.Random(seed)
    h = 0.75 + rnd.random() * 0.2
    cyl(0.035, 0.22, (0, 0, 0.11), wood(0.9, 0.0), seg=6, bevel=0, name="trunk")
    tiers = 4
    for k in range(tiers):
        z0 = 0.12 + k * h * 0.19
        r = 0.27 * (1 - k / (tiers + 0.6))
        sway = 0.3 + 0.7 * k / (tiers - 1)
        c = cyl(r, h * 0.3, (0, 0, z0 + h * 0.15), leaf_mat(0.8 + 0.08 * k, sway), seg=8, r2=0.0, bevel=0, name="tier")
        for v in c.data.vertices:
            if v.co.z < 0:
                ang = math.atan2(v.co.y, v.co.x)
                v.co.z -= 0.02 * math.sin(ang * 4 + seed)


@scenery("tree_palm")
def tree_palm(seed):
    rnd = random.Random(seed)
    pts = [Vector((0, 0, 0))]
    lean = rnd.random() * 0.12
    for k in range(1, 6):
        pts.append(Vector((lean * (k / 5) ** 2, 0, k * 0.13)))
    for a, b in zip(pts, pts[1:]):
        capsule(tuple(a), tuple(b), 0.03 - 0.003 * pts.index(a), wood(1.1, 0.4 * pts.index(a) / 5), seg=6, name="trunk_seg")
    top = pts[-1]
    for k in range(7):
        ang = k * math.tau / 7 + rnd.random() * 0.3
        tip = top + Vector((math.cos(ang) * 0.32, math.sin(ang) * 0.32, -0.1 - rnd.random() * 0.06))
        mid = (top + tip) / 2 + Vector((0, 0, 0.07))
        for a, b in ((top, mid), (mid, tip)):
            d = b - a
            side = Vector((-math.sin(ang), math.cos(ang), 0)) * 0.05
            bm = bmesh.new()
            vs = [bm.verts.new(a + side * 0.4), bm.verts.new(b + side * 0.15), bm.verts.new(b - side * 0.15), bm.verts.new(a - side * 0.4)]
            bm.faces.new(vs)
            _new_obj("frond", bm, leaf_mat(0.9, 1.0), "Body")
    blob(0.05, tuple(top + Vector((0, 0, -0.03))), wood(0.8, 1.0), seed, rough=0.1)


@scenery("tree_dead")
def tree_dead(seed):
    rnd = random.Random(seed)
    h = 0.5 + rnd.random() * 0.15
    capsule((0, 0, 0), (0.02, 0, h), 0.03, wood(0.75, 0.0), seg=6, name="trunk")
    for k in range(4):
        a = rnd.random() * math.tau
        z = h * (0.45 + 0.15 * k)
        capsule((0.01, 0, z), (math.cos(a) * 0.15, math.sin(a) * 0.15, z + 0.12), 0.012, wood(0.7, 0.6), seg=5, name="branch")


@scenery("bush")
def bush(seed):
    rnd = random.Random(seed)
    for k in range(3):
        a = k * math.tau / 3 + rnd.random()
        blob(0.09 + rnd.random() * 0.03, (math.cos(a) * 0.07, math.sin(a) * 0.07, 0.07), leaf_mat(0.75 + 0.2 * rnd.random(), 0.5), seed + k,
             rough=0.2, scale=(1, 1, 0.8))


@scenery("grass_tuft")
def grass_tuft(seed):
    rnd = random.Random(seed)
    for k in range(7):
        a = rnd.random() * math.tau
        r = rnd.random() * 0.06
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        tip = base + Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, 0.09 + rnd.random() * 0.05))
        side = Vector((-math.sin(a), math.cos(a), 0)) * 0.012
        bm = bmesh.new()
        bm.faces.new((bm.verts.new(base + side), bm.verts.new(base - side), bm.verts.new(tip)))
        _new_obj("blade", bm, leaf_mat(0.95 + 0.15 * rnd.random(), 1.0), "Body")


@scenery("cactus")
def cactus(seed):
    rnd = random.Random(seed)
    g = mat("cactus", (0.35, 0.5, 0.25), 0.7)
    h = 0.35 + rnd.random() * 0.1
    capsule((0, 0, 0), (0, 0, h), 0.045, g, seg=8, name="stem")
    for s in (-1, 1):
        z = h * (0.4 + rnd.random() * 0.2)
        capsule((0, 0, z), (s * 0.09, 0, z), 0.03, g, seg=6, name="arm")
        capsule((s * 0.09, 0, z), (s * 0.09, 0, z + 0.12), 0.03, g, seg=6, name="arm_up")


# ============================================================================================ stone

def rock_shape(seed, r, scale, shade, subdiv=2):
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    for v in bm.verts:
        k = 0.85 + rnd.random() * 0.3
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], max(v.co.z, -r * 0.3) * scale[2])) * k
    ob = _new_obj("rock", bm, rock_mat(shade), "Body")
    return ob


@scenery("rock")
def rock(seed):
    rnd = random.Random(seed)
    rock_shape(seed, 0.2, (1.2, 1.0, 0.75), 0.9 + 0.2 * rnd.random())
    if rnd.random() > 0.4:
        o = rock_shape(seed + 7, 0.09, (1, 1, 0.8), 0.8)
        o.location = (0.2, 0.08, 0)


@scenery("boulder_pile")
def boulder_pile(seed):
    rnd = random.Random(seed)
    for k in range(4):
        a = k * math.tau / 4 + rnd.random()
        o = rock_shape(seed + k, 0.1 + rnd.random() * 0.06, (1, 1, 0.8), 0.8 + 0.3 * rnd.random())
        o.location = (math.cos(a) * 0.15, math.sin(a) * 0.15, 0)


@scenery("mountain")
def mountain(seed):
    rnd = random.Random(seed)
    o = rock_shape(seed, 0.55, (1.1, 1.0, 1.6), 1.0, subdiv=3)
    for k in range(3):
        a = k * math.tau / 3 + rnd.random()
        o = rock_shape(seed + 10 + k, 0.3, (1, 1, 1.2), 0.85 + 0.2 * rnd.random())
        o.location = (math.cos(a) * 0.35, math.sin(a) * 0.35, 0)


@scenery("cliff_rock")
def cliff_rock(seed):
    rnd = random.Random(seed)
    for k in range(3):
        o = rock_shape(seed + k, 0.22, (1.3, 0.8, 1.3), 0.8 + 0.25 * rnd.random())
        o.location = ((k - 1) * 0.25, rnd.random() * 0.1, 0)
        o.rotation_euler = (0, 0, rnd.random())


@scenery("rubble")
def rubble(seed):
    rnd = random.Random(seed)
    conc = mat("concrete_rubble", (0.5, 0.48, 0.45), 0.9)
    rust = mat("rust", (0.45, 0.25, 0.15), 0.8, 0.4)
    for k in range(6):
        s = 0.05 + rnd.random() * 0.08
        box((s * 1.6, s, s * 0.7), (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), s * 0.3), conc if k % 3 else rust, bevel=0.01,
            rot=(rnd.uniform(-20, 20), rnd.uniform(-20, 20), rnd.uniform(0, 180)), name="chunk")
    box((0.02, 0.4, 0.02), (0.05, 0, 0.06), rust, bevel=0, rot=(0, 20, 30), name="rebar")


# ============================================================================================ ore

@scenery("ore_gold")
def ore_gold(seed):
    rnd = random.Random(seed)
    gold = mat("ore_gold", (0.85, 0.66, 0.22), 0.35, 0.6)
    dark = mat("ore_matrix", (0.45, 0.36, 0.22), 0.8)
    o = rock_shape(seed, 0.09, (1.3, 1.1, 0.6), 0.7)
    o.data.materials[0] = dark
    for k in range(4):
        a = rnd.random() * math.tau
        r = rnd.random() * 0.08
        blob(0.035 + rnd.random() * 0.02, (math.cos(a) * r, math.sin(a) * r, 0.04 + rnd.random() * 0.03), gold, seed + k, rough=0.3)


@scenery("ore_aurite")
def ore_aurite(seed):
    rnd = random.Random(seed)
    crystal = mat("aurite", (0.35, 0.85, 0.95), 0.1, 0.2, emit=1.2)
    base = mat("aurite_base", (0.25, 0.3, 0.35), 0.8)
    o = rock_shape(seed, 0.08, (1.2, 1.2, 0.5), 0.6)
    o.data.materials[0] = base
    for k in range(4 + int(rnd.random() * 2)):
        tilt = (rnd.uniform(-30, 30), rnd.uniform(-30, 30), rnd.uniform(0, 360))
        L = 0.12 + rnd.random() * 0.12
        c = cyl(0.025 + rnd.random() * 0.015, L, (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), L * 0.45), crystal, seg=6, r2=0.004,
                bevel=0, name="crystal")
        c.rotation_euler = tuple(math.radians(t) for t in tilt)


@scenery("ore_vent")
def ore_vent(seed):
    m = mat("vent_rock", (0.3, 0.28, 0.26), 0.9)
    cyl(0.35, 0.18, (0, 0, 0.09), m, seg=14, r2=0.24, name="vent_cone")
    cyl(0.2, 0.05, (0, 0, 0.17), mat("vent_glow", (0.95, 0.7, 0.25), 0.4, 0.0, emit=1.5), seg=14, bevel=0, name="vent_core")
    for k in range(5):
        a = k * math.tau / 5
        o = rock_shape(seed + k, 0.07, (1, 1, 0.8), 0.7)
        o.location = (math.cos(a) * 0.33, math.sin(a) * 0.33, 0)


# ============================================================================================ bridge (one cell, deck along X)

@scenery("bridge_deck")
def bridge_deck(seed):
    plank = mat("plank", (0.45, 0.36, 0.26), 0.9)
    beam = mat("beam", (0.32, 0.25, 0.18), 0.9)
    steel = mat("bridge_steel", (0.4, 0.42, 0.44), 0.5, 0.6)
    box((1.02, 0.9, 0.06), (0, 0, -0.03), beam, bevel=0.005, name="deck_base")
    for k in range(8):
        box((0.12, 0.88, 0.025), (-0.44 + k * 0.126, 0, 0.012), plank, bevel=0, name="plank")
    for s in (-1, 1):
        box((1.02, 0.06, 0.08), (0, s * 0.46, -0.04), steel, bevel=0.006, name="girder")
        box((1.02, 0.03, 0.03), (0, s * 0.46, 0.15), steel, bevel=0.004, name="rail")
        for x in (-0.38, 0.0, 0.38):
            box((0.03, 0.03, 0.15), (x, s * 0.46, 0.07), steel, bevel=0.004, name="post")


@scenery("bridge_pier")
def bridge_pier(seed):
    stone = mat("pier_stone", (0.45, 0.43, 0.4), 0.9)
    box((0.22, 0.65, 1.0), (0, 0, -0.5), stone, bevel=0.02, name="pier")
    box((0.28, 0.7, 0.06), (0, 0, -0.06), stone, bevel=0.01, name="pier_cap")


VARIANTS = {"tree_broad": 3, "tree_pine": 3, "tree_palm": 2, "tree_dead": 2, "bush": 2, "grass_tuft": 2, "cactus": 2, "rock": 3, "boulder_pile": 2,
            "mountain": 3, "cliff_rock": 2, "rubble": 2, "ore_gold": 3, "ore_aurite": 3, "ore_vent": 1, "bridge_deck": 1, "bridge_pier": 1}
