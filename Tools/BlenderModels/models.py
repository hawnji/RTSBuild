"""Model definitions. Each builder takes (P: Palette, info: ModelInfo) and creates objects in the current scene.
Register builders in MODELS with the game's model key (UnitDef.Model / BuildingDef.Model or role name)."""
import math

from mathutils import Vector

from kit import (array, box, capsule, cyl, hull, mirror_x, octo, poly_prism, sphere)

MODELS = {}


def model(key, footprint=None):
    def reg(fn):
        MODELS[key] = (fn, footprint)
        return fn
    return reg


# ============================================================================================ shared vehicle parts

def track_unit(P, x, length, tw, h, wheels=5, skirt=True, wheel_r=None):
    """One track assembly centred at x. length along Y, track width tw, track height h."""
    L = length / 2
    prof = [(-L, h * 0.38), (-L + h * 0.32, 0.0), (L - h * 0.32, 0.0), (L + h * 0.04, h * 0.42),
            (L - h * 0.1, h * 0.82), (-L + h * 0.12, h * 0.82)]
    poly_prism(prof, tw, loc=(x, 0, 0), material=P.rubber, axis="X", bevel=0.01, name="track")
    # tread grousers along the bottom
    n = int(length / 0.045)
    for i in range(n):
        y = -L + h * 0.32 + (length - h * 0.64) * (i + 0.5) / n
        box((tw * 1.02, 0.014, 0.01), (x, y, 0.004), P.metal_dk, bevel=0.002, name="grouser")
    wr = wheel_r or h * 0.3
    side = 1 if x > 0 else -1
    for i in range(wheels):
        y = -L * 0.72 + (L * 1.44) * i / max(1, wheels - 1)
        cyl(wr, tw * 0.5, (x + side * tw * 0.3, y, wr + 0.012), P.metal, axis="X", seg=14, name="wheel")
        cyl(wr * 0.45, tw * 0.52, (x + side * tw * 0.32, y, wr + 0.012), P.metal_dk, axis="X", seg=10, name="hub")
    # drive sprocket (front) and idler (rear)
    cyl(h * 0.24, tw * 0.6, (x + side * tw * 0.22, L - h * 0.1, h * 0.5), P.metal_dk, axis="X", seg=10, name="sprocket")
    cyl(h * 0.22, tw * 0.6, (x + side * tw * 0.22, -L + h * 0.12, h * 0.46), P.metal_dk, axis="X", seg=10, name="idler")
    if skirt:
        box((0.02, length * 0.86, h * 0.42), (x + side * tw * 0.52, 0.02, h * 0.62), P.hull_dk, bevel=0.006, name="skirt")


def rivet_row(P, start, end, n, r=0.008, axis_normal="Z", material=None):
    s, e = Vector(start), Vector(end)
    for i in range(n):
        p = s.lerp(e, (i + 0.5) / n)
        cyl(r, r * 0.8, tuple(p), material or P.hull_dk, axis=axis_normal, seg=6, bevel=0, name="rivet")


def barrel(P, x, y0, length, r, z, part="Turret", brake=True, sleeve=True):
    cyl(r, length, (x, y0 + length / 2, z), P.metal, axis="Y", seg=14, part=part, name="barrel")
    if sleeve:
        for k in (0.25, 0.55):
            cyl(r * 1.25, length * 0.07, (x, y0 + length * k, z), P.metal_dk, axis="Y", seg=14, part=part, name="sleeve")
    if brake:
        cyl(r * 1.45, length * 0.09, (x, y0 + length * 0.955, z), P.metal_dk, axis="Y", seg=14, part=part, name="brake")
        box((r * 3.6, length * 0.05, r * 0.8), (x, y0 + length * 0.955, z), P.metal_dk, part=part, bevel=0.003, name="brake_ports")


def antenna(P, x, y, z, h, part="Turret"):
    cyl(0.0045, h, (x, y, z + h / 2), P.metal_dk, part=part, seg=5, bevel=0, name="antenna")
    cyl(0.012, 0.02, (x, y, z + 0.01), P.dark, part=part, seg=8, name="antenna_base")


