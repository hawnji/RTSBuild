"""Ground vehicles. Blender space: +Y forward, Z up, 1 unit = 1 cell. Turret pivots are (x, y, z)."""
import math

from kit import (BLINK, BOB, RECOIL, ROCK, SPIN, SPIN_X, WHEEL, box, cyl, hull, octo, poly_prism, sphere)
from models import antenna, barrel, headlight, model, rivet_row, track_unit


# ============================================================================================ chassis helpers

def tyre(P, info, x, y, r, w):
    """Animated road wheel with tread blocks and a hub."""
    part = info.animate(WHEEL, r, (x, y, r))
    cyl(r, w, (x, y, r), P.rubber, axis="X", seg=16, part=part, name="tyre")
    side = 1 if x > 0 else -1
    cyl(r * 0.55, w * 1.04, (x + side * 0.002, y, r), P.metal, axis="X", seg=12, part=part, name="rim")
    cyl(r * 0.2, w * 1.12, (x + side * 0.004, y, r), P.metal_dk, axis="X", seg=8, part=part, name="hub")
    for k in range(5):
        a = k * math.tau / 5
        cyl(r * 0.07, w * 1.08, (x + side * 0.003, y + math.cos(a) * r * 0.38, r + math.sin(a) * r * 0.38), P.metal_dk, axis="X", seg=5,
            part=part, bevel=0, name="lug")
    return part


def wheeled(P, info, L, W, h, axles, ys=None, r=None):
    """Wheels plus a lower frame. Returns wheel radius. ys: optional axle positions."""
    r = r or h * 0.42
    ys = ys or [(-L * 0.36 + L * 0.72 * i / max(1, axles - 1)) for i in range(axles)]
    for y in ys:
        for s in (-1, 1):
            tyre(P, info, s * (W / 2 - 0.045), y, r, 0.07)
    box((W - 0.2, L * 0.9, 0.06), (0, 0, r * 0.9), P.metal_dk, bevel=0.006, name="frame")
    # mudguards over each wheel
    for y in ys:
        for s in (-1, 1):
            poly_prism([(y - r * 1.15, r * 1.25), (y - r * 0.8, r * 2.0), (y + r * 0.8, r * 2.0), (y + r * 1.15, r * 1.25),
                        (y + r * 1.1, r * 1.2), (y - r * 1.1, r * 1.2)], 0.08, loc=(s * (W / 2 - 0.045), 0, 0), material=P.hull_dk,
                       axis="X", bevel=0.004, name="fender")
    return r


def tracked(P, info, L, W, h, wheels=5, skirt=None):
    """Tracks plus a faction-styled hull. Returns the deck height."""
    tw = W * 0.24
    skirt = P.ca if skirt is None else skirt
    for s in (-1, 1):
        track_unit(P, info, s * (W / 2 - tw / 2), L * 0.96, tw, h * 0.78, wheels=wheels, skirt=skirt)
    top = h
    if P.ca:
        hull(octo(W - 2 * tw + 0.04, L * 0.84, 0.06), octo(W - 2 * tw + 0.04, L * 0.9, 0.06), 0.05, h * 0.6, material=P.hull_dk, name="lower_hull")
        out = [(-W * 0.47, -L * 0.47), (W * 0.47, -L * 0.47), (W * 0.48, L * 0.26), (W * 0.36, L * 0.44), (-W * 0.36, L * 0.44), (-W * 0.48, L * 0.26)]
        inn = [(-W * 0.45, -L * 0.45), (W * 0.45, -L * 0.45), (W * 0.45, L * 0.12), (W * 0.33, L * 0.22), (-W * 0.33, L * 0.22), (-W * 0.45, L * 0.12)]
        hull(out, inn, h * 0.6, top, material=P.hull, bevel=0.014, name="upper_hull")
        for i in range(4):
            box((W * 0.5, 0.016, 0.01), (0, -L * 0.32 - i * 0.028, top + 0.004), P.metal_dk, bevel=0.003, name="grille")
        for s in (-1, 1):
            headlight(P, s * W * 0.36, L * 0.445, h * 0.82)
            box((0.006, L * 0.45, 0.03), (s * (W / 2 + 0.012), 0.03, h * 0.5), P.team, bevel=0.002, name="team_skirt")
    else:
        prof = [(-L * 0.47, 0.06), (L * 0.36, 0.06), (L * 0.49, h * 0.55), (L * 0.32, top - 0.015), (-L * 0.47, top - 0.015)]
        poly_prism(prof, W - 2 * tw + 0.02, material=P.hull, axis="X", bevel=0.01, name="hull_core")
        box((W * 0.98, L * 0.75, 0.06), (0, -L * 0.08, top - 0.03), P.hull, bevel=0.01, name="sponson")
        for s in (-1, 1):
            box((tw + 0.02, L * 0.92, 0.014), (s * (W / 2 - tw / 2), 0, h * 0.72), P.hull_dk, bevel=0.004, name="fender")
            rivet_row(P, (s * W * 0.49, -L * 0.42, top - 0.03), (s * W * 0.49, L * 0.28, top - 0.03), 7, axis_normal="X")
            headlight(P, s * W * 0.3, L * 0.4, top + 0.01)
            box((0.02, L * 0.22, 0.045), (s * (W * 0.49 + 0.01), -L * 0.2, h * 0.55), P.team, bevel=0.003, name="team_plate")
        rivet_row(P, (-W * 0.42, L * 0.29, top + 0.002), (W * 0.42, L * 0.29, top + 0.002), 8)
        for s in (-1, 1):
            cyl(0.04, 0.16, (s * 0.11, -L * 0.52, h * 0.75), P.metal, axis="X", seg=12, name="fuel_drum")
    return top


