"""Modelling kit for AURITE FRONT models built in Blender from Python.

Conventions (Blender side):
  * 1 Blender unit = 1 map cell. Z is up, +Y is the model's forward direction, origin at ground level.
  * Every object belongs to a named *part* (Body, Turret, Rotor, Extra). Parts other than Body have a pivot.
  * Colours come from flat materials. The material named TEAM is exported as a marker colour that the game
    replaces with the owner's team colour at runtime.

Export format (.bytes, read by RTS.Client.ImportedModels): see write_rtsm().
"""
import math
import struct

import bpy  # noqa: F401  (must load before bmesh)
import bmesh
import bpy
from mathutils import Matrix, Vector

TEAM_MARKER = (1.0, 0.0, 1.0)

# Animation kinds (must match RTS.Client.AnimKind) and parents (RTS.Client.AnimParent).
WHEEL, LEG_L, LEG_R, ARM_L, ARM_R, AIM, RECOIL, SPIN, SPIN_X, BLINK, ROCK, WAVE, TREAD_BOTTOM, TREAD_TOP, BOB = range(1, 16)
PARENTS = {"Body": 0, "Turret": 1, "Rotor": 2, "Extra": 3}
CORE = ("Body", "Turret", "Rotor", "Extra")

# --------------------------------------------------------------------------------------------- scene helpers

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


_mats = {}


