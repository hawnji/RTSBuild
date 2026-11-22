"""Model definitions. Each builder takes (P: Palette, info: ModelInfo) and creates objects in the current scene.
Register builders in MODELS with the game's model key (UnitDef.Model / BuildingDef.Model or role name)."""
import math

from mathutils import Vector

from kit import (AIM, ARM_L, ARM_R, BLINK, BOB, LEG_L, LEG_R, RECOIL, ROCK, SPIN, SPIN_X, TREAD_BOTTOM, TREAD_TOP, WAVE,
                 WHEEL, array, box, capsule, cyl, hull, mirror_x, octo, poly_prism, sphere)

MODELS = {}


def model(key, footprint=None):
    def reg(fn):
        MODELS[key] = (fn, footprint)
        return fn
    return reg


# ============================================================================================ shared vehicle parts

def track_unit(P, info, x, length, tw, h, wheels=5, skirt=True, wheel_r=None):
    """One track assembly centred at x. length along Y, track width tw, track height h.
    Road wheels, sprocket and idler roll, and the track links slide as the vehicle moves."""
    L = length / 2
    prof = [(-L, h * 0.38), (-L + h * 0.32, 0.0), (L - h * 0.32, 0.0), (L + h * 0.04, h * 0.42),
            (L - h * 0.1, h * 0.82), (-L + h * 0.12, h * 0.82)]
    poly_prism(prof, tw, loc=(x, 0, 0), material=P.rubber, axis="X", bevel=0.01, name="track")
    # track links on the bottom and top runs
    spacing = 0.045
    bottom = info.animate(TREAD_BOTTOM, spacing, (x, 0, 0))
    top = info.animate(TREAD_TOP, spacing, (x, 0, 0))
    n = int((length - h * 0.64) / spacing)
    for i in range(n):
        y = -L + h * 0.32 + spacing * (i + 0.5)
        box((tw * 1.02, 0.014, 0.01), (x, y, 0.004), P.metal_dk, part=bottom, bevel=0, name="grouser")
    n2 = int((length - h * 0.3) / spacing)
    for i in range(n2):
        y = -L + h * 0.14 + spacing * (i + 0.5)
        box((tw * 1.02, 0.014, 0.008), (x, y, h * 0.82 + 0.003), P.metal_dk, part=top, bevel=0, name="grouser_top")
    wr = wheel_r or h * 0.3
    side = 1 if x > 0 else -1

    def wheel(r, y, z, mat_outer, mat_hub, wx, depth, spokes=True):
        c = (x + side * wx, y, z)
        part = info.animate(WHEEL, r, c)
        cyl(r, depth, c, mat_outer, axis="X", seg=14, part=part, name="wheel")
        cyl(r * 0.45, depth * 1.04, c, mat_hub, axis="X", seg=10, part=part, name="hub")
        if spokes:
            # bolts make the rotation visible
            for k in range(4):
                ang = k * math.tau / 4
                cyl(r * 0.1, depth * 1.08, (c[0], y + math.cos(ang) * r * 0.62, z + math.sin(ang) * r * 0.62), mat_hub, axis="X", seg=5,
                    part=part, bevel=0, name="bolt")

    for i in range(wheels):
        y = -L * 0.72 + (L * 1.44) * i / max(1, wheels - 1)
        wheel(wr, y, wr + 0.012, P.metal, P.metal_dk, tw * 0.3, tw * 0.5)
    wheel(h * 0.24, L - h * 0.1, h * 0.5, P.metal_dk, P.metal, tw * 0.22, tw * 0.6)    # drive sprocket
    wheel(h * 0.22, -L + h * 0.12, h * 0.46, P.metal_dk, P.metal, tw * 0.22, tw * 0.6)  # idler
    if skirt:
        box((0.02, length * 0.86, h * 0.42), (x + side * tw * 0.52, 0.02, h * 0.62), P.hull_dk, bevel=0.006, name="skirt")