def cab(P, x, y, z, w, l, h, front=True):
    """Truck cab with windscreen."""
    if P.ca:
        hull(octo(w, l, 0.03), octo(w * 0.9, l * 0.7, 0.03), z, z + h, loc=(x, y, 0), material=P.hull, bevel=0.012, name="cab")
        box((w * 0.82, 0.02, h * 0.45), (x, y + l * 0.39, z + h * 0.62), P.glass, bevel=0.004, rot=(-25, 0, 0), name="windscreen")
    else:
        box((w, l, h), (x, y, z + h / 2), P.hull, bevel=0.01, name="cab")
        box((w * 0.8, 0.02, h * 0.35), (x, y + l / 2 + 0.002, z + h * 0.7), P.glass, bevel=0.003, name="windscreen")
        rivet_row(P, (x - w * 0.45, y + l / 2 + 0.004, z + h * 0.45), (x + w * 0.45, y + l / 2 + 0.004, z + h * 0.45), 6, axis_normal="Y")
    for s in (-1, 1):
        box((0.012, l * 0.4, h * 0.3), (x + s * (w / 2 + 0.002), y + l * 0.1, z + h * 0.65), P.glass, bevel=0.003, name="side_window")
        headlight(P, x + s * w * 0.35, y + l / 2 + 0.01, z + h * 0.2)
    cyl(0.004, 0.18, (x - w * 0.4, y - l * 0.3, z + h + 0.09), P.metal_dk, seg=4, bevel=0, name="aerial")


def beacon(P, info, x, y, z, rate=1.5, colour=None):
    """Rotating-light beacon that flashes."""
    from kit import mat
    m = colour or mat("beacon", (1.0, 0.6, 0.1), 0.3, 0.0, emit=3.0)
    cyl(0.018, 0.012, (x, y, z), P.metal_dk, seg=8, name="beacon_base")
    lamp = info.animate(BLINK, rate, (x, y, z + 0.02))
    sphere(0.016, (x, y, z + 0.02), m, seg=8, rings=6, part=lamp, name="beacon_lamp")


def turret_body(P, piv, w, l, h):
    """Faction-styled turret shell. Returns turret top height (absolute)."""
    x, y, z = piv
    if P.ca:
        tb = [(-w * 0.45, -l * 0.55), (w * 0.45, -l * 0.55), (w * 0.5, 0.0), (w * 0.3, l * 0.45), (-w * 0.3, l * 0.45), (-w * 0.5, 0.0)]
        tt = [(-w * 0.38, -l * 0.5), (w * 0.38, -l * 0.5), (w * 0.42, 0.0), (w * 0.25, l * 0.36), (-w * 0.25, l * 0.36), (-w * 0.42, 0.0)]
        hull(tb, tt, 0.0, h, loc=(x, y, z), material=P.hull, part="Turret", bevel=0.012, name="turret")
        for s in (-1, 1):
            box((0.006, l * 0.3, h * 0.3), (x + s * w * 0.49, y - l * 0.1, z + h * 0.55), P.team, part="Turret", bevel=0.002, name="team_side")
    else:
        box((w, l, h), (x, y - l * 0.05, z + h / 2), P.hull, part="Turret", bevel=0.014, name="turret")
        box((w * 1.02, l * 0.25, h * 0.35), (x, y - l * 0.05, z + h * 0.35), P.team, part="Turret", bevel=0.004, name="team_band")
        rivet_row(P, (x - w * 0.4, y + l * 0.45, z + h * 0.75), (x + w * 0.4, y + l * 0.45, z + h * 0.75), 6, axis_normal="Y")
    return z + h


