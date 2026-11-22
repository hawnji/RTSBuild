"""Structures. Centred on their footprint (W cells along X, H cells along Y), entrance facing -Y.
Core parts keep the game's existing behaviour: Turret (defences aim it), Extra (construction yard crane, repair arm,
radar dish and superweapon pieces are driven by the game)."""
import math

from kit import (BLINK, BOB, RECOIL, ROCK, SPIN, SPIN_X, TREAD_BOTTOM, WAVE, box, cyl, hull, mat, octo, poly_prism, sphere)
from models import antenna, barrel, door, flag, foundation, mat_glow, mat_red, model, rivet_row, sandbags, window_row


# ============================================================================================ helpers

def block(P, x, y, z, w, d, h, m=None, roof=None, bevel=0.015, name="block"):
    """Building block with an optional contrasting roof slab. Returns top height."""
    box((w, d, h), (x, y, z + h / 2), m or P.hull, bevel=bevel, name=name)
    if roof is not False:
        box((w + 0.03, d + 0.03, 0.03), (x, y, z + h + 0.015), roof or P.hull_dk, bevel=0.006, name=name + "_roof")
    return z + h + (0.03 if roof is not False else 0)


def team_stripe(P, x, y, z, w, d):
    box((w, d, 0.02), (x, y, z + 0.01), P.team, bevel=0.002, name="team_stripe")


def lamp(P, info, x, y, z, rate=0.6, phase=0.0, colour=(1.0, 0.25, 0.15)):
    m = mat("lamp_%d%d%d" % tuple(int(c * 9) for c in colour), colour, 0.3, 0.0, emit=3.0)
    part = info.animate(BLINK, rate, (x, y, z), phase=phase)
    sphere(0.018, (x, y, z), m, seg=8, rings=6, part=part, name="lamp")


def vent_fan(P, info, x, y, z, r=0.08, speed=300):
    cyl(r * 1.15, 0.04, (x, y, z + 0.02), P.metal_dk, seg=16, name="fan_housing")
    fan = info.animate(SPIN, speed, (x, y, z + 0.045))
    cyl(r * 0.25, 0.012, (x, y, z + 0.045), P.metal, part=fan, seg=10, name="fan_hub")
    for k in range(4):
        box((r * 1.9, r * 0.32, 0.006), (x, y, z + 0.045), P.metal, part=fan, bevel=0, rot=(0, 12, k * 45), name="fan_blade")


def stack(P, x, y, z, r, h, m=None, band=True):
    cyl(r, h, (x, y, z + h / 2), m or P.concrete, seg=14, r2=r * 0.85, name="stack")
    cyl(r * 0.88, 0.03, (x, y, z + h), P.metal_dk, seg=14, name="stack_rim")
    if band:
        cyl(r * 0.95, 0.04, (x, y, z + h * 0.8), P.team, seg=14, bevel=0, name="stack_band")


def pipe(P, a, b, r=0.02, m=None):
    from kit import capsule
    capsule(a, b, r, m or P.metal, name="pipe")


def tank(P, x, y, z, r, h, m=None):
    cyl(r, h, (x, y, z + h / 2), m or P.hull_lt, seg=18, name="tank")
    sphere(r, (x, y, z + h), m or P.hull_lt, seg=18, rings=8, scale=(1, 1, 0.4), half=True, name="tank_cap")
    for k in (0.3, 0.7):
        cyl(r * 1.02, 0.02, (x, y, z + h * k), P.metal_dk, seg=18, bevel=0, name="tank_band")


def garage_door(P, x, y, z, w, h):
    box((w + 0.06, 0.04, h + 0.04), (x, y, z + h / 2), P.dark, bevel=0.008, name="door_frame")
    box((w, 0.045, h), (x, y - 0.004, z + h / 2), P.metal, bevel=0.004, name="garage_door")
    for i in range(int(h / 0.05)):
        box((w * 0.98, 0.05, 0.006), (x, y - 0.006, z + 0.03 + i * 0.05), P.metal_dk, bevel=0, name="door_rib")
    for s in (-1, 1):
        box((0.03, 0.05, 0.2), (x + s * (w / 2 + 0.05), y - 0.004, z + 0.1), P.yellow, bevel=0.004, name="hazard_post")


def dish(P, x, y, z, r, part="Body", tilt=-40, m=None):
    d = cyl(r, r * 0.25, (x, y, z), m or P.white, part=part, seg=18, r2=r * 0.55, name="dish")
    d.rotation_euler = (math.radians(tilt), 0, 0)
    cyl(r * 0.06, r * 0.6, (x, y + r * 0.2, z + r * 0.15), P.metal_dk, part=part, seg=6, name="dish_feed")


def helipad(P, x, y, z, r):
    cyl(r, 0.02, (x, y, z + 0.01), P.concrete_dk, seg=24, name="pad")
    cyl(r * 0.85, 0.024, (x, y, z + 0.012), mat("pad_ring", (0.92, 0.85, 0.3), 0.6), seg=24, bevel=0, cap=False, name="pad_ring")
    box((0.05, r * 0.9, 0.006), (x - r * 0.25, y, z + 0.024), P.white, bevel=0, name="pad_h")
    box((0.05, r * 0.9, 0.006), (x + r * 0.25, y, z + 0.024), P.white, bevel=0, name="pad_h")
    box((r * 0.5, 0.05, 0.006), (x, y, z + 0.024), P.white, bevel=0, name="pad_h")


# ============================================================================================ base

@model("ConstructionYard", footprint=(3, 3))
def construction_yard(P, info):
    W = H = 3.0
    foundation(P, W, H)
    b = 0.08
    if P.ca:
        t = block(P, -0.35, 0.15, b, 1.5, 1.4, 0.55)
        hull(octo(1.1, 1.0, 0.15), octo(0.8, 0.7, 0.12), t, t + 0.25, loc=(-0.35, 0.15, 0), material=P.hull_lt, name="upper_deck")
        box((1.0, 0.03, 0.12), (-0.35, 0.15 - 0.7, b + 0.4), P.glass, bevel=0.006, name="front_glass")
        team_stripe(P, -0.35, 0.15, t + 0.25, 0.7, 0.12)
        vent_fan(P, info, 0.1, 0.5, t)
        vent_fan(P, info, -0.8, 0.5, t, speed=-260)
    else:
        t = block(P, -0.35, 0.15, b, 1.6, 1.45, 0.6, m=P.concrete_dk)
        box((1.1, 1.0, 0.3), (-0.35, 0.25, t + 0.15), P.hull, bevel=0.012, name="upper_block")
        rivet_row(P, (-1.1, -0.58, b + 0.5), (0.4, -0.58, b + 0.5), 12, axis_normal="Y")
        box((1.62, 0.12, 0.08), (-0.35, -0.58, b + 0.5), P.team, bevel=0.006, name="team_band")
        stack(P, -0.9, 0.6, t, 0.08, 0.5)
    garage_door(P, -0.35, 0.15 - 0.73, b, 0.6, 0.38)
    # control tower
    tx, ty = 0.85, -0.7
    block(P, tx, ty, b, 0.4, 0.4, 0.9, m=P.hull_dk)
    box((0.42, 0.42, 0.14), (tx, ty, b + 0.85), P.glass, bevel=0.01, name="tower_glass")
    box((0.48, 0.48, 0.04), (tx, ty, b + 0.94), P.dark, bevel=0.008, name="tower_cap")
    antenna(P, tx + 0.1, ty, b + 0.96, 0.4, part="Body")
    lamp(P, info, tx + 0.1, ty, b + 1.37, rate=0.5)
    flag(P, info, -1.3, -1.3, b, 0.8)
    # crane on Extra (the game swings it slowly)
    ep = (0.95, 0.9, 0.1)
    info.pivots["Extra"] = ep
    info.extra_spins = True
    y_ = mat("crane", (0.92, 0.72, 0.15), 0.5)
    for k in range(6):
        box((0.12, 0.12, 0.2), (ep[0], ep[1], ep[2] + 0.1 + k * 0.22), y_, part="Extra", bevel=0.006, name="mast_section")
        box((0.13, 0.13, 0.015), (ep[0], ep[1], ep[2] + 0.2 + k * 0.22), P.metal_dk, part="Extra", bevel=0, name="mast_ring")
    box((0.08, 1.4, 0.08), (ep[0], ep[1] + 0.45, ep[2] + 1.42), y_, part="Extra", bevel=0.008, name="jib")
    box((0.2, 0.25, 0.18), (ep[0], ep[1] - 0.3, ep[2] + 1.42), P.dark, part="Extra", bevel=0.01, name="counterweight")
    box((0.14, 0.14, 0.12), (ep[0], ep[1], ep[2] + 1.36), P.glass, part="Extra", bevel=0.01, name="crane_cab")
    cyl(0.004, 0.55, (ep[0], ep[1] + 1.05, ep[2] + 1.12), P.metal_dk, part="Extra", seg=4, bevel=0, name="cable")
    box((0.06, 0.06, 0.05), (ep[0], ep[1] + 1.05, ep[2] + 0.83), P.metal_dk, part="Extra", bevel=0.008, name="hook")
    info.height = 1.6


