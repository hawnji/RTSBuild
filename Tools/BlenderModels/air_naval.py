"""Aircraft and ships. Aircraft are centred on their origin (the game sets their altitude); ships float with the
waterline at Z = 0."""
import math

from kit import (BLINK, BOB, RECOIL, SPIN, SPIN_X, WAVE, box, cyl, hull, mat, octo, poly_prism, sphere)
from models import antenna, barrel, mat_glow, model, rivet_row


def nav_lights(P, info, xl, xr, y, z):
    red = mat("nav_red", (1.0, 0.15, 0.1), 0.3, 0.0, emit=3.0)
    green = mat("nav_green", (0.2, 1.0, 0.3), 0.3, 0.0, emit=3.0)
    for x, m, ph in ((xl, red, 0.0), (xr, green, 0.5)):
        part = info.animate(BLINK, 1.2, (x, y, z), phase=ph)
        sphere(0.014, (x, y, z), m, seg=8, rings=6, part=part, name="nav_light")


def engine_glow(P, info, x, y, z, r):
    """Afterburner glow that pulses."""
    part = info.animate(BOB, 0.0, (x, y, z))
    flame = mat_glow(P, (1.0, 0.55, 0.2))
    c = cyl(r * 0.8, r * 1.2, (x, y - r * 0.5, z), flame, axis="Y", seg=10, r2=r * 0.2, part=part, bevel=0, name="exhaust_glow")
    c.rotation_euler = (math.radians(90), 0, 0)
    return part