def set_turret(info, piv):
    info.pivots["Turret"] = piv


# ============================================================================================ light vehicles

@model("scout")
def scout(P, info):
    L, W, h = 0.75, 0.45, 0.22
    r = wheeled(P, info, L, W, h, 2, ys=[-0.24, 0.24], r=0.085)
    if P.ca:
        hull(octo(W * 0.8, L, 0.08), octo(W * 0.62, L * 0.6, 0.06), 0.11, 0.25, material=P.hull, bevel=0.012, name="body")
        box((W * 0.5, 0.12, 0.05), (0, 0.06, 0.26), P.glass, bevel=0.01, name="canopy")
    else:
        box((W * 0.82, L * 0.95, 0.12), (0, 0, 0.17), P.hull, bevel=0.01, name="body")
        box((W * 0.6, 0.05, 0.07), (0, L * 0.25, 0.26), P.hull_dk, bevel=0.006, rot=(-20, 0, 0), name="shield")
        rivet_row(P, (-W * 0.35, L * 0.47, 0.18), (W * 0.35, L * 0.47, 0.18), 5, axis_normal="Y")
    box((W * 0.4, 0.06, 0.01), (0, -L * 0.3, 0.255), P.team, bevel=0.002, name="team")
    for s in (-1, 1):
        headlight(P, s * W * 0.28, L * 0.47, 0.18)
    antenna(P, -W * 0.3, -L * 0.35, 0.24, 0.25, part="Body")
    piv = (0, -0.02, 0.26)
    set_turret(info, piv)
    cyl(0.07, 0.05, (0, piv[1], piv[2] + 0.025), P.dark, part="Turret", seg=12, name="ring_mount")
    box((0.05, 0.1, 0.05), (0, piv[1] + 0.03, piv[2] + 0.07), P.metal_dk, part="Turret", bevel=0.006, name="mg_body")
    barrel(P, 0, piv[1] + 0.08, 0.2, 0.01, piv[2] + 0.075, brake=False, sleeve=False, info=info, recoil=0.03)
    box((0.08, 0.012, 0.06), (0, piv[1] + 0.06, piv[2] + 0.08), P.hull_dk, part="Turret", bevel=0.004, name="gun_shield")
    info.height = 0.45
    info.muzzle_forward = 0.3
    info.muzzle_height = piv[2] + 0.075


@model("apc")
def apc(P, info):
    L, W, h = 0.95, 0.6, 0.32
    r = wheeled(P, info, L, W, h, 3, r=0.1)
    if P.ca:
        hull(octo(W * 0.86, L, 0.1), octo(W * 0.7, L * 0.8, 0.08), 0.14, 0.4, material=P.hull, bevel=0.014, name="body")
        prof = [(L * 0.4, 0.14), (L * 0.52, 0.2), (L * 0.4, 0.38)]
        poly_prism(prof, W * 0.7, material=P.hull_lt, axis="X", bevel=0.008, name="nose")
    else:
        box((W * 0.86, L, 0.24), (0, 0, 0.26), P.hull, bevel=0.012, name="body")
        poly_prism([(L * 0.5, 0.14), (L * 0.58, 0.2), (L * 0.5, 0.38)], W * 0.8, material=P.hull_dk, axis="X", bevel=0.006, name="nose")
        for s in (-1, 1):
            rivet_row(P, (s * W * 0.43, -L * 0.45, 0.33), (s * W * 0.43, L * 0.45, 0.33), 9, axis_normal="X")
    # rear ramp, side doors, vision blocks
    box((W * 0.5, 0.025, 0.2), (0, -L * 0.5, 0.25), P.hull_dk, bevel=0.006, name="ramp")
    for s in (-1, 1):
        for k in range(3):
            box((0.012, 0.035, 0.025), (s * W * 0.37, -0.2 + k * 0.16, 0.36), P.glass, bevel=0.003, name="vision_block")
        headlight(P, s * W * 0.3, L * 0.48 + (0.06 if not P.ca else 0.0), 0.24)
        box((0.008, L * 0.4, 0.04), (s * (W * 0.43 + 0.005), -0.05, 0.26), P.team, bevel=0.002, name="team_side")
    for k in range(4):
        cyl(0.012, 0.035, (-W * 0.26 + k * 0.025, L * 0.3, 0.4), P.dark, axis="Y", seg=8, name="smoke")
    piv = (0, 0.05, 0.4)
    set_turret(info, piv)
    cyl(0.12, 0.07, (0, piv[1], piv[2] + 0.035), P.hull_dk, part="Turret", seg=16, name="turret")
    box((0.1, 0.1, 0.06), (0, piv[1] + 0.04, piv[2] + 0.08), P.hull, part="Turret", bevel=0.012, name="gun_house")
    barrel(P, 0, piv[1] + 0.1, 0.24, 0.012, piv[2] + 0.085, brake=False, sleeve=False, info=info, recoil=0.04)
    antenna(P, -0.08, piv[1] - 0.08, piv[2] + 0.07, 0.2)
    info.height = 0.6
    info.muzzle_forward = 0.4
    info.muzzle_height = piv[2] + 0.085