@model("Power", footprint=(2, 2))
def power(P, info):
    plant(P, info, 2.0, 2.0, False)


@model("AdvancedPower", footprint=(3, 2))
def adv_power(P, info):
    plant(P, info, 3.0, 2.0, True)


def plant(P, info, W, H, adv):
    foundation(P, W, H)
    b = 0.08
    if P.ca:
        # fusion-style generator halls with glowing cores
        n = 2 if adv else 1
        for i in range(n):
            x = (i - (n - 1) / 2) * 1.1
            t = block(P, x, 0.25, b, 0.9, 0.9, 0.3)
            cyl(0.32, 0.4, (x, 0.25, t + 0.2), P.hull_lt, seg=24, r2=0.26, name="reactor_dome")
            sphere(0.26, (x, 0.25, t + 0.4), P.hull_lt, seg=24, rings=10, half=True, scale=(1, 1, 0.6), name="dome_cap")
            glow = info.animate(BOB, 0.03, (x, 0.25, t + 0.1), phase=i * 0.3)
            cyl(0.34, 0.05, (x, 0.25, t + 0.1), mat_glow(P), part=glow, seg=24, bevel=0, name="core_ring")
            team_stripe(P, x, 0.25 - 0.46, b + 0.2, 0.5, 0.02)
            vent_fan(P, info, x + 0.33, -0.15, t, r=0.07, speed=420)
        for s in (-1, 1):
            for k in range(3 if adv else 2):
                box((0.1, 0.06, 0.4), (s * (W * 0.42), -0.6 + k * 0.4, b + 0.2), P.metal, bevel=0.01, name="pylon")
                box((0.2, 0.02, 0.02), (s * (W * 0.42), -0.6 + k * 0.4, b + 0.38), P.metal_dk, bevel=0, name="insulator")
        door(P, 0, 0.25 - 0.46, b, 0.18, 0.2)
    else:
        # coal-and-turbine plant: cooling towers with a turbine hall
        n = 2 if adv else 1
        for i in range(n):
            x = (i - (n - 1) / 2) * 1.2
            cyl(0.42, 0.9, (x - 0.1, 0.35, b + 0.45), P.concrete, seg=24, r2=0.3, name="cooling_tower")
            cyl(0.31, 0.06, (x - 0.1, 0.35, b + 0.93), P.concrete_dk, seg=24, name="tower_lip")
            cyl(0.43, 0.06, (x - 0.1, 0.35, b + 0.25), P.team, seg=24, bevel=0, name="team_band")
            steam = info.animate(BOB, 0.06, (x - 0.1, 0.35, b + 0.98), phase=0.4 * i)
            sphere(0.22, (x - 0.1, 0.35, b + 1.02), mat("steam", (0.85, 0.85, 0.85), 1.0), part=steam, seg=12, rings=6, scale=(1, 1, 0.45), name="steam")
        t = block(P, 0.0, -0.5, b, W * 0.7, 0.55, 0.35, m=P.hull, roof=P.accent)
        rivet_row(P, (-W * 0.33, -0.78, b + 0.3), (W * 0.33, -0.78, b + 0.3), 10, axis_normal="Y")
        turbine = info.animate(SPIN_X, 220, (W * 0.38, -0.5, b + 0.15))
        cyl(0.12, 0.04, (W * 0.38, -0.5, b + 0.15), P.metal, axis="X", part=turbine, seg=12, name="flywheel")
        for k in range(4):
            box((0.012, 0.22, 0.03), (W * 0.38 + 0.025, -0.5, b + 0.15), P.metal_dk, part=turbine, bevel=0, rot=(k * 45, 0, 0), name="spoke")
        stack(P, -W * 0.38, -0.5, b, 0.07, 0.8)
        door(P, -0.2, -0.79, b, 0.18, 0.2)
    lamp(P, info, W * 0.45, -H * 0.45, b + 0.04, rate=0.8, colour=(1.0, 0.8, 0.2))
    info.height = 1.2


@model("Refinery", footprint=(3, 3))
def refinery(P, info):
    W = H = 3.0
    foundation(P, W, H)
    b = 0.08
    m = P.hull if P.ca else P.concrete_dk
    t = block(P, -0.5, 0.35, b, 1.6, 1.4, 0.5, m=m)
    tank(P, 0.85, 0.9, b, 0.3, 0.6)
    tank(P, 0.85, 0.25, b, 0.24, 0.5)
    pipe(P, (0.55, 0.9, b + 0.4), (0.3, 0.9, b + 0.4))
    pipe(P, (0.6, 0.25, b + 0.35), (0.3, 0.25, b + 0.35))
    stack(P, -1.0, 0.85, t, 0.09, 0.55, band=False)
    team_stripe(P, -0.5, 0.35, t, 1.2, 0.15)
    # unloading dock with a conveyor that runs into the building
    box((0.8, 0.9, 0.06), (0.55, -0.9, b + 0.03), P.concrete_dk, bevel=0.01, name="dock_pad")
    for s in (-1, 1):
        box((0.05, 0.9, 0.12), (0.55 + s * 0.42, -0.9, b + 0.06), P.yellow, bevel=0.006, name="dock_rail")
    box((0.28, 1.0, 0.05), (-0.1, -0.55, b + 0.12), P.metal_dk, bevel=0.006, rot=(8, 0, 0), name="conveyor")
    belt = info.animate(TREAD_BOTTOM, 0.08, (-0.1, -0.55, b + 0.15))
    for k in range(12):
        box((0.26, 0.02, 0.012), (-0.1, -1.0 + k * 0.08, b + 0.15 + (k * 0.08 - 0.45) * math.tan(math.radians(8))), P.metal, part=belt,
            bevel=0, name="belt_slat")
    ore = mat("ore", (0.85, 0.7, 0.25), 0.4, 0.3)
    for k in range(5):
        sphere(0.07, (0.85 + (k % 2) * 0.1 - 0.05, -1.25 + k * 0.08, b + 0.05), ore, seg=8, rings=5, scale=(1, 1, 0.6), name="ore_pile")
    garage_door(P, -0.5, 0.35 - 0.72, b, 0.5, 0.3)
    lamp(P, info, 0.55, -1.38, b + 0.15, rate=1.0, colour=(1.0, 0.8, 0.2))
    lamp(P, info, 0.15, -1.38, b + 0.15, rate=1.0, phase=0.5, colour=(1.0, 0.8, 0.2))
    vent_fan(P, info, -0.1, 0.7, t)
    info.height = 1.2