def headlight(P, x, y, z):
    cyl(0.016, 0.012, (x, y, z), P.light, axis="Y", seg=10, bevel=0, name="headlight")
    cyl(0.02, 0.01, (x, y - 0.008, z), P.metal_dk, axis="Y", seg=10, bevel=0, name="headlight_rim")


# ============================================================================================ main battle tank

@model("tank_mbt")
def tank_mbt(P, info):
    if P.ca:
        L, W = 0.95, 0.62
        tw = 0.15
        for s in (-1, 1):
            track_unit(P, s * (W / 2 - tw / 2), L * 0.96, tw, 0.2, wheels=6, skirt=True)
        # lower hull between the tracks and upper hull overhanging them
        hull(octo(W - 2 * tw + 0.04, L * 0.84, 0.06), octo(W - 2 * tw + 0.04, L * 0.9, 0.06), 0.05, 0.16, material=P.hull_dk, name="lower_hull")
        top_out = [(-W * 0.47, -L * 0.47), (W * 0.47, -L * 0.47), (W * 0.48, L * 0.26), (W * 0.36, L * 0.44), (-W * 0.36, L * 0.44), (-W * 0.48, L * 0.26)]
        top_in = [(-W * 0.45, -L * 0.45), (W * 0.45, -L * 0.45), (W * 0.45, L * 0.12), (W * 0.33, L * 0.22), (-W * 0.33, L * 0.22), (-W * 0.45, L * 0.12)]
        hull(top_out, top_in, 0.16, 0.255, material=P.hull, bevel=0.014, name="upper_hull")
        # glacis detail: driver hatch and periscopes
        box((0.11, 0.08, 0.02), (-0.1, L * 0.24, 0.258), P.hull_lt, bevel=0.008, name="driver_hatch")
        for k in (-0.03, 0.0, 0.03):
            box((0.022, 0.012, 0.016), (-0.1 + k, L * 0.29, 0.262), P.glass, bevel=0.002, name="periscope")
        # engine deck grilles
        for i in range(5):
            box((W * 0.5, 0.018, 0.012), (0, -L * 0.30 - i * 0.03, 0.26), P.metal_dk, bevel=0.003, name="grille")
        box((W * 0.66, 0.06, 0.05), (0, -L * 0.46, 0.2), P.hull_dk, bevel=0.01, name="rear_plate")
        for s in (-1, 1):
            headlight(P, s * W * 0.36, L * 0.445, 0.22)
            cyl(0.022, 0.05, (s * W * 0.25, -L * 0.49, 0.17), P.metal_dk, axis="Y", seg=10, name="exhaust")
        # turret (sleek wedge)
        piv = (0, -0.04, 0.255)
        info.pivots["Turret"] = piv
        tb = [(-0.19, -0.24), (0.19, -0.24), (0.215, 0.02), (0.13, 0.19), (-0.13, 0.19), (-0.215, 0.02)]
        tt = [(-0.165, -0.22), (0.165, -0.22), (0.18, 0.02), (0.11, 0.15), (-0.11, 0.15), (-0.18, 0.02)]
        hull(tb, tt, 0.0, 0.13, loc=(0, piv[1], piv[2]), material=P.hull, part="Turret", bevel=0.014, name="turret")
        # turret bustle / stowage basket
        box((0.3, 0.1, 0.07), (0, piv[1] - 0.27, piv[2] + 0.06), P.hull_dk, part="Turret", bevel=0.01, name="bustle")
        for i in range(4):
            box((0.32, 0.006, 0.05), (0, piv[1] - 0.24 - i * 0.03, piv[2] + 0.105), P.metal_dk, part="Turret", bevel=0, name="basket")
        # mantlet and gun
        box((0.13, 0.06, 0.09), (0, piv[1] + 0.2, piv[2] + 0.065), P.hull_dk, part="Turret", bevel=0.012, name="mantlet")
        barrel(P, 0, piv[1] + 0.22, 0.5, 0.024, piv[2] + 0.068)
        # commander cupola, sight, MG
        cyl(0.05, 0.035, (0.08, piv[1] - 0.07, piv[2] + 0.148), P.hull_lt, part="Turret", seg=14, name="cupola")
        cyl(0.04, 0.012, (0.08, piv[1] - 0.07, piv[2] + 0.17), P.hull, part="Turret", seg=14, name="hatch")
        box((0.05, 0.05, 0.05), (-0.09, piv[1] + 0.06, piv[2] + 0.155), P.dark, part="Turret", bevel=0.008, name="sight")
        box((0.04, 0.008, 0.035), (-0.09, piv[1] + 0.086, piv[2] + 0.158), P.glass, part="Turret", bevel=0.002, name="sight_glass")
        cyl(0.007, 0.12, (0.08, piv[1] + 0.0, piv[2] + 0.19), P.metal_dk, axis="Y", part="Turret", seg=6, name="mg")
        antenna(P, -0.13, piv[1] - 0.17, piv[2] + 0.13, 0.22)
        antenna(P, 0.13, piv[1] - 0.17, piv[2] + 0.13, 0.15)
        # smoke dischargers
        for s in (-1, 1):
            for k in range(3):
                cyl(0.011, 0.04, (s * 0.17, piv[1] + 0.06 + k * 0.022, piv[2] + 0.1), P.dark, axis="Y", part="Turret", seg=8, name="smoke")
        # team markings
        for s in (-1, 1):
            box((0.006, 0.14, 0.04), (s * 0.205, piv[1] - 0.05, piv[2] + 0.07), P.team, part="Turret", bevel=0.002, name="team_side")
            box((0.006, L * 0.5, 0.035), (s * (W / 2 + 0.012), 0.03, 0.13), P.team, bevel=0.002, name="team_skirt")
    else:
        L, W = 0.95, 0.64
        tw = 0.16
        for s in (-1, 1):
            track_unit(P, s * (W / 2 - tw / 2), L * 0.96, tw, 0.21, wheels=5, skirt=False, wheel_r=0.075)
            # fenders
            box((tw + 0.02, L * 0.92, 0.014), (s * (W / 2 - tw / 2), 0, 0.185), P.hull_dk, bevel=0.004, name="fender")
        # boxy hull with steep glacis
        prof = [(-L * 0.47, 0.06), (L * 0.36, 0.06), (L * 0.49, 0.14), (L * 0.32, 0.235), (-L * 0.47, 0.235)]
        poly_prism(prof, W - 2 * tw + 0.02, material=P.hull, axis="X", bevel=0.01, name="hull_core")
        box((W * 0.98, L * 0.75, 0.06), (0, -L * 0.08, 0.22), P.hull, bevel=0.01, name="hull_sponson")
        rivet_row(P, (-W * 0.42, L * 0.29, 0.252), (W * 0.42, L * 0.29, 0.252), 9)
        rivet_row(P, (-W * 0.42, -L * 0.44, 0.252), (W * 0.42, -L * 0.44, 0.252), 9)
        for s in (-1, 1):
            rivet_row(P, (s * W * 0.49, -L * 0.42, 0.22), (s * W * 0.49, L * 0.28, 0.22), 8, axis_normal="X")
        # fuel drums at the rear
        for s in (-1, 1):
            cyl(0.045, 0.18, (s * 0.12, -L * 0.52, 0.19), P.metal, axis="X", seg=12, name="fuel_drum")
            cyl(0.047, 0.012, (s * 0.12 + 0.05, -L * 0.52, 0.19), P.dark, axis="X", seg=12, name="drum_band")
        # tool boxes and tow cable
        box((0.08, 0.2, 0.05), (W * 0.42, -L * 0.15, 0.275), P.hull_dk, bevel=0.006, name="toolbox")
        cyl(0.012, L * 0.5, (-W * 0.42, -L * 0.12, 0.262), P.metal_dk, axis="Y", seg=6, name="cable")
        for s in (-1, 1):
            headlight(P, s * W * 0.3, L * 0.4, 0.27)
            cyl(0.02, 0.06, (s * W * 0.2, -L * 0.45, 0.27), P.metal_dk, seg=8, name="exhaust_stack")
        # cast, rounded turret
        piv = (0, -0.03, 0.25)
        info.pivots["Turret"] = piv
        sphere(0.22, (0, piv[1] - 0.01, piv[2] - 0.01), P.hull, part="Turret", seg=20, rings=12, scale=(1.0, 1.1, 0.58), half=True, name="turret_dome")
        cyl(0.205, 0.03, (0, piv[1] - 0.01, piv[2] + 0.005), P.hull_dk, part="Turret", seg=20, name="turret_ring")
        box((0.12, 0.08, 0.08), (0, piv[1] + 0.19, piv[2] + 0.05), P.hull_dk, part="Turret", bevel=0.02, name="mantlet")
        barrel(P, 0, piv[1] + 0.21, 0.5, 0.026, piv[2] + 0.055, sleeve=False)
        cyl(0.045, 0.04, (-0.08, piv[1] - 0.04, piv[2] + 0.13), P.hull_dk, part="Turret", seg=12, name="cupola")
        cyl(0.04, 0.01, (-0.08, piv[1] - 0.04, piv[2] + 0.155), P.hull, part="Turret", seg=12, name="hatch")
        # heavy MG on the loader's hatch
        cyl(0.03, 0.03, (0.08, piv[1] - 0.02, piv[2] + 0.125), P.hull_dk, part="Turret", seg=10, name="loader_hatch")
        cyl(0.009, 0.14, (0.08, piv[1] + 0.05, piv[2] + 0.17), P.metal_dk, axis="Y", part="Turret", seg=6, name="dshk")
        box((0.03, 0.03, 0.04), (0.08, piv[1] - 0.01, piv[2] + 0.155), P.metal_dk, part="Turret", bevel=0.004, name="mg_mount")
        antenna(P, -0.15, piv[1] - 0.12, piv[2] + 0.06, 0.25)
        # searchlight and team band
        cyl(0.03, 0.04, (-0.15, piv[1] + 0.12, piv[2] + 0.09), P.metal_dk, axis="Y", part="Turret", seg=10, name="searchlight")
        cyl(0.024, 0.006, (-0.15, piv[1] + 0.142, piv[2] + 0.09), P.light, axis="Y", part="Turret", seg=10, bevel=0, name="searchlight_lens")
        cyl(0.224, 0.035, (0, piv[1] - 0.01, piv[2] + 0.045), P.team, part="Turret", seg=20, r2=0.205, bevel=0.003, name="team_band")
        box((0.02, L * 0.25, 0.05), (W * 0.49 + 0.01, -L * 0.2, 0.14), P.team, bevel=0.003, name="team_plate")
        box((0.02, L * 0.25, 0.05), (-W * 0.49 - 0.01, -L * 0.2, 0.14), P.team, bevel=0.003, name="team_plate")
    info.height = 0.5
    info.muzzle_forward = 0.72 - 0.04
    info.muzzle_height = 0.32