def fuselage(P, L, r, z=0.0, nose=0.25, tail=0.2, material=None, part="Body", squash=1.0):
    """Lofted round fuselage along Y from -L/2 to L/2."""
    import bmesh
    import bpy
    from kit import _new_obj, _smooth
    m = material or P.hull
    bm = bmesh.new()
    rings = []
    stations = [(-0.5, 0.35), (-0.5 + tail, 0.85), (-0.15, 1.0), (0.2, 1.0), (0.5 - nose, 0.85), (0.5 - nose * 0.4, 0.5), (0.5, 0.06)]
    seg = 12
    for t, k in stations:
        ring = []
        for i in range(seg):
            a = i * math.tau / seg
            ring.append(bm.verts.new((math.cos(a) * r * k, t * L, z + math.sin(a) * r * k * squash)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _new_obj("fuselage", bm, m, part)
    _smooth(ob)
    return ob


def wing(P, root_y, root_c, tip_y, tip_c, span, z, t=0.02, material=None, part="Body", dihedral=0.0):
    """Pair of wings. root_y/tip_y: leading edge Y; root_c/tip_c: chord."""
    m = material or P.hull_dk
    for s in (-1, 1):
        prof_b = [(0, root_y - root_c), (0, root_y), (s * span / 2, tip_y), (s * span / 2, tip_y - tip_c)]
        if s < 0:
            prof_b = list(reversed(prof_b))
        top = [(x, y) for x, y in prof_b]
        ob = hull(prof_b, top, z - t / 2, z + t / 2, material=m, part=part, bevel=0.004, seg=1, name="wing")
        if dihedral:
            ob.rotation_euler = (0, math.radians(-s * dihedral), 0)


# ============================================================================================ jets

def jet(P, info, L, span, kind):
    bomber = kind == "bomber"
    r = 0.11 if bomber else 0.07
    fuselage(P, L, r, nose=0.3 if not bomber else 0.22)
    # canopy
    sphere(r * 0.75, (0, L * 0.22, r * 0.55), P.glass, seg=12, rings=8, scale=(0.7, 1.9, 0.7), name="canopy")
    sweep = 0.1 if bomber else 0.28
    wing(P, L * 0.12, L * 0.35, L * (0.12 - sweep) - 0.02, L * 0.14, span, -r * 0.2, t=0.025 if bomber else 0.018)
    # tailplanes and fins
    wing(P, -L * 0.3, L * 0.14, -L * 0.42, L * 0.08, span * 0.38, 0.0, t=0.012)
    twin = kind == "fighter"
    for x in ((-r * 0.7, r * 0.7) if twin else (0.0,)):
        poly_prism([(-L * 0.48, 0.0), (-L * 0.3, 0.0), (-L * 0.4, r * 2.4), (-L * 0.47, r * 2.4)], 0.012, loc=(x, 0, r * 0.4),
                   material=P.hull_dk, axis="X", bevel=0.003, name="fin")
        box((0.014, L * 0.07, r * 0.6), (x, -L * 0.43, r * 2.4), P.team, bevel=0.002, name="fin_team")
    # wingtip team colours and nav lights
    tip_y = L * (0.12 - sweep) - 0.02 - L * 0.07
    for s in (-1, 1):
        box((span * 0.08, L * 0.1, 0.03), (s * span * 0.44, tip_y, -r * 0.2), P.team, bevel=0.004, name="wingtip")
    nav_lights(P, info, -span * 0.5, span * 0.5, tip_y, -r * 0.2)
    # engines
    if bomber:
        for s in (-1, 1):
            for k in (0.22, 0.36):
                cyl(0.05, 0.28, (s * span * k, -L * 0.02, -r * 0.6), P.dark, axis="Y", seg=12, name="engine")
                cyl(0.035, 0.02, (s * span * k, L * 0.12, -r * 0.6), P.metal_dk, axis="Y", seg=12, bevel=0, name="intake")
                engine_glow(P, info, s * span * k, -L * 0.02 - 0.14, -r * 0.6, 0.035)
        box((r * 1.2, L * 0.3, 0.02), (0, 0.0, -r * 0.98), P.dark, bevel=0.004, name="bomb_bay")
    else:
        for s in ((-1, 1) if twin else (0,)):
            engine_glow(P, info, s * r * 0.5, -L * 0.5, 0.0, r * (0.45 if twin else 0.7))
        for s in (-1, 1):
            box((0.04, L * 0.25, 0.05), (s * r * 1.1, L * 0.05, -r * 0.2), P.hull_dk, bevel=0.008, name="intake")
            cyl(0.012, 0.18, (s * span * 0.26, -0.02, -r * 0.9), P.white, axis="Y", seg=8, name="missile")
    if kind == "recon":
        sphere(r * 1.2, (0, -L * 0.05, -r * 0.9), P.dark, seg=14, rings=8, scale=(1, 1.6, 0.5), name="sensor_pod")
        cam = info.animate(SPIN, 90, (0, -L * 0.05, -r * 1.4))
        box((0.04, 0.04, 0.03), (0, -L * 0.05, -r * 1.45), P.glass, part=cam, bevel=0.006, name="camera")
    info.height = 0.3
    info.floating = True
    info.muzzle_forward = L * 0.5
    info.muzzle_height = 0.0


@model("fighter")
def fighter(P, info):
    jet(P, info, 0.9, 0.9, "fighter")


@model("bomber")
def bomber(P, info):
    jet(P, info, 1.35, 1.5, "bomber")


@model("recon_air")
def recon_air(P, info):
    jet(P, info, 0.8, 1.0, "recon")


@model("stealth_air")
def stealth_air(P, info):
    dark = mat("stealth", (0.18, 0.19, 0.22), 0.35, 0.3)
    out = [(0, 0.6), (-0.75, -0.42), (-0.5, -0.45), (-0.25, -0.3), (0, -0.45), (0.25, -0.3), (0.5, -0.45), (0.75, -0.42)]
    # flying wing: thick in the middle, thin at the tips
    import bmesh
    from kit import _bevel, _new_obj
    bm = bmesh.new()
    vb = [bm.verts.new((x, y, -0.03 * (1 - abs(x) / 0.8))) for x, y in out]
    vt = [bm.verts.new((x, y, 0.07 * (1 - abs(x) / 0.8) ** 1.5)) for x, y in out]
    bm.faces.new(list(reversed(vb)))
    bm.faces.new(vt)
    n = len(out)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((vb[i], vb[j], vt[j], vt[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _bevel(_new_obj("wing", bm, dark, "Body"), 0.008, 1)
    sphere(0.07, (0, 0.25, 0.05), mat("stealth_glass", (0.2, 0.25, 0.3), 0.1, 0.5), seg=12, rings=8, scale=(0.9, 1.8, 0.5), name="canopy")
    for s in (-1, 1):
        box((0.12, 0.08, 0.025), (s * 0.15, 0.02, 0.06), dark, bevel=0.01, name="intake")
        engine_glow(P, info, s * 0.15, -0.3, 0.03, 0.03)
    box((0.3, 0.03, 0.01), (0, -0.38, 0.03), P.team, bevel=0.002, name="team_trim")
    nav_lights(P, info, -0.74, 0.74, -0.42, 0.0)
    info.height = 0.3
    info.floating = True
    info.muzzle_forward = 0.5
    info.muzzle_height = 0.0


# ============================================================================================ helicopters

def rotor_blades(P, n, R, hub_z, part, chord=0.04):
    cyl(0.03, 0.06, (0, 0, hub_z), P.metal_dk, part=part, seg=10, name="rotor_hub")
    for i in range(n):
        a = i * 180.0 / n if n % 2 == 0 else i * 360.0 / n
        if n % 2 == 0 and i >= n // 2:
            break
        length = R * 2 if n % 2 == 0 else R
        off = 0 if n % 2 == 0 else R / 2
        ang = math.radians(a)
        box((chord, length, 0.008), (math.sin(ang) * off, math.cos(ang) * off, hub_z + 0.02), P.dark, part=part, bevel=0.002,
            rot=(0, 0, -a), name="blade")


@model("heli_attack")
def heli_attack(P, info):
    heli(P, info, 0.9, False)


@model("adv_heli")
def adv_heli(P, info):
    heli(P, info, 1.05, True)


def heli(P, info, L, advanced):
    # slim tandem gunship
    fuselage(P, L * 0.62, 0.11, z=-0.02, nose=0.35, tail=0.15, squash=1.25)
    box((0.07, 0.08, 0.06), (0, L * 0.2, 0.03), P.glass, bevel=0.02, name="front_canopy")
    box((0.08, 0.1, 0.07), (0, L * 0.08, 0.07), P.glass, bevel=0.02, name="rear_canopy")
    # tail boom with fin and spinning tail rotor
    cyl(0.035, L * 0.5, (0, -L * 0.42, 0.02), P.hull, axis="Y", seg=10, r2=0.02, name="tail_boom")
    poly_prism([(-L * 0.72, 0.0), (-L * 0.6, 0.0), (-L * 0.66, 0.15), (-L * 0.72, 0.15)], 0.012, loc=(0, 0, 0.02), material=P.hull_dk,
               axis="X", bevel=0.002, name="tail_fin")
    box((0.014, L * 0.05, 0.08), (0.0, -L * 0.69, 0.12), P.team, bevel=0.002, name="fin_team")
    tr = info.animate(SPIN_X, 2400, (0.02, -L * 0.68, 0.1))
    cyl(0.012, 0.02, (0.02, -L * 0.68, 0.1), P.metal_dk, axis="X", part=tr, seg=8, name="tail_hub")
    box((0.006, 0.16, 0.02), (0.025, -L * 0.68, 0.1), P.dark, part=tr, bevel=0, name="tail_blade")
    box((0.006, 0.02, 0.16), (0.025, -L * 0.68, 0.1), P.dark, part=tr, bevel=0, name="tail_blade")
    # stub wings with rocket pods and a chin gun
    box((0.56, 0.1, 0.02), (0, -0.02, -0.04), P.hull_dk, bevel=0.005, name="stub_wing")
    for s in (-1, 1):
        cyl(0.035, 0.2, (s * 0.24, -0.02, -0.08), P.team if advanced else P.metal, axis="Y", seg=10, name="rocket_pod")
        cyl(0.03, 0.005, (s * 0.24, 0.08, -0.08), P.dark, axis="Y", seg=10, bevel=0, name="pod_face")
    gun = info.animate(SPIN, 0.0, (0, L * 0.3, -0.1))
    box((0.05, 0.05, 0.04), (0, L * 0.3, -0.1), P.metal_dk, part=gun, bevel=0.006, name="chin_turret")
    cyl(0.008, 0.12, (0, L * 0.3 + 0.08, -0.105), P.metal_dk, axis="Y", part=gun, seg=6, name="chin_gun")
    # engine housing, mast and main rotor
    box((0.14, 0.24, 0.07), (0, -0.04, 0.12), P.hull, bevel=0.02, name="engines")
    for s in (-1, 1):
        engine_glow(P, info, s * 0.05, -0.16, 0.12, 0.018)
    cyl(0.02, 0.06, (0, 0.02, 0.17), P.metal_dk, seg=8, name="mast")
    rp = (0, 0.02, 0.2)
    info.pivots["Rotor"] = rp
    rotor_blades(P, 4 if advanced else 2, L * 0.62, 0.2, "Rotor")
    if advanced:
        sphere(0.04, (0, 0.02, 0.25), P.dark, seg=10, rings=6, name="mast_radar")
    nav_lights(P, info, -0.28, 0.28, -0.02, -0.04)
    info.height = 0.35
    info.floating = True
    info.muzzle_forward = L * 0.45
    info.muzzle_height = -0.15


@model("transport_air")
def transport_air(P, info):
    # tandem-rotor transport: each rotor spins around its own hub
    box((0.36, 1.1, 0.3), (0, 0, 0.0), P.hull, bevel=0.06, seg=3, name="cabin")
    sphere(0.17, (0, 0.55, 0.0), P.hull, seg=14, rings=8, scale=(1.05, 0.6, 0.9), name="nose")
    box((0.26, 0.05, 0.1), (0, 0.66, 0.06), P.glass, bevel=0.015, rot=(-30, 0, 0), name="windscreen")
    for s in (-1, 1):
        for k in range(4):
            box((0.012, 0.05, 0.05), (s * 0.18, 0.3 - k * 0.15, 0.04), P.glass, bevel=0.005, name="window")
        cyl(0.06, 0.4, (s * 0.21, -0.25, -0.12), P.hull_dk, axis="Y", seg=12, name="sponson")
    box((0.2, 0.38, 0.012), (0, 0.0, 0.155), P.team, bevel=0.002, name="team_roof")
    box((0.3, 0.06, 0.18), (0, -0.55, -0.02), P.hull_dk, bevel=0.01, rot=(25, 0, 0), name="rear_ramp")
    for y, z, h in ((0.42, 0.2, 0.08), (-0.45, 0.28, 0.2)):
        box((0.16, 0.2, h), (0, y, z - h / 2 + 0.02), P.hull_dk, bevel=0.02, name="rotor_pylon")
        rotor = info.animate(SPIN, 1400 if y > 0 else -1400, (0, y, z + 0.03))
        rotor_blades(P, 3, 0.5, z + 0.03, rotor)
    for s in (-1, 1):
        engine_glow(P, info, s * 0.1, -0.56, 0.2, 0.025)
    nav_lights(P, info, -0.2, 0.2, 0.0, -0.1)
    info.height = 0.5
    info.floating = True
    info.muzzle_forward = 0.6
    info.muzzle_height = 0.0


# ============================================================================================ ships

def ship_hull(P, L, W, h=0.18):
    col = mat("ship_hull", (0.45, 0.5, 0.55) if P.ca else (0.32, 0.32, 0.30), 0.6, 0.2)
    deck = mat("deck", (0.36, 0.38, 0.4) if P.ca else (0.36, 0.3, 0.24), 0.8)
    bottom = [(-W * 0.38, -L * 0.48), (W * 0.38, -L * 0.48), (W * 0.42, -L * 0.2), (W * 0.38, L * 0.2), (W * 0.16, L * 0.42), (0, L * 0.5),
              (-W * 0.16, L * 0.42), (-W * 0.38, L * 0.2), (-W * 0.42, -L * 0.2)]
    top = [(-W * 0.45, -L * 0.5), (W * 0.45, -L * 0.5), (W * 0.5, -L * 0.2), (W * 0.47, L * 0.2), (W * 0.22, L * 0.44), (0, L * 0.53),
           (-W * 0.22, L * 0.44), (-W * 0.47, L * 0.2), (-W * 0.5, -L * 0.2)]
    hull(bottom, top, -0.04, h, material=col, bevel=0.01, name="hull")
    hull(top, [(x * 0.97, y * 0.98) for x, y in top], h, h + 0.008, material=deck, bevel=0.0, name="deck")
    box((W * 0.9, L * 0.9, 0.025), (0, 0, 0.0), mat("boot_top", (0.5, 0.15, 0.12), 0.7), bevel=0.0, name="waterline")
    return h + 0.008


def ship_gun_turret(P, info, piv, ts, barrels, part):
    x, y, z = piv
    if P.ca:
        hull(octo(ts * 1.6, ts * 1.8, ts * 0.4), octo(ts * 1.3, ts * 1.3, ts * 0.3), 0.0, ts * 0.8, loc=(x, y, z), material=P.hull_lt,
             part=part, bevel=0.01, name="gun_house")
    else:
        box((ts * 1.6, ts * 1.8, ts * 0.8), (x, y, z + ts * 0.4), P.hull, part=part, bevel=0.01, name="gun_house")
        rivet_row(P, (x - ts * 0.7, y + ts * 0.91, z + ts * 0.6), (x + ts * 0.7, y + ts * 0.91, z + ts * 0.6), 5, axis_normal="Y")
    for i in range(barrels):
        bx = x + (i - (barrels - 1) * 0.5) * ts * 0.45
        barrel(P, bx, y + ts * 0.8, ts * 2.4, ts * 0.13, z + ts * 0.45, part=part, brake=False, sleeve=False, info=info, recoil=ts * 0.4)


def superstructure(P, info, L, W, deck, cls):
    bh = 0.18 + cls * 0.05
    sup = P.hull if P.ca else P.hull
    if P.ca:
        hull(octo(W * 0.5, L * 0.24, 0.05), octo(W * 0.4, L * 0.18, 0.04), deck, deck + bh, loc=(0, -L * 0.08, 0), material=sup, bevel=0.01,
             name="superstructure")
    else:
        box((W * 0.5, L * 0.24, bh), (0, -L * 0.08, deck + bh / 2), sup, bevel=0.01, name="superstructure")
        box((W * 0.4, L * 0.12, 0.06), (0, -L * 0.12, deck + bh + 0.03), sup, bevel=0.008, name="upper_deck")
    box((W * 0.36, 0.02, 0.04), (0, -L * 0.08 + L * 0.1, deck + bh - 0.03), P.glass, bevel=0.004, name="bridge_windows")
    # mast with spinning radar, flag and lights
    mx, my = 0, -L * 0.15
    cyl(0.014, 0.3 + cls * 0.06, (mx, my, deck + bh + (0.3 + cls * 0.06) / 2), P.metal, seg=6, name="mast")
    top = deck + bh + 0.3 + cls * 0.06
    box((0.16, 0.012, 0.012), (mx, my, top - 0.08), P.metal, bevel=0, name="yard")
    radar = info.animate(SPIN, 120, (mx, my, top - 0.03))
    box((0.16, 0.02, 0.04), (mx, my, top - 0.03), P.metal_dk, part=radar, bevel=0.004, name="radar")
    light = info.animate(BLINK, 0.7, (mx, my, top + 0.01))
    sphere(0.012, (mx, my, top + 0.01), mat("mast_light", (1.0, 0.3, 0.2), 0.3, 0.0, emit=3.0), seg=6, rings=4, part=light, name="mast_light")
    flag_part = info.animate(WAVE, 18, (mx, my, top - 0.12))
    box((0.004, 0.1, 0.06), (mx, my - 0.05, top - 0.12), P.team, part=flag_part, bevel=0, name="ensign")
    if cls >= 2:
        cyl(0.06, 0.15, (0, -L * 0.26, deck + 0.075), P.dark, seg=12, name="funnel")
        cyl(0.065, 0.02, (0, -L * 0.26, deck + 0.15), P.team, seg=12, bevel=0, name="funnel_band")
    return deck + bh


def ship(P, info, L, W, cls):
    deck = ship_hull(P, L, W)
    box((W * 0.5, L * 0.12, 0.01), (0, -L * 0.36, deck + 0.005), P.team, bevel=0.0, name="team_deck")
    superstructure(P, info, L, W, deck, cls)
    ts = 0.12 + cls * 0.03
    barrels = 1 if cls in (0, 4) else 3 if cls >= 3 else 2
    piv = (0, L * 0.22, deck)
    info.pivots["Turret"] = piv
    ship_gun_turret(P, info, piv, ts, barrels, "Turret")
    if cls >= 2:
        piv2 = (0, -L * 0.36, deck)
        info.pivots["Extra"] = piv2
        # rear turret faces backwards (the game turns Extra by 180 degrees)
        ship_gun_turret(P, info, piv2, ts * 0.85, 2, "Extra")
    if cls == 4:
        # tender: helipad and supply cranes
        box((W * 0.6, L * 0.25, 0.02), (0, -L * 0.3, deck + 0.01), mat("helipad", (0.3, 0.32, 0.3), 0.9), bevel=0, name="helipad")
        box((0.18, 0.012, 0.004), (0, -L * 0.3, deck + 0.022), P.white, bevel=0, name="pad_mark")
        crane = info.animate(SPIN, 20, (W * 0.25, L * 0.05, deck))
        cyl(0.025, 0.2, (W * 0.25, L * 0.05, deck + 0.1), P.yellow, part=crane, seg=8, name="crane_post")
        box((0.03, 0.3, 0.03), (W * 0.25, L * 0.05 + 0.12, deck + 0.2), P.yellow, part=crane, bevel=0.004, rot=(-15, 0, 0), name="crane_jib")
    for s in (-1, 1):
        rivet_row(P, (s * W * 0.48, -L * 0.4, 0.12), (s * W * 0.48, L * 0.2, 0.12), 8, axis_normal="X", material=P.metal_dk) if not P.ca else None
    info.height = 0.5 + cls * 0.08
    info.muzzle_forward = ts * 3.2
    info.muzzle_height = deck + ts * 0.45
    info.floating = True


for _k, _L, _W, _c in (("patrol", 1.1, 0.42, 0), ("destroyer", 1.7, 0.55, 1), ("cruiser", 2.1, 0.65, 2), ("battleship", 2.6, 0.8, 3),
                       ("tender", 1.5, 0.6, 4)):
    model(_k)((lambda L, W, c: (lambda P, info: ship(P, info, L, W, c)))(_L, _W, _c))


@model("lst")
def lst(P, info):
    L, W = 1.6, 0.75
    deck = ship_hull(P, L, W, h=0.2)
    # well deck walls, bow doors and rear bridge
    for s in (-1, 1):
        box((0.04, L * 0.7, 0.08), (s * W * 0.44, 0.05, deck + 0.04), P.hull_dk, bevel=0.006, name="well_wall")
    box((W * 0.7, 0.05, 0.14), (0, L * 0.47, deck + 0.05), P.dark, bevel=0.01, rot=(-15, 0, 0), name="bow_ramp")
    box((W * 0.45, L * 0.35, 0.02), (0, 0.1, deck + 0.012), P.team, bevel=0, name="team_deck")
    box((W * 0.6, 0.3, 0.25), (0, -L * 0.38, deck + 0.125), P.hull, bevel=0.012, name="bridge")
    box((W * 0.45, 0.02, 0.05), (0, -L * 0.38 + 0.15, deck + 0.2), P.glass, bevel=0.004, name="bridge_windows")
    radar = info.animate(SPIN, 100, (0, -L * 0.4, deck + 0.35))
    cyl(0.012, 0.1, (0, -L * 0.4, deck + 0.3), P.metal, seg=6, name="mast")
    box((0.14, 0.02, 0.035), (0, -L * 0.4, deck + 0.36), P.metal_dk, part=radar, bevel=0.004, name="radar")
    flag_part = info.animate(WAVE, 18, (0.1, -L * 0.48, deck + 0.3))
    cyl(0.006, 0.3, (0.1, -L * 0.48, deck + 0.15), P.metal, seg=5, name="flagstaff")
    box((0.004, 0.1, 0.06), (0.1, -L * 0.48 - 0.05, deck + 0.27), P.team, part=flag_part, bevel=0, name="ensign")
    info.height = 0.6
    info.floating = True


@model("sub")
def sub(P, info):
    c = mat("sub_hull", (0.16, 0.18, 0.2), 0.5, 0.3)
    fuselage(P, 1.5, 0.2, z=0.0, nose=0.2, tail=0.3, material=c, squash=0.55)
    box((0.1, 0.3, 0.2), (0, 0.15, 0.17), c, bevel=0.04, seg=2, name="sail")
    box((0.3, 0.06, 0.012), (0, 0.18, 0.2), c, bevel=0.004, name="sail_planes")
    box((0.11, 0.2, 0.01), (0, 0.15, 0.275), P.team, bevel=0.002, name="team_sail")
    peri = info.animate(BOB, 0.04, (0.02, 0.2, 0.27))
    cyl(0.008, 0.08, (0.02, 0.2, 0.31), P.metal, part=peri, seg=6, name="periscope")
    prop = info.animate(SPIN_X, 600, (0, -0.76, 0.0))
    for k in range(3):
        box((0.008, 0.012, 0.09), (0, -0.76, 0.0), P.metal, part=prop, bevel=0, rot=(k * 120, 0, 0), name="prop_blade")
    for s in (-1, 1):
        box((0.22, 0.06, 0.01), (0, -0.62, 0.0), c, bevel=0.003, rot=(0, 90 * (s + 1) / 2, 0), name="stern_plane")
    info.height = 0.4
    info.muzzle_forward = 0.8
    info.muzzle_height = 0.0
    info.floating = True