def truck(P, info, kind):
    L, W = 1.0, 0.5
    wheeled(P, info, L, W, 0.24, 3, ys=[-0.34, -0.16, 0.3], r=0.09)
    cab(P, 0, L * 0.3, 0.13, W * 0.86, 0.26, 0.22)
    zb = 0.17
    if kind == "convoy":
        # supply bed with crates under a canvas cover
        box((W * 0.92, 0.6, 0.04), (0, -0.2, zb), P.hull_dk, bevel=0.006, name="bed")
        canvas = P.uniform if P.ca else P.leather
        poly_prism([(-W * 0.45, 0), (W * 0.45, 0), (W * 0.45, 0.16), (W * 0.3, 0.24), (-W * 0.3, 0.24), (-W * 0.45, 0.16)], 0.56,
                   loc=(0, -0.2, zb + 0.02), material=canvas, axis="Y", bevel=0.01, name="canvas")
        box((W * 0.5, 0.012, 0.08), (0, -0.48, zb + 0.15), P.team, bevel=0.002, name="team_tailgate")
        for k in range(4):
            poly_prism([(-W * 0.46, 0), (W * 0.46, 0), (W * 0.46, 0.165), (W * 0.31, 0.245), (-W * 0.31, 0.245), (-W * 0.46, 0.165)], 0.015,
                       loc=(0, -0.44 + k * 0.16, zb + 0.02), material=P.hull_dk, axis="Y", bevel=0, name="hoop")
    else:
        # engineer carrier: enclosed box body with ladder, tool racks and a beacon
        box((W * 0.92, 0.6, 0.26), (0, -0.2, zb + 0.13), P.hull, bevel=0.012, name="crew_box")
        box((W * 0.94, 0.12, 0.03), (0, -0.2, zb + 0.27), P.team, bevel=0.004, name="team_roof")
        for k in range(5):
            box((0.1, 0.008, 0.008), (W * 0.3, -0.48 + k * 0.025, zb + 0.29), P.metal, bevel=0, name="ladder")
        box((0.15, 0.012, 0.12), (-W * 0.2, -0.505, zb + 0.13), P.hull_dk, bevel=0.004, name="rear_door")
        for s in (-1, 1):
            box((0.012, 0.3, 0.04), (s * W * 0.47, -0.2, zb + 0.06), P.yellow, bevel=0.003, name="hazard_stripe")
        beacon(P, info, 0, L * 0.3 - 0.02, 0.36)
    info.height = 0.55


@model("convoy_truck")
def convoy_truck(P, info):
    truck(P, info, "convoy")


@model("eng_carrier")
def eng_carrier(P, info):
    truck(P, info, "engineer")


@model("repair_truck")
def repair_truck(P, info):
    L, W = 1.0, 0.52
    wheeled(P, info, L, W, 0.24, 3, ys=[-0.34, -0.16, 0.3], r=0.09)
    cab(P, 0, L * 0.3, 0.13, W * 0.86, 0.26, 0.22)
    box((W * 0.92, 0.6, 0.12), (0, -0.2, 0.23), P.hull, bevel=0.01, name="bed")
    box((W * 0.94, 0.06, 0.13), (0, -0.2, 0.235), P.team, bevel=0.004, name="team_band")
    # crane arm that slowly swings over the bed
    crane = info.animate(SPIN, 25, (0, -0.32, 0.3))
    cyl(0.05, 0.06, (0, -0.32, 0.32), P.yellow, part=crane, seg=12, name="crane_base")
    box((0.045, 0.36, 0.045), (0, -0.2, 0.42), P.yellow, part=crane, bevel=0.008, rot=(-18, 0, 0), name="crane_arm")
    cyl(0.003, 0.18, (0, -0.03, 0.4), P.metal_dk, part=crane, seg=4, bevel=0, name="cable")
    box((0.04, 0.03, 0.03), (0, -0.03, 0.3), P.metal_dk, part=crane, bevel=0.006, name="hook")
    for s in (-1, 1):
        box((0.06, 0.1, 0.05), (s * W * 0.35, -0.42, 0.33), P.metal, bevel=0.008, name="toolbox")
    beacon(P, info, -W * 0.25, L * 0.3 - 0.02, 0.36, rate=2.0)
    beacon(P, info, W * 0.25, L * 0.3 - 0.02, 0.36, rate=2.0)
    info.height = 0.6