# ============================================================================================ infantry

def soldier(P, info, weapon="rifle"):
    info.infantry = True
    ca = P.ca
    boot = P.leather if ca else P.metal_dk
    # legs (slight stance)
    for s in (-1, 1):
        capsule((s * 0.035, 0.0 + s * 0.012, 0.035), (s * 0.04, 0.0, 0.16), 0.024, P.uniform_dk, name="leg")
        box((0.04, 0.065, 0.03), (s * 0.035, 0.012 + s * 0.012, 0.016), boot, bevel=0.01, name="boot")
    # torso and vest
    box((0.12, 0.075, 0.13), (0, 0, 0.235), P.uniform, bevel=0.025, seg=3, name="torso")
    box((0.13, 0.085, 0.085), (0, 0.002, 0.245), P.team if ca else P.uniform_dk, bevel=0.02, seg=2, name="vest")
    if not ca:
        box((0.135, 0.02, 0.025), (0, 0.04, 0.21), P.team, bevel=0.006, name="sash")
        box((0.135, 0.09, 0.02), (0, 0, 0.185), P.leather, bevel=0.006, name="belt")
    else:
        for k in (-0.035, 0.0, 0.035):
            box((0.028, 0.02, 0.03), (k, 0.048, 0.225), P.uniform_dk, bevel=0.006, name="pouch")
        box((0.13, 0.088, 0.018), (0, 0, 0.18), P.uniform_dk, bevel=0.005, name="belt")
    # backpack
    box((0.09, 0.05, 0.09), (0, -0.06, 0.25), P.uniform_dk if ca else P.leather, bevel=0.015, name="pack")
    cyl(0.02, 0.09, (0, -0.06, 0.305), P.uniform_dk, axis="X", seg=8, name="bedroll")
    # head and helmet
    capsule((0, 0, 0.3), (0, 0, 0.32), 0.022, P.skin, name="neck")
    sphere(0.034, (0, 0.004, 0.348), P.skin, seg=12, rings=8, scale=(0.95, 1.0, 1.12), name="head")
    if ca:
        sphere(0.042, (0, -0.002, 0.355), P.uniform_dk, seg=14, rings=8, scale=(1.0, 1.08, 0.82), half=True, name="helmet")
        box((0.05, 0.012, 0.012), (0, 0.04, 0.36), P.metal_dk, bevel=0.003, name="goggles")
    else:
        sphere(0.04, (0, 0, 0.357), P.uniform_dk, seg=14, rings=8, scale=(1.0, 1.0, 0.9), half=True, name="helmet")
        cyl(0.055, 0.008, (0, -0.004, 0.357), P.uniform_dk, seg=14, r2=0.045, name="helmet_brim")
    # arms holding the weapon forward
    for s in (-1, 1):
        sh = (s * 0.072, 0.0, 0.28)
        el = (s * 0.075, 0.05, 0.21)
        hand = (s * 0.025 + 0.02, 0.1 + (0.03 if s > 0 else 0), 0.225)
        capsule(sh, el, 0.02, P.uniform, name="upper_arm")
        capsule(el, hand, 0.017, P.uniform, name="forearm")
        sphere(0.016, hand, P.skin, seg=8, rings=6, name="hand")
    # weapon
    gx, gz = 0.02, 0.235
    if weapon == "rifle":
        box((0.018, 0.2, 0.03), (gx, 0.12, gz), P.metal_dk, bevel=0.004, name="rifle_body")
        cyl(0.0055, 0.08, (gx, 0.25, gz + 0.006), P.metal_dk, axis="Y", seg=6, name="rifle_barrel")
        box((0.014, 0.03, 0.045), (gx, 0.1, gz - 0.03), P.metal_dk, bevel=0.003, name="magazine")
        box((0.016, 0.06, 0.035), (gx, 0.0, gz - 0.01), P.leather if not ca else P.dark, bevel=0.006, name="stock")
        if ca:
            box((0.014, 0.05, 0.016), (gx, 0.14, gz + 0.025), P.dark, bevel=0.003, name="scope")
    info.height = 0.42