@model("Silo", footprint=(1, 1))
def silo(P, info):
    foundation(P, 1.0, 1.0)
    b = 0.08
    for x, y in ((-0.2, -0.2), (0.2, -0.2), (-0.2, 0.2), (0.2, 0.2)):
        tank(P, x, y, b, 0.17, 0.42, m=P.hull_lt if P.ca else P.hull)
    box((0.06, 0.6, 0.04), (0, 0, b + 0.5), P.metal_dk, bevel=0.006, name="catwalk")
    team_stripe(P, 0, 0, b + 0.52, 0.2, 0.2)
    gauge = info.animate(BOB, 0.03, (0.36, -0.36, b + 0.2))
    box((0.04, 0.02, 0.1), (0.36, -0.37, b + 0.2), mat_glow(P, (0.95, 0.75, 0.2)), part=gauge, bevel=0, name="fill_gauge")
    info.height = 0.8


# ============================================================================================ production

@model("AdvancedBarracks", footprint=(2, 3))
def adv_barracks(P, info):
    W, H = 2.0, 3.0
    foundation(P, W, H)
    b = 0.08
    if P.ca:
        t = block(P, 0, 0.3, b, 1.5, 1.8, 0.5)
        hull(octo(1.2, 1.4, 0.2), octo(1.0, 1.1, 0.2), t, t + 0.18, loc=(0, 0.3, 0), material=P.hull_lt, name="upper")
        box((1.2, 0.03, 0.1), (0, 0.3 - 0.91, b + 0.36), P.glass, bevel=0.006, name="glass_band")
        helipad(P, 0, 0.45, t + 0.18, 0.4)
        lamp(P, info, 0.55, 0.0, t + 0.2, rate=0.7, colour=(0.3, 0.8, 1.0))
        lamp(P, info, -0.55, 0.0, t + 0.2, rate=0.7, phase=0.5, colour=(0.3, 0.8, 1.0))
    else:
        t = block(P, 0, 0.3, b, 1.6, 1.9, 0.55, m=P.concrete_dk)
        poly_prism([(-0.85, 0), (0.85, 0), (0, 0.3)], 1.95, loc=(0, 0.3, t), material=P.hull, axis="Y", bevel=0.01, name="roof")
        for x in (-0.82, 0.82):
            box((0.12, 0.12, 0.9), (x, -0.65, b + 0.45), P.concrete, bevel=0.012, name="gate_tower")
            box((0.16, 0.16, 0.05), (x, -0.65, b + 0.92), P.hull_dk, bevel=0.008, name="tower_cap")
        for x in (-0.4, 0.4):
            box((0.24, 0.012, 0.36), (x, 0.3 - 0.96, b + 0.3), P.team, bevel=0.002, name="banner")
        stack(P, 0.55, 0.9, t, 0.06, 0.4)
    door(P, 0, 0.3 - 0.92, b, 0.3, 0.3)
    sandbags(P, -0.95, -1.4, -0.3, -1.4)
    sandbags(P, 0.3, -1.4, 0.95, -1.4)
    flag(P, info, 0.85, -1.2, b, 0.8)
    info.height = 1.2


@model("Factory", footprint=(3, 3))
def factory(P, info):
    plant_factory(P, info, 3.0, 3.0, False)


@model("AdvancedFactory", footprint=(3, 4))
def adv_factory(P, info):
    plant_factory(P, info, 3.0, 4.0, True)


def plant_factory(P, info, W, H, adv):
    foundation(P, W, H)
    b = 0.08
    hh = 0.85 if not adv else 1.0
    m = P.hull if P.ca else P.concrete_dk
    t = block(P, 0, 0.15, b, W * 0.8, H * 0.68, hh * 0.8, m=m, roof=False)
    # saw-tooth roof
    n = 4 if not adv else 5
    d = H * 0.68 / n
    for i in range(n):
        y = 0.15 - H * 0.34 + d * (i + 0.5)
        poly_prism([(y - d / 2, 0), (y + d / 2, 0), (y + d / 2, 0.22)], W * 0.82, loc=(0, 0, t), material=P.hull_dk if P.ca else P.hull,
                   axis="X", bevel=0.006, name="sawtooth")
        box((W * 0.78, 0.015, 0.18), (0, y + d / 2 - 0.006, t + 0.1), P.glass, bevel=0, name="skylight")
    garage_door(P, 0, 0.15 - H * 0.34 - 0.02, b, W * 0.4, hh * 0.55)
    team_stripe(P, 0, 0.15 - H * 0.34 - 0.03, b + hh * 0.7, W * 0.5, 0.04)
    # vehicle exit apron with flashing warning lights
    box((W * 0.5, 0.55, 0.012), (0, -H * 0.36, b + 0.006), P.concrete_dk, bevel=0, name="apron")
    for k in range(5):
        box((0.06, 0.4, 0.004), (-W * 0.2 + k * W * 0.1, -H * 0.36, b + 0.014), P.yellow, bevel=0, rot=(0, 0, 35), name="hazard_stripe")
    lamp(P, info, -W * 0.25, 0.15 - H * 0.34 - 0.04, b + hh * 0.62, rate=1.5, colour=(1.0, 0.6, 0.1))
    lamp(P, info, W * 0.25, 0.15 - H * 0.34 - 0.04, b + hh * 0.62, rate=1.5, phase=0.5, colour=(1.0, 0.6, 0.1))
    # rooftop gantry crane that rocks back and forth
    gx = W * 0.32
    for y in (-H * 0.2, H * 0.3):
        box((0.06, 0.06, 0.5), (gx, y, t + 0.25), P.yellow, bevel=0.006, name="gantry_leg")
    box((0.06, H * 0.55, 0.06), (gx, H * 0.05, t + 0.5), P.yellow, bevel=0.006, name="gantry_beam")
    trolley = info.animate(ROCK, 0.0, (gx, H * 0.05, t + 0.5))
    box((0.12, 0.12, 0.08), (gx, H * 0.05, t + 0.45), P.dark, part=trolley, bevel=0.008, name="trolley")
    if P.ca:
        vent_fan(P, info, -W * 0.25, H * 0.25, t)
        vent_fan(P, info, -W * 0.05, H * 0.25, t, speed=-300)
    else:
        stack(P, -W * 0.3, H * 0.28, t, 0.09, 0.6)
        stack(P, -W * 0.12, H * 0.28, t, 0.07, 0.45, band=False)
    if adv:
        tank(P, W * 0.42, H * 0.38, b, 0.16, 0.4)
        antenna(P, -W * 0.38, -H * 0.1, t, 0.4, part="Body")
        lamp(P, info, -W * 0.38, -H * 0.1, t + 0.42, rate=0.5)
    info.height = 1.3 if adv else 1.1