# ============================================================================================ tanks

def gun_turret(P, info, piv, w, l, h, blen, br, twin=False, recoil=0.07):
    top = turret_body(P, piv, w, l, h)
    x, y, z = piv
    box((w * 0.4, l * 0.18, h * 0.65), (x, y + l * 0.42, z + h * 0.5), P.hull_dk, part="Turret", bevel=0.012, name="mantlet")
    offs = (-br * 1.8, br * 1.8) if twin else (0.0,)
    for o in offs:
        barrel(P, x + o, y + l * 0.48, blen, br, z + h * 0.52, info=info, recoil=recoil, sleeve=not twin)
    cyl(w * 0.14, 0.035, (x + w * 0.2, y - l * 0.15, top + 0.017), P.hull_lt, part="Turret", seg=12, name="cupola")
    cyl(0.007, 0.12, (x + w * 0.2, y - l * 0.1, top + 0.05), P.metal_dk, axis="Y", part="Turret", seg=6, name="mg")
    box((w * 0.18, w * 0.18, 0.05), (x - w * 0.22, y + l * 0.1, top + 0.025), P.dark, part="Turret", bevel=0.008, name="sight")
    antenna(P, x - w * 0.35, y - l * 0.4, top - 0.01, 0.22)
    box((w * 0.8, l * 0.2, h * 0.6), (x, y - l * 0.62, z + h * 0.45), P.hull_dk, part="Turret", bevel=0.01, name="bustle")
    return top


@model("tank_heavy")
def tank_heavy(P, info):
    L, W, h = 1.15, 0.78, 0.3
    top = tracked(P, info, L, W, h, wheels=7)
    piv = (0, -0.05, top)
    set_turret(info, piv)
    gun_turret(P, info, piv, 0.5, 0.5, 0.17, 0.62, 0.028, twin=True, recoil=0.08)
    # extra armour plates on the hull front
    if P.ca:
        box((W * 0.6, 0.04, 0.08), (0, L * 0.42, top - 0.06), P.hull_lt, bevel=0.01, rot=(-35, 0, 0), name="applique")
    else:
        for k in range(3):
            box((0.12, 0.03, 0.08), (-0.16 + k * 0.16, L * 0.44, top - 0.08), P.hull_dk, bevel=0.008, rot=(-30, 0, 0), name="era_block")
    info.height = 0.62
    info.muzzle_forward = 0.62 + 0.2
    info.muzzle_height = top + 0.09


@model("td")
def tank_destroyer(P, info):
    L, W, h = 1.0, 0.64, 0.24
    top = tracked(P, info, L, W, h, wheels=5)
    piv = (0, -0.05, top)
    set_turret(info, piv)
    x, y, z = piv
    # low casemate with a long gun
    if P.ca:
        cb = [(-0.22, -0.3), (0.22, -0.3), (0.25, 0.1), (0.12, 0.28), (-0.12, 0.28), (-0.25, 0.1)]
        ct = [(-0.18, -0.28), (0.18, -0.28), (0.2, 0.06), (0.09, 0.18), (-0.09, 0.18), (-0.2, 0.06)]
        hull(cb, ct, 0.0, 0.12, loc=(x, y, z), material=P.hull, part="Turret", bevel=0.012, name="casemate")
    else:
        box((0.48, 0.55, 0.13), (x, y, z + 0.065), P.hull, part="Turret", bevel=0.012, name="casemate")
        box((0.5, 0.04, 0.14), (x, y + 0.28, z + 0.065), P.hull_dk, part="Turret", bevel=0.008, rot=(-25, 0, 0), name="front_plate")
    box((0.12, 0.08, 0.09), (x, y + 0.28, z + 0.06), P.hull_dk, part="Turret", bevel=0.015, name="mantlet")
    barrel(P, x, y + 0.3, 0.68, 0.026, z + 0.065, info=info, recoil=0.09)
    box((0.15, 0.08, 0.01), (x, y - 0.12, z + 0.125), P.team, part="Turret", bevel=0.002, name="team_roof")
    antenna(P, x - 0.15, y - 0.2, z + 0.12, 0.2)
    info.height = 0.5
    info.muzzle_forward = 1.0
    info.muzzle_height = z + 0.065