@model("inf_rifle")
def inf_rifle(P, info):
    soldier(P, info, "rifle")


# ============================================================================================ structures

def foundation(P, W, H, h=0.08):
    box((W * 0.97, H * 0.97, h), (0, 0, h / 2), P.concrete, bevel=0.02, name="foundation")
    # kerb lines
    box((W * 0.9, 0.02, 0.006), (0, -H * 0.44, h + 0.003), P.yellow, bevel=0, name="kerb")


def door(P, x, y, z, w, h, facing=-1):
    box((w + 0.04, 0.03, h + 0.03), (x, y, z + h / 2), P.dark, bevel=0.006, name="door_frame")
    box((w, 0.035, h), (x, y + facing * 0.004, z + h / 2), P.metal, bevel=0.004, name="door")
    for i in range(int(h / 0.04)):
        box((w * 0.96, 0.04, 0.006), (x, y + facing * 0.006, z + 0.02 + i * 0.04), P.metal_dk, bevel=0, name="door_rib")


def window_row(P, x0, x1, y, z, n, w=0.07, h=0.05, facing=-1):
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n
        box((w + 0.016, 0.02, h + 0.016), (x, y, z), P.dark, bevel=0.004, name="win_frame")
        box((w, 0.024, h), (x, y + facing * 0.003, z), P.glass, bevel=0.002, name="window")