@model("RepairDepot", footprint=(3, 3))
def repair_depot(P, info):
    W = H = 3.0
    foundation(P, W, H)
    b = 0.08
    # service pad with corner posts
    cyl(0.9, 0.05, (0, 0, b + 0.025), P.concrete_dk, seg=8, name="service_pad")
    for k in range(8):
        a = k * math.tau / 8
        box((0.2, 0.04, 0.006), (math.cos(a) * 0.75, math.sin(a) * 0.75, b + 0.053), P.yellow, bevel=0, rot=(0, 0, math.degrees(a) + 90),
            name="pad_mark")
    for x, y in ((-1.1, -1.1), (1.1, -1.1), (-1.1, 1.1), (1.1, 1.1)):
        box((0.12, 0.12, 0.7), (x, y, b + 0.35), P.hull_dk if P.ca else P.concrete, bevel=0.012, name="post")
        lamp(P, info, x, y, b + 0.73, rate=1.2, phase=(x + y) * 0.1, colour=(0.3, 1.0, 0.4))
    t = block(P, 0, 1.2, b, 1.6, 0.4, 0.4, m=P.hull if P.ca else P.concrete_dk)
    team_stripe(P, 0, 1.0, b + 0.3, 1.2, 0.02)
    # overhead service arm on Extra (the game spins it while repairing)
    info.pivots["Extra"] = (0, 0, 0.75)
    info.extra_spins = True
    cyl(0.06, 0.66, (0, 1.2, b + 0.33 + 0.4), P.metal_dk, seg=10, name="arm_column")
    cyl(0.1, 0.08, (0, 0, 0.78), P.metal, part="Extra", seg=14, name="arm_hub")
    for s in (-1, 1):
        box((0.06, 0.8, 0.05), (0, s * 0.4, 0.78), P.yellow, part="Extra", bevel=0.006, name="arm")
        cyl(0.04, 0.18, (0, s * 0.78, 0.68), P.metal_dk, part="Extra", seg=10, name="tool_head")
        sphere(0.025, (0, s * 0.78, 0.58), mat_glow(P, (0.4, 0.9, 1.0)), part="Extra", seg=8, rings=6, name="welder")
    info.height = 1.0


@model("Hangar", footprint=(3, 2))
def hangar(P, info):
    airfield(P, info, 3.0, 2.0, False)


@model("AdvancedHangar", footprint=(3, 3))
def adv_hangar(P, info):
    airfield(P, info, 3.0, 3.0, True)