@model("flame_tank")
def flame_tank(P, info):
    L, W, h = 0.95, 0.64, 0.26
    top = tracked(P, info, L, W, h, wheels=5)
    piv = (0, 0.0, top)
    set_turret(info, piv)
    x, y, z = piv
    t = turret_body(P, piv, 0.36, 0.34, 0.13)
    # twin projector nozzles with pilot flames
    from models import mat_glow
    for s in (-1, 1):
        cyl(0.022, 0.2, (x + s * 0.05, y + 0.25, z + 0.07), P.metal_dk, axis="Y", part="Turret", seg=10, name="projector")
        cyl(0.03, 0.03, (x + s * 0.05, y + 0.35, z + 0.07), P.metal, axis="Y", part="Turret", seg=10, name="nozzle")
        flame = info.animate(BOB, 0.01, (x + s * 0.05, y + 0.375, z + 0.07), parent="Turret", phase=0.3 * s)
        sphere(0.014, (x + s * 0.05, y + 0.375, z + 0.07), mat_glow(P, (1.0, 0.55, 0.15)), seg=8, rings=6, part=flame, name="pilot")
    # fuel tanks on the rear deck
    for s in (-1, 1):
        cyl(0.06, 0.32, (s * 0.15, -L * 0.3, top + 0.06), P.accent if not P.ca else P.yellow, axis="Y", seg=14, name="fuel_tank")
        for k in (-0.1, 0.1):
            cyl(0.063, 0.015, (s * 0.15, -L * 0.3 + k, top + 0.06), P.metal_dk, axis="Y", seg=14, name="tank_strap")
    cyl(0.01, 0.4, (0, -0.05, top + 0.02), P.rubber, axis="Y", seg=6, name="fuel_line")
    antenna(P, -0.15, -0.12, t, 0.18)
    info.height = 0.5
    info.muzzle_forward = 0.5
    info.muzzle_height = 0.38


@model("aa_track")
def aa_track(P, info):
    L, W, h = 0.95, 0.62, 0.24
    top = tracked(P, info, L, W, h, wheels=5)
    piv = (0, 0.0, top)
    set_turret(info, piv)
    x, y, z = piv
    t = turret_body(P, piv, 0.36, 0.36, 0.14)
    # quad autocannons on side mounts
    for s in (-1, 1):
        box((0.05, 0.16, 0.08), (x + s * 0.22, y + 0.02, z + 0.12), P.hull_dk, part="Turret", bevel=0.01, name="gun_pod")
        for k in (-0.018, 0.018):
            cyl(0.008, 0.26, (x + s * 0.22 + k, y + 0.21, z + 0.15), P.metal_dk, axis="Y", part="Turret", seg=6, name="autocannon")
    # tracking radar dish that spins on its mast
    dish = info.animate(SPIN, 200, (x, y - 0.12, t + 0.06), parent="Turret")
    cyl(0.012, 0.08, (x, y - 0.12, t + 0.04), P.metal_dk, part="Turret", seg=6, name="radar_mast")
    box((0.16, 0.02, 0.07), (x, y - 0.12, t + 0.1), P.metal, part=dish, bevel=0.008, rot=(-10, 0, 0), name="radar_dish")
    box((0.03, 0.03, 0.03), (x, y - 0.1, t + 0.1), P.dark, part=dish, bevel=0.005, name="radar_feed")
    info.height = 0.6
    info.muzzle_forward = 0.4
    info.muzzle_height = 0.55