def rivet_row(P, start, end, n, r=0.008, axis_normal="Z", material=None):
    s, e = Vector(start), Vector(end)
    for i in range(n):
        p = s.lerp(e, (i + 0.5) / n)
        cyl(r, r * 0.8, tuple(p), material or P.hull_dk, axis=axis_normal, seg=6, bevel=0, name="rivet")


def barrel(P, x, y0, length, r, z, part="Turret", brake=True, sleeve=True, info=None, recoil=0.07):
    """Gun barrel from y0 forward. With info, the barrel becomes its own part that recoils when firing."""
    if info is not None:
        part = info.animate(RECOIL, recoil, (x, y0, z), parent=part)
    cyl(r, length, (x, y0 + length / 2, z), P.metal, axis="Y", seg=14, part=part, name="barrel")
    if sleeve:
        for k in (0.25, 0.55):
            cyl(r * 1.25, length * 0.07, (x, y0 + length * k, z), P.metal_dk, axis="Y", seg=14, part=part, name="sleeve")
    if brake:
        cyl(r * 1.45, length * 0.09, (x, y0 + length * 0.955, z), P.metal_dk, axis="Y", seg=14, part=part, name="brake")
        box((r * 3.6, length * 0.05, r * 0.8), (x, y0 + length * 0.955, z), P.metal_dk, part=part, bevel=0.003, name="brake_ports")
    return part


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
            track_unit(P, info, s * (W / 2 - tw / 2), L * 0.96, tw, 0.2, wheels=6, skirt=True)
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
        barrel(P, 0, piv[1] + 0.22, 0.5, 0.024, piv[2] + 0.068, info=info)
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
            track_unit(P, info, s * (W / 2 - tw / 2), L * 0.96, tw, 0.21, wheels=5, skirt=False, wheel_r=0.075)
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
        barrel(P, 0, piv[1] + 0.21, 0.5, 0.026, piv[2] + 0.055, sleeve=False, info=info)
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