def airfield(P, info, W, H, adv):
    foundation(P, W, H)
    b = 0.08
    # arched hangar on the left, landing pads on the right
    hx = -W * 0.22
    arch = [(0.55 * math.cos(math.pi * k / 16), 0.42 * math.sin(math.pi * k / 16)) for k in range(17)]
    poly_prism(arch, H * 0.8, loc=(hx, 0.0, b), material=P.hull if P.ca else P.hull_dk, axis="Y", bevel=0.008, seg=1, name="hangar", smooth=True)
    box((0.9, 0.03, 0.3), (hx, -H * 0.4 - 0.01, b + 0.15), P.dark, bevel=0.006, name="hangar_mouth")
    team_stripe(P, hx, 0.0, b + 0.42, 0.08, H * 0.7)
    pads = 2 if not adv else 4
    for i in range(pads):
        px = W * 0.25 if pads == 2 else W * (0.12 + 0.26 * (i % 2))
        py = (-H * 0.22 + H * 0.44 * (i // 2 if pads == 4 else i)) if pads == 4 else (-H * 0.22 + H * 0.44 * i)
        helipad(P, px, py, b, 0.32)
        lamp(P, info, px + 0.3, py - 0.3, b + 0.03, rate=0.9, phase=i * 0.25, colour=(0.3, 0.9, 0.3))
    # control mast with spinning beacon
    mx, my = -W * 0.45, H * 0.38
    cyl(0.03, 0.8, (mx, my, b + 0.4), P.metal, seg=8, name="mast")
    beacon = info.animate(SPIN, 240, (mx, my, b + 0.82))
    box((0.12, 0.03, 0.04), (mx, my, b + 0.82), mat_glow(P, (1.0, 0.9, 0.4)), part=beacon, bevel=0.004, name="beacon")
    if adv:
        dish(P, -W * 0.05, H * 0.35, b + 0.5, 0.18)
        cyl(0.03, 0.4, (-W * 0.05, H * 0.35, b + 0.2), P.metal_dk, seg=8, name="dish_post")
    info.height = 1.0


@model("Shipyard", footprint=(3, 3))
def shipyard(P, info):
    naval_yard(P, info, 3.0, 3.0, False)


@model("AdvancedShipyard", footprint=(4, 3))
def adv_shipyard(P, info):
    naval_yard(P, info, 4.0, 3.0, True)


def naval_yard(P, info, W, H, adv):
    b = 0.06
    # piers around an open berth (shipyards sit on water)
    box((W * 0.97, 0.4, 0.12), (0, H * 0.3, b), P.concrete, bevel=0.012, name="pier_back")
    for s in (-1, 1):
        box((0.3, H * 0.8, 0.12), (s * (W * 0.485 - 0.15), -H * 0.05, b), P.concrete, bevel=0.012, name="pier_side")
        for k in range(4):
            cyl(0.03, 0.2, (s * (W * 0.485 - 0.15), -H * 0.4 + k * 0.25, b), P.metal_dk, seg=8, name="bollard")
    t = block(P, 0, H * 0.38, b + 0.06, W * 0.6, 0.3, 0.35, m=P.hull if P.ca else P.concrete_dk)
    team_stripe(P, 0, H * 0.38 - 0.16, b + 0.3, W * 0.4, 0.02)
    # dock crane that rocks while working
    cx = -W * 0.32
    for y in (-0.1, 0.2):
        box((0.06, 0.06, 0.9), (cx, y, b + 0.5), P.yellow, bevel=0.006, name="crane_leg")
    box((0.08, 0.5, 0.08), (cx, 0.05, b + 0.96), P.yellow, bevel=0.008, name="crane_top")
    jib = info.animate(ROCK, 12, (cx, 0.05, b + 0.96))
    box((0.06, 0.06, 0.9), (cx + 0.45, 0.05, b + 0.98), P.yellow, part=jib, bevel=0.006, rot=(0, 90, 0), name="jib")
    cyl(0.004, 0.45, (cx + 0.75, 0.05, b + 0.74), P.metal_dk, part=jib, seg=4, bevel=0, name="cable")
    # floating buoys bob on the water
    for k, (x, y) in enumerate(((-0.4, -H * 0.45), (0.4, -H * 0.45))):
        buoy = info.animate(BOB, 0.03, (x, y, 0.02), phase=k * 0.5)
        cyl(0.05, 0.08, (x, y, 0.02), mat_red(P), part=buoy, seg=10, name="buoy")
        lamp(P, info, x, y, 0.08, rate=0.6, phase=k * 0.5)
    if adv:
        dish(P, W * 0.35, H * 0.35, b + 0.6, 0.15)
        cyl(0.03, 0.45, (W * 0.35, H * 0.35, b + 0.3), P.metal_dk, seg=8, name="dish_post")
    flag(P, info, W * 0.42, H * 0.1, b + 0.06, 0.7)
    info.height = 1.2


# ============================================================================================ tech

@model("TechCenter", footprint=(2, 2))
def tech_center(P, info):
    W = H = 2.0
    foundation(P, W, H)
    b = 0.08
    if P.ca:
        hull(octo(1.4, 1.4, 0.35), octo(1.1, 1.1, 0.28), b, b + 0.45, material=P.hull, name="hall")
        sphere(0.45, (0, 0, b + 0.45), P.glass, seg=24, rings=10, half=True, scale=(1, 1, 0.55), name="glass_dome")
    else:
        block(P, 0, 0, b, 1.4, 1.4, 0.5, m=P.concrete_dk)
        box((0.8, 0.8, 0.2), (0, 0.1, b + 0.63), P.hull, bevel=0.012, name="upper")
        rivet_row(P, (-0.6, -0.71, b + 0.4), (0.6, -0.71, b + 0.4), 10, axis_normal="Y")
    team_stripe(P, 0, -0.71, b + 0.25, 0.8, 0.02)
    door(P, 0, -0.72, b, 0.2, 0.22)
    sat = info.animate(SPIN, 30, (0.55, 0.55, b + 0.55))
    cyl(0.03, 0.4, (0.55, 0.55, b + 0.3), P.metal_dk, seg=8, name="sat_post")
    dish(P, 0.55, 0.55, b + 0.6, 0.18, part=sat)
    lamp(P, info, -0.6, 0.6, b + 0.6, rate=0.5, colour=(0.3, 0.8, 1.0))
    info.height = 1.2


@model("ResearchLab", footprint=(3, 3))
def research_lab(P, info):
    W = H = 3.0
    foundation(P, W, H)
    b = 0.08
    m = P.hull if P.ca else P.concrete_dk
    t = block(P, -0.3, 0.2, b, 1.6, 1.6, 0.45, m=m)
    # glowing reactor column with a floating ring
    cx, cy = 0.75, -0.6
    cyl(0.25, 0.08, (cx, cy, b + 0.04), P.metal_dk, seg=20, name="column_base")
    cyl(0.12, 0.9, (cx, cy, b + 0.5), P.glass, seg=16, name="column_glass")
    cyl(0.06, 0.88, (cx, cy, b + 0.5), mat_glow(P, (0.3, 1.0, 0.6) if P.ca else (1.0, 0.4, 0.2)), seg=10, bevel=0, name="column_core")
    ring = info.animate(BOB, 0.25, (cx, cy, b + 0.45))
    cyl(0.22, 0.04, (cx, cy, b + 0.45), P.metal, part=ring, seg=20, name="float_ring")
    spin = info.animate(SPIN, 60, (cx, cy, b + 0.97))
    for k in range(3):
        a = k * math.tau / 3
        box((0.04, 0.2, 0.03), (cx + math.cos(a) * 0.15, cy + math.sin(a) * 0.15, b + 0.97), P.hull_dk, part=spin, bevel=0.004,
            rot=(0, 0, math.degrees(a) + 90), name="emitter")
    for s in (-1, 1):
        pipe(P, (cx + s * 0.2, cy, b + 0.1), (-0.3 + s * 0.4, 0.2 - 0.8, b + 0.3))
    window_row(P, -1.0, 0.4, 0.2 - 0.81, b + 0.3, 4)
    team_stripe(P, -0.3, 0.2, t, 1.0, 0.12)
    vent_fan(P, info, -0.7, 0.6, t)
    vent_fan(P, info, 0.1, 0.6, t, speed=-350)
    info.height = 1.3


@model("Radar", footprint=(2, 2))
def radar(P, info):
    W = H = 2.0
    foundation(P, W, H)
    b = 0.08
    t = block(P, -0.25, -0.2, b, 1.1, 1.0, 0.4, m=P.hull if P.ca else P.concrete_dk)
    window_row(P, -0.7, 0.2, -0.71, b + 0.25, 3)
    team_stripe(P, -0.25, -0.2, t, 0.6, 0.1)
    # lattice tower carrying the dish (Extra, spun by the game)
    tx, ty = 0.45, 0.45
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(0.02, 1.0, (tx + sx * 0.12, ty + sy * 0.12, b + 0.5), P.metal, seg=6, name="tower_leg")
    for k in range(4):
        box((0.28, 0.28, 0.015), (tx, ty, b + 0.2 + k * 0.25), P.metal_dk, bevel=0, name="tower_brace")
    info.pivots["Extra"] = (tx, ty, 1.1)
    info.extra_spins = True
    d = cyl(0.42, 0.06, (tx, ty + 0.05, 1.3), P.white, part="Extra", seg=24, r2=0.3, name="dish")
    d.rotation_euler = (math.radians(-70), 0, 0)
    box((0.08, 0.2, 0.08), (tx, ty - 0.02, 1.15), P.dark, part="Extra", bevel=0.01, name="dish_mount")
    cyl(0.012, 0.3, (tx, ty + 0.25, 1.3), P.metal_dk, axis="Y", part="Extra", seg=6, name="feed")
    lamp(P, info, tx, ty, b + 1.0, rate=0.5)
    info.height = 1.6


@model("CommCenter", footprint=(2, 2))
def comm_center(P, info):
    W = H = 2.0
    foundation(P, W, H)
    b = 0.08
    t = block(P, 0, -0.25, b, 1.3, 0.9, 0.45, m=P.hull if P.ca else P.concrete_dk)
    window_row(P, -0.5, 0.5, -0.71, b + 0.3, 4)
    team_stripe(P, 0, -0.25, t, 0.9, 0.1)
    # tall antenna mast with blinking aircraft warning lights
    mx, my = 0.0, 0.45
    for k in range(5):
        h = 2.2 - k * 0.0
        cyl(0.07 - k * 0.012, 0.44, (mx, my, b + 0.22 + k * 0.44), P.metal if k % 2 == 0 else mat_red(P), seg=8, r2=0.06 - k * 0.012,
            name="mast_section")
    for k in range(3):
        lamp(P, info, mx, my, b + 0.75 + k * 0.7, rate=0.7, phase=k * 0.15)
    dishes = info.animate(SPIN, 20, (mx, my, b + 1.2))
    for s in (-1, 1):
        dish(P, mx + s * 0.15, my, b + 1.2, 0.12, part=dishes, tilt=-90 + s * 10)
    for s in (-1, 1):
        cyl(0.004, 1.6, (mx + s * 0.6, my, b + 0.9), P.metal_dk, seg=4, bevel=0, name="guy_wire").rotation_euler = (0, math.radians(s * 20), 0)
    info.height = 2.4


@model("Superweapon", footprint=(3, 3))
def superweapon(P, info):
    W = H = 3.0
    foundation(P, W, H)
    b = 0.08
    if P.ca:
        # Helios uplink: pylons around an emitter, with a ring on Extra that the game spins
        block(P, 0, 0, b, 2.2, 2.2, 0.3, m=P.hull)
        for k in range(4):
            a = k * math.tau / 4 + math.pi / 4
            x, y = math.cos(a) * 0.85, math.sin(a) * 0.85
            box((0.18, 0.18, 1.2), (x, y, b + 0.3 + 0.6), P.hull_lt, bevel=0.02, rot=(0, 0, math.degrees(a)), name="pylon")
            lamp(P, info, x, y, b + 1.52, rate=0.4, phase=k * 0.25, colour=(0.3, 0.8, 1.0))
        cyl(0.25, 0.6, (0, 0, b + 0.6), P.metal_dk, seg=20, name="emitter_base")
        cyl(0.12, 0.8, (0, 0, b + 1.2), mat_glow(P, (0.4, 0.9, 1.0)), seg=16, r2=0.04, bevel=0, name="emitter")
        info.pivots["Extra"] = (0, 0, 1.4)
        info.extra_spins = True
        cyl(0.7, 0.06, (0, 0, 1.4), P.hull_dk, part="Extra", seg=32, name="ring")
        for k in range(8):
            a = k * math.tau / 8
            box((0.08, 0.08, 0.1), (math.cos(a) * 0.7, math.sin(a) * 0.7, 1.4), mat_glow(P, (0.4, 0.9, 1.0)), part="Extra", bevel=0.01,
                name="ring_node")
        team_stripe(P, 0, -1.1, b + 0.2, 1.0, 0.02)
    else:
        # Hammerfall silo: armoured doors around a missile that rises as it charges (Extra)
        block(P, 0, 0, b, 2.3, 2.3, 0.35, m=P.concrete_dk)
        for s in (-1, 1):
            box((0.45, 0.9, 0.06), (s * 0.48, 0, b + 0.38), P.hull, bevel=0.01, rot=(0, s * 65, 0), name="silo_door")
            rivet_row(P, (s * 0.3, -0.4, b + 0.5), (s * 0.3, 0.4, b + 0.5), 6, axis_normal="X")
        box((0.8, 0.8, 0.02), (0, 0, b + 0.36), P.dark, bevel=0, name="silo_mouth")
        for k in range(4):
            a = k * math.tau / 4
            lamp(P, info, math.cos(a) * 0.95, math.sin(a) * 0.95, b + 0.4, rate=1.2, phase=k * 0.25)
        box((2.32, 0.12, 0.1), (0, -1.0, b + 0.25), P.team, bevel=0.006, name="team_band")
        info.pivots["Extra"] = (0, 0, -0.6)
        cyl(0.12, 1.3, (0, 0, -0.6 + 0.65), P.white, part="Extra", seg=16, name="missile")
        cyl(0.12, 0.35, (0, 0, -0.6 + 1.47), mat_red(P), part="Extra", seg=16, r2=0.0, name="warhead")
        for k in range(4):
            box((0.02, 0.18, 0.22), (0, 0, -0.6 + 0.12), P.dark, part="Extra", bevel=0.004, rot=(0, 0, k * 45), name="fin")
        box((0.2, 0.2, 0.9), (1.05, 0.8, b + 0.45), P.hull_dk, bevel=0.012, name="gantry")
    info.height = 2.2


@model("SpecialPower", footprint=(2, 2))
def special_power(P, info):
    W = H = 2.0
    foundation(P, W, H)
    b = 0.08
    t = block(P, 0, 0.2, b, 1.3, 1.1, 0.4, m=P.hull if P.ca else P.concrete_dk)
    team_stripe(P, 0, 0.2, t, 0.8, 0.12)
    garage_door(P, 0, 0.2 - 0.57, b, 0.4, 0.26)
    # tracking dish that rocks to scan the sky
    cyl(0.05, 0.5, (0.4, 0.4, t + 0.25), P.metal_dk, seg=10, name="dish_post")
    scan = info.animate(ROCK, 20, (0.4, 0.4, t + 0.55))
    dish(P, 0.4, 0.4, t + 0.6, 0.28, part=scan, tilt=-55)
    lamp(P, info, -0.5, 0.6, t + 0.05, rate=0.8, colour=(0.3, 1.0, 0.4))
    info.height = 1.5


@model("Countermeasure", footprint=(2, 2))
def countermeasure(P, info):
    W = H = 2.0
    foundation(P, W, H)
    b = 0.08
    t = block(P, 0, 0, b, 1.2, 1.2, 0.35, m=P.hull if P.ca else P.concrete_dk)
    team_stripe(P, 0, -0.61, b + 0.25, 0.8, 0.02)
    # phased-array radar that turns and interceptor launch tubes
    radar_ = info.animate(SPIN, 45, (0, 0.2, t))
    cyl(0.12, 0.1, (0, 0.2, t + 0.05), P.metal_dk, part=radar_, seg=12, name="radar_base")
    box((0.5, 0.06, 0.4), (0, 0.2, t + 0.32), P.hull_lt, part=radar_, bevel=0.012, rot=(-15, 0, 0), name="array")
    box((0.42, 0.065, 0.32), (0, 0.17, t + 0.32), P.dark, part=radar_, bevel=0.004, rot=(-15, 0, 0), name="array_face")
    for i in range(3):
        for j in range(2):
            cyl(0.06, 0.3, (-0.3 + i * 0.3, -0.35 + j * 0.15, t + 0.12), P.metal, seg=10, name="launch_tube").rotation_euler = (math.radians(-20), 0, 0)
    lamp(P, info, 0.55, -0.55, t + 0.04, rate=0.5, colour=(0.3, 0.8, 1.0))
    info.height = 1.3


# ============================================================================================ defences

@model("mgbunker", footprint=(1, 1))
def mg_bunker(P, info):
    b = 0.0
    if P.ca:
        hull(octo(0.85, 0.85, 0.25), octo(0.6, 0.6, 0.18), b, b + 0.32, material=P.concrete, bevel=0.02, name="pillbox")
    else:
        sandbags(P, -0.42, -0.42, 0.42, -0.42, rows=3)
        sandbags(P, -0.42, 0.42, 0.42, 0.42, rows=3)
        sandbags(P, -0.42, -0.42, -0.42, 0.42, rows=3)
        sandbags(P, 0.42, -0.42, 0.42, 0.42, rows=3)
        box((0.7, 0.7, 0.24), (0, 0, b + 0.12), P.concrete_dk, bevel=0.02, name="pit")
        box((0.3, 0.012, 0.08), (0, -0.452, b + 0.2), P.team, bevel=0.002, name="team_banner")
    box((0.36, 0.03, 0.05), (0, -0.39, b + 0.18), P.team, bevel=0.004, name="team_band") if P.ca else None
    piv = (0, 0, 0.33)
    info.pivots["Turret"] = piv
    cyl(0.16, 0.12, (0, 0, piv[2] + 0.06), P.hull_dk, part="Turret", seg=14, name="cupola")
    box((0.18, 0.04, 0.04), (0, 0.14, piv[2] + 0.07), P.dark, part="Turret", bevel=0.008, name="slit")
    barrel(P, 0, 0.16, 0.2, 0.012, piv[2] + 0.07, brake=False, sleeve=False, info=info, recoil=0.03)
    info.height = 0.7
    info.muzzle_forward = 0.36
    info.muzzle_height = piv[2] + 0.07


def turret_base(P, r=0.38):
    if P.ca:
        hull(octo(0.85, 0.85, 0.22), octo(0.7, 0.7, 0.2), 0.0, 0.3, material=P.concrete, bevel=0.015, name="turret_base")
    else:
        box((0.85, 0.85, 0.3), (0, 0, 0.15), P.concrete_dk, bevel=0.02, name="turret_base")
        rivet_row(P, (-0.38, -0.43, 0.22), (0.38, -0.43, 0.22), 6, axis_normal="Y")
    cyl(r * 0.7, 0.1, (0, 0, 0.35), P.metal_dk, seg=18, name="turret_ring")
    box((0.4, 0.03, 0.05), (0, -0.375 if P.ca else -0.43, 0.2), P.team, bevel=0.004, name="team_band")


@model("aiturret", footprint=(1, 1))
def ai_turret(P, info):
    turret_base(P)
    piv = (0, 0, 0.4)
    info.pivots["Turret"] = piv
    if P.ca:
        # sentry gun: rotary barrels that spin when firing
        sphere(0.17, (0, 0, piv[2] + 0.1), P.hull, part="Turret", seg=16, rings=8, scale=(1, 1.1, 0.8), name="sentry_head")
        box((0.08, 0.05, 0.04), (0, 0.16, piv[2] + 0.16), P.glass, part="Turret", bevel=0.008, name="sensor")
        spin = info.animate(RECOIL, 0.02, (0, 0.16, piv[2] + 0.08), parent="Turret")
        for k in range(4):
            a = k * math.tau / 4
            cyl(0.01, 0.28, (math.cos(a) * 0.025, 0.3, piv[2] + 0.08 + math.sin(a) * 0.025), P.metal_dk, axis="Y", part=spin, seg=6, name="rotary")
        info.muzzle_forward = 0.45
    else:
        # incinerator: fuel drum on a mount with a glowing nozzle
        cyl(0.13, 0.28, (0, -0.05, piv[2] + 0.12), P.accent, axis="Y", part="Turret", seg=14, name="fuel_drum")
        cyl(0.04, 0.25, (0, 0.2, piv[2] + 0.12), P.metal_dk, axis="Y", part="Turret", seg=10, name="nozzle")
        flame = info.animate(BOB, 0.01, (0, 0.33, piv[2] + 0.12), parent="Turret")
        sphere(0.025, (0, 0.33, piv[2] + 0.12), mat_glow(P, (1.0, 0.55, 0.15)), part=flame, seg=8, rings=6, name="pilot")
        info.muzzle_forward = 0.4
    info.muzzle_height = 0.55
    info.height = 0.9


@model("avturret", footprint=(1, 1))
def av_turret(P, info):
    turret_base(P)
    piv = (0, 0, 0.4)
    info.pivots["Turret"] = piv
    if P.ca:
        hull(octo(0.36, 0.42, 0.1), octo(0.28, 0.3, 0.08), 0.0, 0.16, loc=(0, 0, piv[2]), material=P.hull, part="Turret", bevel=0.01, name="rail_house")
        for s in (-1, 1):
            box((0.03, 0.5, 0.05), (s * 0.04, 0.38, piv[2] + 0.08), P.metal_dk, part="Turret", bevel=0.006, name="rail")
        for k in range(3):
            cyl(0.06, 0.02, (0, 0.25 + k * 0.12, piv[2] + 0.08), mat_glow(P), axis="Y", part="Turret", seg=12, bevel=0, name="coil")
        info.muzzle_forward = 0.8
    else:
        box((0.36, 0.4, 0.18), (0, 0, piv[2] + 0.09), P.hull, part="Turret", bevel=0.012, name="gun_house")
        barrel(P, 0, 0.2, 0.55, 0.03, piv[2] + 0.1, info=info, recoil=0.08)
        info.muzzle_forward = 0.8
    info.muzzle_height = 0.55
    info.height = 0.9


@model("aadefense", footprint=(1, 1))
def aa_defense(P, info):
    turret_base(P)
    piv = (0, 0, 0.4)
    info.pivots["Turret"] = piv
    cyl(0.1, 0.1, (0, 0, piv[2] + 0.05), P.hull_dk, part="Turret", seg=12, name="mount")
    if P.ca:
        for s in (-1, 1):
            pod = box((0.12, 0.3, 0.14), (s * 0.13, 0.05, piv[2] + 0.2), P.hull, part="Turret", bevel=0.012, rot=(35, 0, 0), name="missile_pod")
            for k in (-0.03, 0.03):
                c = cyl(0.022, 0.03, (s * 0.13 + k, 0.2, piv[2] + 0.31), P.white, axis="Y", part="Turret", seg=8, r2=0.004, bevel=0, name="missile_tip")
                c.rotation_euler = (math.radians(-90 + 35), 0, 0)
    else:
        for s in (-1, 1):
            for k in (-0.025, 0.025):
                b_ = cyl(0.012, 0.42, (s * 0.1 + k, 0.15, piv[2] + 0.22), P.metal_dk, axis="Y", part="Turret", seg=6, name="flak_barrel")
                b_.rotation_euler = (math.radians(-90 + 40), 0, 0)
        box((0.3, 0.12, 0.12), (0, -0.05, piv[2] + 0.14), P.hull, part="Turret", bevel=0.01, name="breech")
    radar_ = info.animate(SPIN, 180, (0, -0.15, piv[2] + 0.25), parent="Turret")
    box((0.14, 0.02, 0.06), (0, -0.15, piv[2] + 0.28), P.metal, part=radar_, bevel=0.004, name="radar")
    info.muzzle_forward = 0.35
    info.muzzle_height = 0.6
    info.height = 0.9


@model("artillery", footprint=(2, 2))
def artillery(P, info):
    b = 0.0
    if P.ca:
        hull(octo(1.8, 1.8, 0.5), octo(1.4, 1.4, 0.4), b, b + 0.35, material=P.concrete, bevel=0.02, name="citadel")
    else:
        cyl(0.9, 0.25, (0, 0, b + 0.125), P.concrete_dk, seg=24, name="mortar_pit")
        sandbags(P, -0.8, -0.8, 0.8, -0.8, rows=2)
        for k in range(4):
            box((0.16, 0.16, 0.12), (-0.6 + k * 0.4, 0.75, b + 0.31), P.leather, bevel=0.012, name="shell_crate")
    box((1.0, 0.1, 0.05), (0, -0.85, b + 0.3), P.team, bevel=0.006, name="team_band")
    piv = (0, 0, 0.4)
    info.pivots["Turret"] = piv
    cyl(0.35, 0.1, (0, 0, piv[2] + 0.05), P.metal_dk, part="Turret", seg=20, name="traverse_ring")
    box((0.5, 0.55, 0.25), (0, -0.05, piv[2] + 0.22), P.hull, part="Turret", bevel=0.015, name="gun_house")
    gun = info.animate(RECOIL, 0.1, (0, 0.25, piv[2] + 0.25), parent="Turret")
    b_ = cyl(0.045, 0.75, (0, 0.25 + 0.34, piv[2] + 0.25 + 0.16), P.metal, axis="Y", part=gun, seg=14, name="barrel")
    b_.rotation_euler = (math.radians(-90 + 25), 0, 0)
    br = cyl(0.065, 0.08, (0, 0.25 + 0.68, piv[2] + 0.25 + 0.32), P.metal_dk, axis="Y", part=gun, seg=14, name="brake")
    br.rotation_euler = (math.radians(-90 + 25), 0, 0)
    info.muzzle_forward = 0.95
    info.muzzle_height = 0.75
    info.height = 0.9


# ============================================================================================ civilian and tech (neutral)

@model("civ_house", footprint=(2, 2))
def civ_house(P, info):
    house(P, info, 2.0, 2.0, (0.85, 0.8, 0.68), (0.55, 0.25, 0.2))


@model("civ_barn", footprint=(2, 3))
def civ_barn(P, info):
    house(P, info, 2.0, 3.0, (0.6, 0.22, 0.18), (0.35, 0.3, 0.28), barn=True)


def house(P, info, W, H, wall, roof, barn=False):
    wm, rm = mat("wall", wall, 0.9), mat("roof", roof, 0.8)
    box((W * 0.95, H * 0.95, 0.02), (0, 0, 0.01), mat("yard", (0.35, 0.42, 0.22), 1.0), bevel=0, name="yard")
    w, d = W * 0.6, H * 0.55
    box((w, d, 0.38), (0, 0, 0.19), wm, bevel=0.01, name="walls")
    poly_prism([(-w / 2 - 0.06, 0), (w / 2 + 0.06, 0), (0, 0.32 if not barn else 0.4)], d + 0.1, loc=(0, 0, 0.38), material=rm, axis="Y",
               bevel=0.008, name="roof")
    if barn:
        box((0.36, 0.03, 0.32), (0, -d / 2 - 0.01, 0.16), mat("barn_door", (0.45, 0.16, 0.12), 0.9), bevel=0.006, name="barn_door")
        for s in (-1, 1):
            box((0.03, 0.035, 0.42), (s * 0.12, -d / 2 - 0.02, 0.16), P.white, bevel=0, rot=(0, s * 37, 0), name="door_brace")
        cyl(0.16, 0.7, (w / 2 + 0.25, 0.2, 0.35), mat("silo_grain", (0.7, 0.72, 0.7), 0.5, 0.4), seg=16, name="grain_silo")
        sphere(0.16, (w / 2 + 0.25, 0.2, 0.7), mat("silo_grain", (0.7, 0.72, 0.7), 0.5, 0.4), seg=16, rings=8, half=True, name="silo_cap")
        vane = info.animate(SPIN, 15, (0, 0, 0.82))
        box((0.012, 0.16, 0.04), (0, 0.02, 0.82), P.metal_dk, part=vane, bevel=0, name="weather_vane")
    else:
        door(P, -w * 0.2, -d / 2 - 0.005, 0.0, 0.1, 0.18)
        window_row(P, 0.0, w * 0.4, -d / 2 - 0.005, 0.2, 2, w=0.08, h=0.08)
        box((0.1, 0.1, 0.25), (w * 0.3, d * 0.2, 0.62), mat("brick", (0.5, 0.28, 0.2), 0.9), bevel=0.006, name="chimney")
        smoke = info.animate(BOB, 0.06, (w * 0.3, d * 0.2, 0.8))
        sphere(0.06, (w * 0.3, d * 0.2, 0.8), mat("smoke", (0.6, 0.6, 0.6), 1.0), part=smoke, seg=8, rings=5, name="smoke")
        for k in range(6):
            cyl(0.012, 0.12, (-W * 0.45 + k * 0.17, -H * 0.45, 0.06), mat("fence", (0.55, 0.42, 0.3), 0.9), seg=5, name="fence_post")
        box((1.0, 0.012, 0.02), (-W * 0.45 + 0.43, -H * 0.45, 0.09), mat("fence", (0.55, 0.42, 0.3), 0.9), bevel=0, name="fence_rail")
    info.height = 0.9


@model("civ_chapel", footprint=(2, 2))
def civ_chapel(P, info):
    stone, roof = mat("stone", (0.72, 0.7, 0.64), 0.9), mat("slate", (0.3, 0.32, 0.36), 0.8)
    box((0.7, 1.2, 0.45), (0, 0.1, 0.225), stone, bevel=0.01, name="nave")
    poly_prism([(-0.4, 0), (0.4, 0), (0, 0.35)], 1.25, loc=(0, 0.1, 0.45), material=roof, axis="Y", bevel=0.008, name="roof")
    box((0.32, 0.32, 0.9), (0, -0.62, 0.45), stone, bevel=0.01, name="tower")
    cyl(0.23, 0.4, (0, -0.62, 1.1), roof, seg=4, r2=0.0, bevel=0, name="spire").rotation_euler = (0, 0, math.radians(45))
    bell = info.animate(ROCK, 20, (0, -0.62, 0.8))
    sphere(0.06, (0, -0.62, 0.74), mat("bronze", (0.7, 0.5, 0.2), 0.4, 0.8), part=bell, seg=10, rings=6, half=True, scale=(1, 1, -1.2), name="bell")
    box((0.12, 0.02, 0.2), (0, -0.79, 0.12), mat("chapel_door", (0.4, 0.25, 0.15), 0.9), bevel=0.004, name="door")
    for s in (-1, 1):
        for k in range(3):
            box((0.02, 0.08, 0.16), (s * 0.355, -0.2 + k * 0.3, 0.27), mat("stained", (0.5, 0.3, 0.8), 0.2, 0.0, emit=0.5), bevel=0.004, name="window")
    info.height = 1.4


@model("civ_watertower", footprint=(1, 1))
def civ_watertower(P, info):
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(0.025, 0.75, (sx * 0.2, sy * 0.2, 0.375), P.metal_dk, seg=6, name="leg")
    for k in range(2):
        box((0.44, 0.44, 0.012), (0, 0, 0.25 + k * 0.3), P.metal_dk, bevel=0, name="brace")
    cyl(0.3, 0.32, (0, 0, 0.91), mat("tank_paint", (0.7, 0.72, 0.68), 0.6, 0.3), seg=20, name="water_tank")
    cyl(0.32, 0.14, (0, 0, 1.14), P.metal_dk, seg=20, r2=0.03, bevel=0, name="tank_roof")
    lamp(P, info, 0, 0, 1.22, rate=0.4)
    info.height = 1.3


@model("tech_derrick", footprint=(2, 2))
def tech_derrick(P, info):
    foundation(P, 2.0, 2.0)
    b = 0.08
    # pump jack: walking beam rocks on its frame
    for s in (-1, 1):
        box((0.04, 0.3, 0.5), (s * 0.08, 0.0, b + 0.25), P.metal_dk, bevel=0.004, rot=(0, 0, 0), name="samson_post")
    beam = info.animate(ROCK, 14, (0, 0.0, b + 0.5))
    box((0.08, 1.0, 0.06), (0, 0.0, b + 0.52), mat("pump", (0.85, 0.6, 0.15), 0.5), part=beam, bevel=0.008, name="walking_beam")
    hull(octo(0.12, 0.2, 0.0), octo(0.12, 0.2, 0.0), b + 0.4, b + 0.6, loc=(0, 0.52, 0), material=mat("pump", (0.85, 0.6, 0.15), 0.5), part=beam,
         bevel=0.01, name="horse_head")
    box((0.2, 0.2, 0.2), (0, -0.52, b + 0.45), P.metal_dk, part=beam, bevel=0.01, name="counterweight")
    cyl(0.04, 0.3, (0, 0.55, b + 0.15), P.metal, seg=8, name="wellhead")
    tank(P, -0.6, 0.5, b, 0.22, 0.4)
    tank(P, -0.6, -0.1, b, 0.18, 0.35)
    pipe(P, (0, 0.55, b + 0.05), (-0.45, 0.5, b + 0.05))
    flag(P, info, 0.7, -0.7, b, 0.6)
    info.height = 1.7


@model("tech_hospital", footprint=(2, 2))
def tech_hospital(P, info):
    foundation(P, 2.0, 2.0)
    b = 0.08
    t = block(P, 0, 0.1, b, 1.4, 1.2, 0.45, m=P.white, roof=P.concrete_dk)
    for s in (-1, 1):
        box((0.3, 0.02, 0.08), (0, 0.1 - 0.61, b + 0.33), mat_red(P), bevel=0.002, name="cross_h")
    box((0.08, 0.02, 0.3), (0, 0.1 - 0.61, b + 0.33), mat_red(P), bevel=0.002, name="cross_v")
    window_row(P, -0.6, -0.2, 0.1 - 0.61, b + 0.3, 2)
    window_row(P, 0.2, 0.6, 0.1 - 0.61, b + 0.3, 2)
    door(P, 0, 0.1 - 0.62, b, 0.2, 0.18)
    helipad(P, 0.0, 0.2, t, 0.35)
    lamp(P, info, 0.6, 0.65, t + 0.03, rate=0.8)
    lamp(P, info, -0.6, 0.65, t + 0.03, rate=0.8, phase=0.5)
    info.height = 1.0


@model("tech_relay", footprint=(2, 2))
def tech_relay(P, info):
    foundation(P, 2.0, 2.0)
    b = 0.08
    block(P, -0.45, -0.45, b, 0.6, 0.5, 0.3, m=P.concrete)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(0.02, 1.6, (0.3 + sx * 0.15, 0.3 + sy * 0.15, b + 0.8), P.metal, seg=6, name="mast_leg")
    for k in range(5):
        box((0.34, 0.34, 0.012), (0.3, 0.3, b + 0.2 + k * 0.33), P.metal_dk, bevel=0, name="mast_brace")
    turn = info.animate(SPIN, 35, (0.3, 0.3, b + 1.62))
    dish(P, 0.3, 0.3, b + 1.75, 0.25, part=turn, tilt=-60)
    lamp(P, info, 0.3, 0.3, b + 1.62, rate=0.6)
    info.height = 2.0


@model("tech_depot", footprint=(3, 2))
def tech_depot(P, info):
    foundation(P, 3.0, 2.0)
    b = 0.08
    t = block(P, -0.4, 0.2, b, 1.6, 1.1, 0.5, m=P.concrete_dk)
    garage_door(P, -0.4, 0.2 - 0.57, b, 0.6, 0.35)
    for i in range(3):
        for j in range(2):
            box((0.2, 0.2, 0.18), (0.85 + j * 0.25, -0.5 + i * 0.3, b + 0.09), mat("crate", (0.45, 0.36, 0.2), 0.8), bevel=0.01, name="crate")
    box((0.2, 0.2, 0.18), (0.97, -0.2, b + 0.27), mat("crate", (0.45, 0.36, 0.2), 0.8), bevel=0.01, name="crate")
    lamp(P, info, -0.4, 0.2 - 0.58, b + 0.42, rate=0.5, colour=(1.0, 0.8, 0.2))
    info.height = 1.0