@model("howitzer")
def howitzer(P, info):
    L, W, h = 1.05, 0.62, 0.24
    top = tracked(P, info, L, W, h, wheels=6)
    piv = (0, -0.08, top)
    set_turret(info, piv)
    x, y, z = piv
    # tall boxy gun house with an elevated barrel
    if P.ca:
        hull(octo(0.46, 0.5, 0.06), octo(0.42, 0.44, 0.05), 0.0, 0.18, loc=(x, y, z), material=P.hull, part="Turret", bevel=0.012, name="gun_house")
    else:
        box((0.48, 0.5, 0.19), (x, y, z + 0.095), P.hull, part="Turret", bevel=0.012, name="gun_house")
        rivet_row(P, (x - 0.2, y + 0.252, z + 0.15), (x + 0.2, y + 0.252, z + 0.15), 6, axis_normal="Y")
    box((0.49, 0.08, 0.05), (x, y - 0.05, z + 0.1), P.team, part="Turret", bevel=0.004, name="team_band")
    gun = info.animate(RECOIL, 0.08, (x, y + 0.22, z + 0.14), parent="Turret")
    box((0.12, 0.08, 0.1), (x, y + 0.24, z + 0.12), P.hull_dk, part="Turret", bevel=0.012, name="cradle")
    import bpy
    for ob in list(bpy.context.scene.objects):
        pass
    # barrel pitched up 25 degrees
    b = cyl(0.026, 0.62, (x, y + 0.24 + 0.31 * math.cos(math.radians(25)), z + 0.14 + 0.31 * math.sin(math.radians(25))), P.metal,
            axis="Y", seg=14, part=gun, name="barrel")
    b.rotation_euler = (math.radians(-90 + 25), 0, 0)
    tip = (x, y + 0.24 + 0.62 * math.cos(math.radians(25)), z + 0.14 + 0.62 * math.sin(math.radians(25)))
    br = cyl(0.04, 0.06, tip, P.metal_dk, axis="Y", seg=14, part=gun, name="brake")
    br.rotation_euler = (math.radians(-90 + 25), 0, 0)
    # recoil cylinders and spades at the rear
    for s in (-1, 1):
        box((0.06, 0.14, 0.03), (s * 0.18, -L * 0.52, 0.1), P.metal_dk, bevel=0.006, rot=(30, 0, 0), name="spade")
    antenna(P, x - 0.18, y - 0.18, z + 0.18, 0.2)
    info.height = 0.65
    info.muzzle_forward = 0.95
    info.muzzle_height = 0.65


@model("mlrs")
def mlrs(P, info):
    L, W = 1.05, 0.6
    wheeled(P, info, L, W, 0.26, 3, ys=[-0.36, -0.12, 0.3], r=0.095)
    cab(P, 0, L * 0.33, 0.14, W * 0.84, 0.26, 0.22)
    box((W * 0.9, 0.62, 0.08), (0, -0.18, 0.21), P.hull, bevel=0.01, name="bed")
    box((W * 0.92, 0.08, 0.06), (0, -0.18, 0.24), P.team, bevel=0.004, name="team_band")
    for s in (-1, 1):
        box((0.06, 0.06, 0.08), (s * W * 0.42, -0.45, 0.17), P.metal_dk, bevel=0.006, name="outrigger")
    piv = (0, -0.15, 0.26)
    set_turret(info, piv)
    x, y, z = piv
    cyl(0.1, 0.05, (x, y, z + 0.025), P.hull_dk, part="Turret", seg=14, name="launcher_base")
    # rocket pod pitched up, with visible rocket tips
    pod = box((0.34, 0.42, 0.18), (x, y + 0.05, z + 0.18), P.hull, part="Turret", bevel=0.014, rot=(20, 0, 0), name="pod")
    for i in range(3):
        for j in range(2):
            px, pz = x - 0.11 + i * 0.11, z + 0.15 + j * 0.08
            t = cyl(0.03, 0.02, (px, y + 0.26, pz + 0.07), P.dark, axis="Y", part="Turret", seg=10, bevel=0, name="tube")
            t.rotation_euler = (math.radians(-90 + 20), 0, 0)
            c = cyl(0.022, 0.03, (px, y + 0.265, pz + 0.075), P.accent, axis="Y", part="Turret", seg=10, r2=0.004, bevel=0, name="rocket_tip")
            c.rotation_euler = (math.radians(-90 + 20), 0, 0)
    info.height = 0.6
    info.muzzle_forward = 0.3
    info.muzzle_height = 0.55


@model("siege")
def siege(P, info):
    L, W, h = 1.4, 0.9, 0.32
    top = tracked(P, info, L, W, h, wheels=8)
    piv = (0, -0.1, top + 0.04)
    set_turret(info, piv)
    x, y, z = piv
    cyl(0.3, 0.05, (x, y, z - 0.02), P.hull_dk, part="Turret", seg=20, name="turret_ring")
    t = turret_body(P, (x, y, z), 0.58, 0.6, 0.2)
    from models import mat_glow
    # long rail cannon with glowing coils
    for s in (-1, 1):
        box((0.04, 0.95, 0.06), (x + s * 0.05, y + 0.72, z + 0.11), P.metal_dk, part="Turret", bevel=0.008, name="rail")
    for k in range(5):
        cyl(0.07, 0.03, (x, y + 0.35 + k * 0.15, z + 0.11), P.metal, axis="Y", part="Turret", seg=14, name="coil")
        cyl(0.072, 0.012, (x, y + 0.35 + k * 0.15, z + 0.11), mat_glow(P), axis="Y", part="Turret", seg=14, bevel=0, name="coil_glow")
    for s in (-1, 1):
        box((0.12, 0.3, 0.12), (x + s * 0.34, y - 0.1, z + 0.08), P.hull_dk, part="Turret", bevel=0.012, name="capacitor")
        light = info.animate(BLINK, 0.8, (x + s * 0.34, y - 0.27, z + 0.12), parent="Turret", phase=0.5 if s > 0 else 0.0)
        box((0.06, 0.01, 0.03), (x + s * 0.34, y - 0.255, z + 0.12), mat_glow(P), part=light, bevel=0, name="charge_light")
    antenna(P, x - 0.25, y - 0.25, t, 0.25)
    info.height = 0.8
    info.muzzle_forward = 1.2
    info.muzzle_height = 0.75