def soldier(P, info, kind="rifle"):
    """Animated infantryman. Legs swing from the hips; the arms and weapon form one 'aim' part that sways while
    walking and kicks when firing. Unarmed roles (engineer, medic) swing their arms instead."""
    info.infantry = True
    ca = P.ca
    heavy = kind in ("heavy", "elite")
    sc = 1.1 if heavy else 1.0
    boot = P.leather if ca else P.metal_dk
    # ---- legs
    for s, kd in ((-1, LEG_L), (1, LEG_R)):
        hip = (s * 0.04 * sc, 0.0, 0.165 * sc)
        leg = info.animate(kd, 28, hip)
        capsule((s * 0.04 * sc, 0.0, 0.04), hip, 0.025 * sc, P.uniform_dk, part=leg, name="leg")
        box((0.042 * sc, 0.068 * sc, 0.03), (s * 0.04 * sc, 0.012, 0.016), boot, part=leg, bevel=0.01, name="boot")
        if heavy:
            box((0.05 * sc, 0.03, 0.06), (s * 0.04 * sc, 0.028, 0.1), P.hull if ca else P.hull_dk, part=leg, bevel=0.01, name="knee_guard")
    # ---- torso
    z0 = 0.165 * sc
    box((0.12 * sc, 0.075 * sc, 0.13 * sc), (0, 0, z0 + 0.07 * sc), P.uniform, bevel=0.025, seg=3, name="torso")
    if heavy:
        box((0.15 * sc, 0.1 * sc, 0.1 * sc), (0, 0.004, z0 + 0.085 * sc), P.hull if ca else P.hull_dk, bevel=0.025, seg=2, name="armour")
        box((0.152 * sc, 0.03, 0.035), (0, 0.05 * sc, z0 + 0.09 * sc), P.team, bevel=0.008, name="armour_team")
        for s in (-1, 1):
            sphere(0.035 * sc, (s * 0.08 * sc, 0, z0 + 0.13 * sc), P.hull if ca else P.hull_dk, seg=10, rings=6, scale=(1, 1, 0.7), name="pauldron")
    elif ca:
        box((0.13, 0.085, 0.085), (0, 0.002, z0 + 0.08), P.team, bevel=0.02, seg=2, name="vest")
        for k in (-0.035, 0.0, 0.035):
            box((0.028, 0.02, 0.03), (k, 0.048, z0 + 0.06), P.uniform_dk, bevel=0.006, name="pouch")
    else:
        box((0.13, 0.085, 0.085), (0, 0.002, z0 + 0.08), P.uniform_dk, bevel=0.02, seg=2, name="greatcoat")
        box((0.135, 0.02, 0.025), (0, 0.045, z0 + 0.045), P.team, bevel=0.006, name="sash")
    box((0.13 * sc, 0.088 * sc, 0.018), (0, 0, z0 + 0.015), P.leather if not ca else P.uniform_dk, bevel=0.005, name="belt")
    # ---- back: pack or role equipment
    zb = z0 + 0.085 * sc
    if kind == "flame":
        for s in (-1, 1):
            cyl(0.03, 0.14, (s * 0.032, -0.07, zb), P.yellow if ca else P.accent, seg=12, name="fuel_tank")
            sphere(0.03, (s * 0.032, -0.07, zb + 0.07), P.yellow if ca else P.accent, seg=12, rings=6, half=True, name="tank_cap")
    elif kind == "medic":
        box((0.09, 0.05, 0.09), (0, -0.06, zb), P.white, bevel=0.015, name="medpack")
        box((0.03, 0.054, 0.012), (0, -0.06, zb), mat_red(P), bevel=0.002, name="cross_h")
        box((0.012, 0.054, 0.03), (0, -0.06, zb), mat_red(P), bevel=0.002, name="cross_v")
    elif kind == "engineer":
        box((0.1, 0.055, 0.1), (0, -0.065, zb), P.uniform_dk, bevel=0.015, name="toolpack")
        cyl(0.012, 0.1, (0.035, -0.07, zb + 0.08), P.metal, seg=6, name="wrench_handle")
        cyl(0.004, 0.18, (-0.035, -0.07, zb + 0.12), P.metal_dk, seg=4, bevel=0, name="aerial")
    elif kind == "elite":
        box((0.1, 0.05, 0.08), (0, -0.065, zb), P.dark, bevel=0.015, name="charge_pack")
        for k in (-0.025, 0.025):
            cyl(0.014, 0.07, (k, -0.095, zb), P.accent, axis="X", seg=8, name="charge")
    elif kind == "specialist":
        box((0.085, 0.05, 0.11), (0, -0.062, zb), P.uniform_dk, bevel=0.015, name="radio")
        cyl(0.004, 0.26, (0.03, -0.07, zb + 0.16), P.metal_dk, seg=4, bevel=0, name="aerial")
        sphere(0.008, (0.03, -0.07, zb + 0.29), P.light, seg=6, rings=4, name="aerial_tip")
    else:
        box((0.09, 0.05, 0.09), (0, -0.06, zb), P.uniform_dk if ca else P.leather, bevel=0.015, name="pack")
        cyl(0.02, 0.09, (0, -0.06, zb + 0.055), P.uniform_dk, axis="X", seg=8, name="bedroll")
    # ---- head
    zh = z0 + 0.15 * sc
    capsule((0, 0, zh - 0.015), (0, 0, zh + 0.005), 0.022, P.skin, name="neck")
    sphere(0.034, (0, 0.004, zh + 0.033), P.skin, seg=12, rings=8, scale=(0.95, 1.0, 1.12), name="head")
    helmet_mat = P.white if kind == "medic" else P.yellow if kind == "engineer" else P.uniform_dk
    if kind == "elite":
        sphere(0.043, (0, 0, zh + 0.036), P.dark, seg=14, rings=8, scale=(1, 1.05, 1.0), name="hood")
        box((0.06, 0.02, 0.02), (0, 0.036, zh + 0.04), mat_glow(P), bevel=0.004, name="visor")
    elif kind == "sniper":
        sphere(0.04, (0, 0, zh + 0.04), P.uniform_dk, seg=12, rings=6, scale=(1, 1, 0.75), half=True, name="cap")
        box((0.05, 0.04, 0.008), (0, 0.035, zh + 0.045), P.uniform_dk, bevel=0.003, name="cap_peak")
    elif ca:
        sphere(0.042, (0, -0.002, zh + 0.04), helmet_mat, seg=14, rings=8, scale=(1.0, 1.08, 0.82), half=True, name="helmet")
        box((0.05, 0.012, 0.012), (0, 0.04, zh + 0.045), P.metal_dk, bevel=0.003, name="goggles")
    else:
        sphere(0.04, (0, 0, zh + 0.042), helmet_mat, seg=14, rings=8, scale=(1.0, 1.0, 0.9), half=True, name="helmet")
        cyl(0.055, 0.008, (0, -0.004, zh + 0.042), helmet_mat, seg=14, r2=0.045, name="helmet_brim")
    if heavy and kind == "heavy":
        box((0.06, 0.02, 0.03), (0, 0.035, zh + 0.03), P.metal_dk, bevel=0.006, name="faceguard")
    info.height = zh + 0.08
    # ---- arms and weapon
    sh_z = z0 + 0.12 * sc
    if kind in ("engineer", "medic"):
        for s, kd in ((-1, ARM_L), (1, ARM_R)):
            sh = (s * 0.072 * sc, 0.0, sh_z)
            arm = info.animate(kd, 24, sh)
            capsule(sh, (s * 0.078, 0.0, sh_z - 0.07), 0.019, P.uniform, part=arm, name="upper_arm")
            capsule((s * 0.078, 0.0, sh_z - 0.07), (s * 0.08, 0.02, sh_z - 0.13), 0.016, P.uniform, part=arm, name="forearm")
            sphere(0.016, (s * 0.08, 0.022, sh_z - 0.14), P.skin, seg=8, rings=6, part=arm, name="hand")
        # tool in the right hand
        arm_r = [k for k, v in info.anim.items() if v[1] == ARM_R][-1]
        if kind == "engineer":
            box((0.06, 0.025, 0.04), (0.085, 0.03, sh_z - 0.17), P.yellow, part=arm_r, bevel=0.006, name="toolbox")
        else:
            box((0.05, 0.025, 0.04), (0.085, 0.03, sh_z - 0.17), P.white, part=arm_r, bevel=0.006, name="medkit")
        return
    aim = info.animate(AIM, 9 if kind in ("rifle", "sniper", "elite", "specialist") else 14, (0, 0, sh_z))
    gz = sh_z - 0.045
    gx = 0.02
    shoulder_launcher = kind in ("at", "aa")
    for s in (-1, 1):
        sh = (s * 0.072 * sc, 0.0, sh_z)
        if shoulder_launcher:
            el = (s * 0.07, 0.05, sh_z - 0.04)
            hand = (0.04 + s * 0.02, 0.07 + (0.04 if s < 0 else 0), sh_z - 0.0)
        else:
            el = (s * 0.075 * sc, 0.05, gz - 0.025)
            hand = (s * 0.025 + gx, 0.1 + (0.03 if s > 0 else 0), gz - 0.01)
        capsule(sh, el, 0.02 * sc, P.uniform, part=aim, name="upper_arm")
        capsule(el, hand, 0.017 * sc, P.uniform, part=aim, name="forearm")
        sphere(0.016, hand, P.skin, seg=8, rings=6, part=aim, name="hand")
    gun = P.metal_dk
    stock = P.leather if not ca else P.dark
    if kind in ("rifle", "elite", "specialist"):
        L = 0.2 if kind == "rifle" else 0.15
        box((0.018, L, 0.03), (gx, 0.1 + L / 2 - 0.08, gz), gun, part=aim, bevel=0.004, name="rifle_body")
        cyl(0.0055, 0.08, (gx, 0.1 + L - 0.07, gz + 0.006), gun, axis="Y", seg=6, part=aim, name="rifle_barrel")
        box((0.014, 0.03, 0.045), (gx, 0.1, gz - 0.03), gun, part=aim, bevel=0.003, name="magazine")
        box((0.016, 0.06, 0.035), (gx, 0.0, gz - 0.01), stock, part=aim, bevel=0.006, name="stock")
        if ca:
            box((0.014, 0.05, 0.016), (gx, 0.12, gz + 0.025), P.dark, part=aim, bevel=0.003, name="optic")
        if kind == "specialist":
            box((0.04, 0.03, 0.02), (-0.03, 0.08, gz + 0.06), P.dark, part=aim, bevel=0.005, name="binoculars")
    elif kind == "heavy":
        box((0.035, 0.22, 0.05), (gx, 0.1, gz), gun, part=aim, bevel=0.006, name="repeater")
        for k in range(3):
            ang = k * math.tau / 3
            cyl(0.006, 0.12, (gx + math.cos(ang) * 0.012, 0.27, gz + math.sin(ang) * 0.012), gun, axis="Y", seg=6, part=aim, name="barrels")
        box((0.04, 0.05, 0.05), (gx - 0.035, 0.08, gz - 0.02), P.metal, part=aim, bevel=0.008, name="ammo_box")
    elif kind == "sniper":
        box((0.016, 0.24, 0.026), (gx, 0.1, gz), stock, part=aim, bevel=0.005, name="rifle_body")
        cyl(0.005, 0.16, (gx, 0.3, gz + 0.006), gun, axis="Y", seg=6, part=aim, name="long_barrel")
        cyl(0.011, 0.08, (gx, 0.12, gz + 0.03), P.dark, axis="Y", seg=8, part=aim, name="scope")
        cyl(0.009, 0.004, (gx, 0.081, gz + 0.03), P.glass, axis="Y", seg=8, part=aim, bevel=0, name="scope_lens")
    elif kind == "flame":
        box((0.025, 0.16, 0.035), (gx, 0.1, gz), gun, part=aim, bevel=0.006, name="projector")
        cyl(0.013, 0.07, (gx, 0.21, gz + 0.004), P.metal, axis="Y", seg=8, part=aim, name="nozzle")
        sphere(0.008, (gx, 0.25, gz + 0.004), mat_glow(P, (1.0, 0.55, 0.15)), seg=6, rings=4, part=aim, name="pilot_flame")
        capsule((gx - 0.02, 0.04, gz - 0.02), (0.0, -0.05, z0 + 0.05), 0.008, P.rubber, part=aim, name="hose")
    elif shoulder_launcher:
        lz = sh_z + 0.03
        r = 0.022 if kind == "at" else 0.026
        cyl(r, 0.3, (0.055, 0.03, lz), P.uniform_dk if kind == "at" else P.dark, axis="Y", seg=12, part=aim, name="launcher")
        cyl(r * 1.25, 0.03, (0.055, 0.17, lz), gun, axis="Y", seg=12, part=aim, name="launcher_mouth")
        box((0.03, 0.04, 0.03), (0.02, 0.06, lz + 0.03), P.dark, part=aim, bevel=0.005, name="sight")
        if kind == "aa":
            box((0.012, 0.06, 0.05), (0.09, 0.08, lz + 0.03), gun, part=aim, bevel=0.003, name="iff_antenna")
            sphere(0.014, (0.055, 0.185, lz), P.white, seg=8, rings=6, part=aim, name="seeker")
        else:
            sphere(0.024, (0.055, 0.2, lz), P.accent, seg=10, rings=6, scale=(1, 1.4, 1), part=aim, name="warhead")