def mat(name, rgb, rough=0.6, metal=0.0, emit=0.0):
    """Flat material. rgb is in sRGB (0..1) like the colours used in the game code."""
    key = name
    if key in _mats and _mats[key].name in bpy.data.materials:
        return _mats[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    lin = tuple(srgb_to_lin(c) for c in rgb)
    bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = (*lin, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    m["rts_rgb"] = list(rgb)
    m["rts_emit"] = emit
    m.diffuse_color = (*lin, 1.0)
    _mats[key] = m
    return m


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


class Palette:
    """Faction colour set. Colours mirror ModelLibrary.Style in the game code."""

    def __init__(self, faction):
        self.faction = faction
        self.ca = faction != "id"
        if faction == "ca":
            hull, dark, accent = (0.66, 0.69, 0.72), (0.36, 0.39, 0.43), (0.85, 0.87, 0.90)
        elif faction == "ne":
            hull, dark, accent = (0.70, 0.64, 0.55), (0.40, 0.33, 0.27), (0.62, 0.25, 0.20)
        else:
            hull, dark, accent = (0.40, 0.39, 0.29), (0.24, 0.22, 0.18), (0.55, 0.32, 0.18)
        _mats.clear()
        self.hull = mat("hull", hull, 0.55, 0.15)
        self.hull_lt = mat("hull_lt", tuple(min(1, c * 1.1) for c in hull), 0.55, 0.15)
        self.hull_dk = mat("hull_dk", tuple(c * 0.85 for c in hull), 0.6, 0.15)
        self.dark = mat("dark", dark, 0.6, 0.2)
        self.accent = mat("accent", accent, 0.5, 0.3)
        self.metal = mat("metal", (0.28, 0.28, 0.30), 0.4, 0.8)
        self.metal_dk = mat("metal_dk", (0.17, 0.17, 0.18), 0.5, 0.7)
        self.rubber = mat("rubber", (0.12, 0.12, 0.12), 0.9)
        self.glass = mat("glass", (0.25, 0.45, 0.60), 0.15, 0.3)
        self.light = mat("light", (1.0, 0.85, 0.55), 0.3, 0.0, emit=2.0)
        self.team = mat("TEAM", (0.80, 0.15, 0.12), 0.5, 0.1)
        self.skin = mat("skin", (0.85, 0.68, 0.52), 0.7)
        self.concrete = mat("concrete", (0.50, 0.52, 0.54) if self.ca else (0.36, 0.34, 0.30), 0.9)
        self.concrete_dk = mat("concrete_dk", (0.40, 0.41, 0.42) if self.ca else (0.28, 0.27, 0.24), 0.9)
        self.yellow = mat("yellow", (0.90, 0.72, 0.15), 0.5)
        self.white = mat("white", (0.92, 0.92, 0.9), 0.6)
        if self.ca:
            self.uniform = mat("uniform", (0.42, 0.48, 0.42), 0.8)
        else:
            self.uniform = mat("uniform", (0.38, 0.33, 0.24), 0.8)
        self.uniform_dk = mat("uniform_dk", tuple(c * 0.75 for c in self.uniform["rts_rgb"]), 0.8)
        self.leather = mat("leather", (0.30, 0.22, 0.14), 0.8)


# --------------------------------------------------------------------------------------------- primitives
# Every primitive returns the created object. 'part' tags it for export. Transforms are applied immediately so
# that bevel widths stay in world units.

def _new_obj(name, bm, material, part):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if material is not None:
        me.materials.append(material)
    ob["part"] = part
    return ob


def _bevel(ob, width, segments=2, angle=40):
    if width <= 0:
        return ob
    mod = ob.modifiers.new("bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(angle)
    mod.harden_normals = False
    return ob


def box(size, loc=(0, 0, 0), material=None, part="Body", bevel=0.012, seg=2, rot=(0, 0, 0), name="box"):
    """Box with its *centre* at loc."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = _new_obj(name, bm, material, part)
    ob.rotation_euler = [math.radians(a) for a in rot]
    ob.location = loc
    return _bevel(ob, min(bevel, min(size) * 0.45), seg)


def poly_prism(profile, length, loc=(0, 0, 0), material=None, part="Body", axis="X", bevel=0.01, seg=2, name="prism", smooth=False):
    """Extrudes a 2D profile. axis='X': profile is in (Y, Z) and extruded along X, centred.
    axis='Y': profile in (X, Z), extruded along Y. axis='Z': profile in (X, Y), extruded up from loc."""
    bm = bmesh.new()

    def P3(a, b, t):
        if axis == "X":
            return (-length / 2 + t * length, a, b)
        if axis == "Y":
            return (a, -length / 2 + t * length, b)
        return (a, b, t * length)

    v0 = [bm.verts.new(P3(a, b, 0)) for (a, b) in profile]
    v1 = [bm.verts.new(P3(a, b, 1)) for (a, b) in profile]
    n = len(v0)
    bm.faces.new(v0)
    bm.faces.new(list(reversed(v1)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((v0[i], v1[i], v1[j], v0[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _new_obj(name, bm, material, part)
    ob.location = loc
    if smooth:
        _smooth(ob)
        ob.data.polygons[0].use_smooth = False
        ob.data.polygons[1].use_smooth = False
        # the flat base of an arch should not blend into the curve
        mod = ob.modifiers.new("split", "EDGE_SPLIT")
        mod.split_angle = math.radians(50)
    return _bevel(ob, bevel, seg)


def hull(bottom, top, z0, z1, loc=(0, 0, 0), material=None, part="Body", bevel=0.012, seg=2, name="hull"):
    """Loft between two outlines (lists of (x, y) of equal length, counter-clockwise seen from above)."""
    bm = bmesh.new()
    vb = [bm.verts.new((x, y, z0)) for (x, y) in bottom]
    vt = [bm.verts.new((x, y, z1)) for (x, y) in top]
    n = len(vb)
    bm.faces.new(list(reversed(vb)))
    bm.faces.new(vt)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((vb[i], vb[j], vt[j], vt[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _new_obj(name, bm, material, part)
    ob.location = loc
    return _bevel(ob, bevel, seg)


def octo(w, l, cut):
    """Rectangle w x l (x, y) with chamfered corners, counter-clockwise."""
    hw, hl = w / 2, l / 2
    c = min(cut, hw * 0.9, hl * 0.9)
    return [(-hw + c, -hl), (hw - c, -hl), (hw, -hl + c), (hw, hl - c), (hw - c, hl), (-hw + c, hl), (-hw, hl - c), (-hw, -hl + c)]


def cyl(r, depth, loc=(0, 0, 0), material=None, part="Body", axis="Z", seg=12, r2=None, bevel=0.006, cap=True, name="cyl"):
    """Cylinder (or cone frustum with r2) centred at loc along axis."""
    bm = bmesh.new()
    r2 = r if r2 is None else r2
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r, radius2=r2, depth=depth)
    ob = _new_obj(name, bm, material, part)
    if axis == "X":
        ob.rotation_euler = (0, math.radians(90), 0)
    elif axis == "Y":
        ob.rotation_euler = (math.radians(-90), 0, 0)
    ob.location = loc
    _smooth(ob)
    return _bevel(ob, min(bevel, r * 0.3), 1, 50)


def sphere(r, loc=(0, 0, 0), material=None, part="Body", seg=12, rings=8, scale=(1, 1, 1), name="sph", half=False):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    if half:
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-4], context="VERTS")
        edge = [e for e in bm.edges if e.is_boundary]
        if edge:
            bmesh.ops.holes_fill(bm, edges=edge)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    ob = _new_obj(name, bm, material, part)
    ob.location = loc
    _smooth(ob)
    return ob


def capsule(a, b, r, material=None, part="Body", seg=8, name="limb"):
    """Rounded limb from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    L = d.length
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=6, radius=r)
    for v in bm.verts:
        if v.co.z > 0:
            v.co.z += L
    ob = _new_obj(name, bm, material, part)
    ob.location = a
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    _smooth(ob)
    return ob


def _smooth(ob):
    """Smooth-shades curved faces only; flat caps (large n-gons) stay flat so they do not shade dark."""
    for p in ob.data.polygons:
        p.use_smooth = len(p.vertices) <= 4


def mirror_x(ob):
    """Duplicates an object mirrored across X=0."""
    c = ob.copy()
    c.data = ob.data.copy()
    bpy.context.scene.collection.objects.link(c)
    c.location.x = -ob.location.x
    c.scale.x = -ob.scale.x
    if ob.rotation_mode == "QUATERNION":
        q = ob.rotation_quaternion
        c.rotation_quaternion = (q.w, q.x, -q.y, -q.z)
    else:
        c.rotation_euler = (ob.rotation_euler.x, -ob.rotation_euler.y, -ob.rotation_euler.z)
    return c


def array(fn, count, step):
    """Calls fn(i, offset_vector) count times."""
    out = []
    for i in range(count):
        out.append(fn(i, Vector(step) * i))
    return out


def boolean_cut(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    mod.solver = "EXACT"
    cutter.hide_render = True
    cutter["part"] = "__cutter"
    return target


# --------------------------------------------------------------------------------------------- export

class ModelInfo:
    def __init__(self, key, faction):
        self.key = key
        self.faction = faction
        self.pivots = {}          # part -> (x, y, z) in Blender space
        self.height = 0.5
        self.muzzle_forward = 0.5
        self.muzzle_height = 0.3
        self.rotor_vertical = True
        self.extra_spins = False
        self.infantry = False
        self.floating = False     # aircraft and ships: parts may sit below Z = 0
        self.footprint = (0, 0)   # buildings: cells
        self.anim = {}            # part name -> (parent, kind, param, phase)
        self._n = 0

    def animate(self, kind, param, pivot, parent="Body", phase=0.0, name=None):
        """Registers an animated part and returns its name; pass it as part= to the primitives that make it."""
        self._n += 1
        name = name or f"a{self._n}"
        self.anim[name] = (parent, kind, float(param), float(phase))
        self.pivots[name] = tuple(pivot)
        return name


def _to_unity(v):
    # Blender (x right, y forward, z up) -> Unity (x right, y up, z forward)
    return (v[0], v[2], v[1])


def gather(info):
    """Collects evaluated, triangulated geometry per part, relative to each part's pivot."""
    deps = bpy.context.evaluated_depsgraph_get()
    parts = {}
    for ob in bpy.context.scene.objects:
        if ob.type != "MESH":
            continue
        part = ob.get("part", "Body")
        if part.startswith("__"):
            continue
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        mw = ob.matrix_world
        nm = mw.to_3x3().inverted_safe().transposed()
        flip = mw.determinant() < 0
        piv = Vector(info.pivots.get(part, (0, 0, 0)))
        cn = me.corner_normals
        dst = parts.setdefault(part, {"v": [], "n": [], "c": [], "t": []})
        for tri in me.loop_triangles:
            matl = me.materials[tri.material_index] if me.materials else None
            if matl is None:
                rgb, emit = (0.7, 0.7, 0.7), 0.0
            elif matl.name.startswith("TEAM"):
                rgb, emit = TEAM_MARKER, 0.0
            else:
                rgb, emit = tuple(matl["rts_rgb"]), float(matl.get("rts_emit", 0.0))
            loops = list(tri.loops)
            if flip:
                loops.reverse()
            for li in loops:
                vi = me.loops[li].vertex_index
                p = mw @ me.vertices[vi].co - piv
                n = (nm @ cn[li].vector).normalized()
                if flip:
                    pass
                dst["t"].append(len(dst["v"]))
                dst["v"].append(_to_unity(p))
                dst["n"].append(_to_unity(n))
                a = 1.0 if emit <= 0 else max(0.0, 1.0 - min(1.0, emit / 4.0))
                dst["c"].append((rgb[0], rgb[1], rgb[2], a))
        ev.to_mesh_clear()
    for p in parts.values():
        _weld(p)
    return parts


def _weld(p):
    """Merges identical vertices (same position, normal and colour) to shrink the file."""
    idx = {}
    v2, n2, c2, remap = [], [], [], []
    for i in range(len(p["v"])):
        k = (tuple(round(x, 5) for x in p["v"][i]), tuple(round(x, 3) for x in p["n"][i]), tuple(round(x, 3) for x in p["c"][i]))
        j = idx.get(k)
        if j is None:
            j = len(v2)
            idx[k] = j
            v2.append(p["v"][i]); n2.append(p["n"][i]); c2.append(p["c"][i])
        remap.append(j)
    p["t"] = [remap[t] for t in p["t"]]
    p["v"], p["n"], p["c"] = v2, n2, c2


def write_rtsm(path, info, parts):
    """Binary layout (little endian), version 2:
       'RTSM' u8 version
       f32 height, f32 muzzleForward, f32 muzzleHeight
       u8 flags (1 rotorVertical, 2 extraSpins, 4 infantry, 8 floating), u8 footprintW, u8 footprintH
       u8 partCount, then per part:
         u8 nameLen, name (ascii), u8 parent, u8 animKind, f32 param, f32 phase,
         f32x3 pivot (Unity model space), i32 vertexCount,
         vertexCount x (f32x3 pos relative to pivot, f32x3 normal, u8x4 colour sRGB; alpha < 255 = glow),
         i32 indexCount, indices (u16, or i32 when vertexCount > 65535)
    """
    flags = (1 if info.rotor_vertical else 0) | (2 if info.extra_spins else 0) | (4 if info.infantry else 0) | (8 if info.floating else 0)
    order = [k for k in CORE if k in parts] + sorted(k for k in parts if k not in CORE)
    if len(order) > 255:
        raise ValueError("too many parts")
    with open(path, "wb") as f:
        f.write(b"RTSM")
        f.write(struct.pack("<B", 2))
        f.write(struct.pack("<fff", info.height, info.muzzle_forward, info.muzzle_height))
        f.write(struct.pack("<BBB", flags, info.footprint[0], info.footprint[1]))
        f.write(struct.pack("<B", len(order)))
        total = 0
        for name in order:
            p = parts[name]
            parent, kind, param, phase = info.anim.get(name, ("Body", 0, 0.0, 0.0))
            if name not in CORE and kind == 0:
                raise ValueError(f"part {name} has no animation")
            nb = name.encode("ascii")
            f.write(struct.pack("<B", len(nb)))
            f.write(nb)
            f.write(struct.pack("<BBff", PARENTS[parent], kind, param, phase))
            f.write(struct.pack("<fff", *_to_unity(info.pivots.get(name, (0, 0, 0)))))
            f.write(struct.pack("<i", len(p["v"])))
            for i in range(len(p["v"])):
                f.write(struct.pack("<fff", *p["v"][i]))
                f.write(struct.pack("<fff", *p["n"][i]))
                c = p["c"][i]
                f.write(struct.pack("<BBBB", *[max(0, min(255, int(round(x * 255)))) for x in c]))
            f.write(struct.pack("<i", len(p["t"])))
            fmt = "<H" if len(p["v"]) <= 65535 else "<i"
            for t in p["t"]:
                f.write(struct.pack(fmt, t))
            total += len(p["t"]) // 3
    return total


# --------------------------------------------------------------------------------------------- preview render

def render_preview(path, team_rgb=(0.80, 0.15, 0.12), size=512, frame=None, samples=24, from_front=True):
    scn = bpy.context.scene
    for m in bpy.data.materials:
        if m.name.startswith("TEAM"):
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*[srgb_to_lin(c) for c in team_rgb], 1)
    objs = [o for o in scn.objects if o.type == "MESH" and not str(o.get("part", "")).startswith("__")]
    deps = bpy.context.evaluated_depsgraph_get()
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.evaluated_get(deps).bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    centre = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.2)
    # ground
    bpy.ops.mesh.primitive_plane_add(size=radius * 12, location=(centre.x, centre.y, min(lo.z, 0.0) - 0.002))
    g = bpy.context.active_object
    g["part"] = "__preview"
    gm = bpy.data.materials.new("__ground"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.2, 0.11, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1
    g.data.materials.append(gm)
    # camera: same angles as the game (50 deg pitch, 45 deg yaw), orthographic
    cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = radius * 1.75
    cam = bpy.data.objects.new("cam", cam_data); scn.collection.objects.link(cam); cam["part"] = "__preview"
    pitch, yaw = math.radians(35), math.radians(35 if from_front else 215)
    dirv = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = centre + dirv * 20
    cam.rotation_euler = (-dirv).to_track_quat("-Z", "Y").to_euler()
    scn.camera = cam
    sun_data = bpy.data.lights.new("sun", "SUN"); sun_data.energy = 3.2; sun_data.angle = math.radians(4)
    sun = bpy.data.objects.new("sun", sun_data); scn.collection.objects.link(sun); sun["part"] = "__preview"
    sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(-35))
    world = bpy.data.worlds.new("w"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.5, 0.6, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    scn.world = world
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.samples = samples
    scn.cycles.use_denoising = True
    scn.render.resolution_x = size; scn.render.resolution_y = size
    scn.render.film_transparent = False
    scn.view_settings.view_transform = "Standard"
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in list(scn.objects):
        if str(o.get("part", "")) == "__preview":
            bpy.data.objects.remove(o, do_unlink=True)


def pose(info, amount=1.0):
    """Moves animated parts to a sample pose (for previews only): legs mid-stride, arms swung, guns recoiled,
    wheels and spinners turned. Approximates the in-game motion so pivots can be checked by eye."""
    import math as _m
    from mathutils import Matrix as _M
    bpy.context.view_layer.update()   # matrix_world is stale until the scene is evaluated
    for ob in bpy.context.scene.objects:
        name = ob.get("part", "Body")
        if name not in info.anim:
            continue
        parent, kind, param, phase = info.anim[name]
        p = Vector(info.pivots[name])
        if kind in (LEG_L, LEG_R, ARM_L, ARM_R):
            sign = 1 if kind in (LEG_L, ARM_R) else -1
            R = _M.Rotation(_m.radians(param * sign * amount), 4, "X")
        elif kind == AIM:
            R = _M.Rotation(_m.radians(param * amount), 4, "X")
        elif kind in (WHEEL, SPIN_X):
            R = _M.Rotation(_m.radians(40 * amount), 4, "X")
        elif kind == SPIN:
            R = _M.Rotation(_m.radians(40 * amount), 4, "Z")
        elif kind == WAVE:
            R = _M.Rotation(_m.radians(param * amount), 4, "Z")
        elif kind == ROCK:
            R = _M.Rotation(_m.radians(param * amount), 4, "X")
        elif kind == RECOIL:
            ob.matrix_world = _M.Translation((0, -param * amount, 0)) @ ob.matrix_world
            continue
        elif kind == BOB:
            ob.matrix_world = _M.Translation((0, 0, param * amount)) @ ob.matrix_world
            continue
        else:
            continue
        ob.matrix_world = _M.Translation(p) @ R @ _M.Translation(-p) @ ob.matrix_world