# ============================================================================================ economy vehicles

@model("harvester")
def harvester(P, info):
    L, W, h = 1.15, 0.78, 0.3
    tw = W * 0.24
    for s in (-1, 1):
        track_unit(P, info, s * (W / 2 - tw / 2), L * 0.9, tw, h * 0.75, wheels=5, skirt=False)
    # hopper body
    box((W * 0.62, L * 0.6, 0.3), (0, -0.12, 0.3), P.hull, bevel=0.016, name="hopper")
    box((W * 0.66, L * 0.62, 0.04), (0, -0.12, 0.47), P.hull_dk, bevel=0.008, name="hopper_rim")
    from kit import mat
    ore = mat("ore", (0.85, 0.7, 0.25), 0.4, 0.3)
    for k in range(6):
        sphere(0.06, (-0.12 + (k % 3) * 0.12, -0.25 + (k // 3) * 0.16, 0.47), ore, seg=8, rings=5, scale=(1, 1, 0.6), name="ore_load")
    box((W * 0.64, 0.1, 0.12), (0, -0.12, 0.36), P.team, bevel=0.006, name="team_band")
    # cab on one side
    cab(P, -W * 0.22, L * 0.2, 0.22, 0.2, 0.2, 0.16)
    # front collector: arms and a spinning drum
    for s in (-1, 1):
        box((0.04, 0.24, 0.05), (s * W * 0.3, L * 0.46, 0.18), P.hull_dk, bevel=0.008, rot=(18, 0, 0), name="drum_arm")
    drum = info.animate(SPIN_X, 360, (0, L * 0.56, 0.1))
    cyl(0.075, W * 0.6, (0, L * 0.56, 0.1), P.metal, axis="X", seg=14, part=drum, name="drum")
    for k in range(6):
        a = k * math.tau / 6
        box((W * 0.6, 0.02, 0.05), (0, L * 0.56 + math.cos(a) * 0.08, 0.1 + math.sin(a) * 0.08), P.metal_dk, part=drum, bevel=0.003,
            rot=(math.degrees(a), 0, 0), name="drum_tooth")
    # exhaust stack with flashing work light
    cyl(0.025, 0.2, (W * 0.28, L * 0.08, 0.52), P.metal_dk, seg=10, name="exhaust")
    beacon(P, info, -W * 0.22, L * 0.2, 0.39)
    info.height = 0.8


@model("mcv")
def mcv(P, info):
    L, W = 1.3, 0.72
    wheeled(P, info, L, W, 0.3, 4, r=0.11)
    cab(P, 0, L * 0.38, 0.17, W * 0.84, 0.24, 0.24)
    # folded base modules on the bed
    box((W * 0.9, 0.85, 0.24), (0, -0.16, 0.34), P.hull, bevel=0.016, name="module_low")
    box((W * 0.6, 0.4, 0.16), (0.06, -0.3, 0.54), P.hull_dk, bevel=0.012, name="module_high")
    box((W * 0.92, 0.1, 0.1), (0, -0.16, 0.38), P.team, bevel=0.006, name="team_band")
    for k in range(4):
        box((0.012, 0.15, 0.06), (W * 0.452, -0.5 + k * 0.2, 0.34), P.dark, bevel=0.003, name="panel_line")
    # folded crane
    box((0.05, 0.6, 0.05), (-W * 0.3, -0.05, 0.5), P.yellow, bevel=0.008, name="folded_crane")
    # comms dish that turns
    dish = info.animate(SPIN, 40, (0.06, -0.3, 0.62))
    cyl(0.015, 0.06, (0.06, -0.3, 0.65), P.metal_dk, part=dish, seg=6, name="dish_mast")
    d = cyl(0.09, 0.02, (0.06, -0.26, 0.71), P.metal, part=dish, seg=16, r2=0.06, name="dish")
    d.rotation_euler = (math.radians(-60), 0, 0)
    beacon(P, info, -W * 0.3, L * 0.38 - 0.06, 0.43)
    beacon(P, info, W * 0.3, L * 0.38 - 0.06, 0.43)
    info.height = 1.0