def mat_red(P):
    from kit import mat
    return mat("red", (0.8, 0.12, 0.1), 0.6)


def mat_glow(P, rgb=(0.3, 0.9, 1.0)):
    from kit import mat
    return mat("glow_%d_%d_%d" % tuple(int(c * 100) for c in rgb), rgb, 0.3, 0.0, emit=2.0)


for _k, _kind in (("inf_rifle", "rifle"), ("inf_heavy", "heavy"), ("inf_at", "at"), ("inf_aa", "aa"), ("inf_engineer", "engineer"),
                  ("inf_medic", "medic"), ("inf_flame", "flame"), ("inf_sniper", "sniper"), ("inf_elite", "elite"),
                  ("inf_specialist", "specialist")):
    MODELS[_k] = ((lambda kind: (lambda P, info: soldier(P, info, kind)))(_kind), None)



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


def flag(P, info, x, y, base, h=0.75):
    cyl(0.012, h, (x, y, base + h / 2), P.metal, seg=8, name="flagpole")
    sphere(0.016, (x, y, base + h + 0.01), P.yellow, seg=8, rings=6, name="finial")
    # the cloth is split in two so its outer half flutters more than the half on the pole
    inner = info.animate(WAVE, 10, (x, y, base), phase=0.0)
    outer = info.animate(WAVE, 22, (x, y + 0.08, base), parent="Body", phase=0.15)
    box((0.004, 0.08, 0.14), (x, y + 0.05, base + h - 0.09), P.team, part=inner, bevel=0.0, name="flag_in")
    box((0.004, 0.08, 0.13), (x, y + 0.13, base + h - 0.09), P.team, part=outer, bevel=0.0, name="flag_out")


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
        lamp = info.animate(BLINK, 0.5, (0, 0.05 - L / 2 - 0.06, base + 0.27))
        box((0.04, 0.02, 0.02), (0, 0.05 - L / 2 - 0.06, base + 0.27), P.light, part=lamp, bevel=0.002, name="door_light")
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
            fan = info.animate(SPIN, 360, (0.22, 0.05 + k, base + 0.735))
            cyl(0.05, 0.008, (0.22, 0.05 + k, base + 0.735), P.metal_dk, seg=12, part=fan, name="fan_hub")
            for j in range(3):
                box((0.09, 0.018, 0.004), (0.22, 0.05 + k, base + 0.742), P.metal, part=fan, bevel=0, rot=(0, 0, j * 60), name="fan_blade")
        # team stripe along the roof crown
        box((0.08, L * 0.9, 0.01), (-0.18, 0.05, base + 0.69), P.team, bevel=0.002, rot=(0, 20, 0), name="roof_team")
        # yard: sandbag walls, flag, obstacle course posts
        sandbags(P, -0.92, -0.92, -0.45, -0.92)
        sandbags(P, 0.45, -0.92, 0.92, -0.92)
        flag(P, info, 0.75, 0.75, base)
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
        sweep = info.animate(SPIN, 45, (tx, ty, base + 0.86))
        cyl(0.02, 0.03, (tx, ty, base + 0.865), P.metal_dk, part=sweep, seg=8, name="lamp_post")
        cyl(0.03, 0.05, (tx, ty + 0.03, base + 0.885), P.metal_dk, axis="Y", part=sweep, seg=10, name="searchlight")
        cyl(0.025, 0.006, (tx, ty + 0.058, base + 0.885), P.light, axis="Y", part=sweep, seg=10, bevel=0, name="searchlight_lens")
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
        flag(P, info, -0.85, 0.55, base, 0.6)
        info.height = 1.0



import vehicles  # noqa: E402,F401  (registers vehicle models)
import air_naval  # noqa: E402,F401