def flag(P, x, y, base, h=0.75):
    cyl(0.012, h, (x, y, base + h / 2), P.metal, seg=8, name="flagpole")
    sphere(0.016, (x, y, base + h + 0.01), P.yellow, seg=8, rings=6, name="finial")
    # slightly waved flag from three panels
    for i in range(3):
        box((0.004, 0.08, 0.14), (x + (0.008 if i == 1 else 0), y + 0.05 + i * 0.075, base + h - 0.09), P.team, bevel=0.0, rot=(0, 0, 8 if i == 1 else -6), name="flag")


def sandbags(P, x0, y0, x1, y1, rows=2):
    a, b = Vector((x0, y0, 0)), Vector((x1, y1, 0))
    n = max(2, int((b - a).length / 0.07))
    ang = math.degrees(math.atan2(b.y - a.y, b.x - a.x))
    sand = P.uniform if P.ca else P.leather
    for r in range(rows):
        for i in range(n - (r % 2)):
            t = (i + 0.5 + 0.5 * (r % 2)) / n
            p = a.lerp(b, t)
            box((0.07, 0.045, 0.032), (p.x, p.y, 0.1 + r * 0.03), sand, bevel=0.012, seg=2, rot=(0, 0, ang), name="sandbag")


@model("Barracks", footprint=(2, 2))
def barracks(P, info):
    W = H = 2.0
    foundation(P, W, H)
    base = 0.08
    if P.ca:
        # prefabricated quonset-style hall: half-cylinder roof on a low wall
        L = 1.25
        box((0.95, L, 0.22), (0, 0.05, base + 0.11), P.hull, bevel=0.015, name="hall_wall")
        arch = [(0.48 * math.cos(math.pi * k / 16), 0.48 * math.sin(math.pi * k / 16)) for k in range(17)]
        poly_prism(arch, L, loc=(0, 0.05, base + 0.22), material=P.hull_lt, axis="Y", bevel=0.008, seg=1, name="hall_roof", smooth=True)
        rib = [(0.487 * math.cos(math.pi * k / 16), 0.487 * math.sin(math.pi * k / 16)) for k in range(17)]
        for i in range(7):
            poly_prism(rib, 0.02, loc=(0, 0.05 - L / 2 + 0.06 + i * (L - 0.12) / 6, base + 0.22), material=P.hull_dk, axis="Y", bevel=0, name="roof_rib", smooth=True)
        # front gable
        box((0.92, 0.05, 0.26), (0, 0.05 - L / 2 - 0.02, base + 0.13), P.hull_dk, bevel=0.01, name="gable")
        door(P, 0, 0.05 - L / 2 - 0.05, base, 0.26, 0.24)
        box((0.36, 0.14, 0.02), (0, 0.05 - L / 2 - 0.1, base + 0.3), P.hull_dk, bevel=0.006, name="canopy")
        box((0.04, 0.02, 0.02), (0, 0.05 - L / 2 - 0.06, base + 0.27), P.light, bevel=0.002, name="door_light")
        window_row(P, -0.42, 0.42, 0.05 - L / 2 - 0.05, base + 0.15, 2, w=0.1, facing=-1)
        # side windows along the wall
        for s in (-1, 1):
            for i in range(4):
                y = 0.05 - L / 2 + 0.2 + i * 0.28
                box((0.02, 0.1, 0.06), (s * 0.476, y, base + 0.15), P.dark, bevel=0.003, name="side_win_frame")
                box((0.024, 0.085, 0.045), (s * 0.478, y, base + 0.15), P.glass, bevel=0.002, name="side_win")
        # rooftop AC units and vents
        for k in (-0.25, 0.25):
            box((0.16, 0.16, 0.08), (0.22, 0.05 + k, base + 0.69), P.metal, bevel=0.01, name="ac_unit")
            cyl(0.05, 0.01, (0.22, 0.05 + k, base + 0.735), P.metal_dk, seg=12, name="fan")
        # team stripe along the roof crown
        box((0.08, L * 0.9, 0.01), (-0.18, 0.05, base + 0.69), P.team, bevel=0.002, rot=(0, 20, 0), name="roof_team")
        # yard: sandbag walls, flag, obstacle course posts
        sandbags(P, -0.92, -0.92, -0.45, -0.92)
        sandbags(P, 0.45, -0.92, 0.92, -0.92)
        flag(P, 0.75, 0.75, base)
        for i in range(3):
            box((0.03, 0.03, 0.18), (-0.78, 0.4 + i * 0.16, base + 0.09), P.leather, bevel=0.006, name="post")
        box((0.03, 0.42, 0.025), (-0.78, 0.56, base + 0.17), P.leather, bevel=0.006, name="bar")
        info.height = 1.0
    else:
        # heavy blockhouse with a corner watchtower
        box((1.2, 1.1, 0.42), (-0.15, 0.1, base + 0.21), P.concrete_dk, bevel=0.02, name="block")
        box((1.26, 1.16, 0.06), (-0.15, 0.1, base + 0.44), P.hull_dk, bevel=0.01, name="cornice")
        # pitched metal roof
        prof = [(-0.6, 0.0), (0.6, 0.0), (0.0, 0.22)]
        poly_prism(prof, 1.2, loc=(-0.15, 0.1, base + 0.47), material=P.hull, axis="Y", bevel=0.01, name="roof")
        for i in range(8):
            y = 0.1 - 0.55 + i * 1.1 / 7
            poly_prism([(-0.61, 0.0), (0.61, 0.0), (0.0, 0.225)], 0.015, loc=(-0.15, y, base + 0.47), material=P.hull_dk, axis="Y", bevel=0, name="roof_seam")
        # buttresses
        for x in (-0.72, 0.42):
            for y in (-0.42, 0.62):
                box((0.1, 0.1, 0.5), (x, y, base + 0.25), P.concrete, bevel=0.015, name="buttress")
        door(P, -0.15, 0.1 - 0.56, base, 0.3, 0.28)
        box((0.5, 0.08, 0.08), (-0.15, 0.1 - 0.58, base + 0.34), P.hull_dk, bevel=0.01, name="lintel")
        # slit windows
        for x in (-0.55, 0.25):
            box((0.16, 0.03, 0.035), (x, 0.1 - 0.555, base + 0.3), P.dark, bevel=0.004, name="slit")
        # watchtower at the front right corner
        tx, ty = 0.68, -0.58
        for sx in (-1, 1):
            for sy in (-1, 1):
                cyl(0.018, 0.75, (tx + sx * 0.1, ty + sy * 0.1, base + 0.375), P.metal_dk, seg=6, name="tower_leg")
        box((0.3, 0.3, 0.025), (tx, ty, base + 0.75), P.hull_dk, bevel=0.006, name="tower_floor")
        box((0.3, 0.3, 0.1), (tx, ty, base + 0.8), P.hull, bevel=0.006, name="tower_wall")
        poly_prism([(-0.19, 0.0), (0.19, 0.0), (0.0, 0.1)], 0.34, loc=(tx, ty, base + 0.9), material=P.hull_dk, axis="Y", bevel=0.005, name="tower_roof")
        cyl(0.025, 0.03, (tx, ty - 0.16, base + 0.84), P.light, axis="Y", seg=10, bevel=0, name="tower_lamp")
        # team banners hanging on the front wall
        for x in (-0.55, 0.25):
            box((0.14, 0.012, 0.22), (x, 0.1 - 0.56, base + 0.16 - 0.02), P.team, bevel=0.002, name="banner")
            box((0.16, 0.02, 0.016), (x, 0.1 - 0.56, base + 0.4 - 0.13), P.metal_dk, bevel=0.002, name="banner_rod")
        # chimney and stove pipe
        box((0.12, 0.12, 0.3), (-0.55, 0.45, base + 0.62), P.concrete, bevel=0.01, name="chimney")
        cyl(0.04, 0.08, (-0.55, 0.45, base + 0.8), P.metal_dk, seg=10, name="chimney_cap")
        # barbed wire posts and crates in the yard
        for i in range(5):
            cyl(0.01, 0.12, (-0.9 + i * 0.12, -0.9, base + 0.06), P.metal_dk, seg=5, name="wire_post")
        cyl(0.004, 0.5, (-0.66, -0.9, base + 0.1), P.metal, axis="X", seg=4, bevel=0, name="wire")
        for i, (x, y) in enumerate(((0.75, 0.6), (0.75, 0.4), (0.6, 0.55))):
            box((0.13, 0.13, 0.11), (x, y, base + 0.055 + (0.11 if i == 2 else 0)), P.leather, bevel=0.01, name="crate")
        flag(P, -0.85, 0.55, base, 0.6)
        info.height = 1.0

