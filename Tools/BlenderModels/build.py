"""Builds AURITE FRONT models with Blender's Python module and exports them for the game.

Usage (from this folder, with `pip install bpy` done):
    python3 build.py [keys...] [--faction ca|id] [--preview DIR] [--out DIR]

Without keys every registered model is built. Output: <out>/<key>_<faction>.bytes (default
../../Assets/_Project/Resources/Models). --preview also renders a PNG per model.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import kit  # noqa: E402
import models  # noqa: E402

TEAMS = {"ca": (0.20, 0.45, 0.85), "id": (0.80, 0.15, 0.12), "ne": (0.75, 0.7, 0.6)}


def factions_for(key, wanted):
    """Civilian and tech structures belong to the neutral side and get one '_ne' model."""
    if key.startswith(("civ_", "tech_")):
        return ["ne"]
    return [f for f in wanted if f != "ne"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*")
    ap.add_argument("--faction", choices=["ca", "id", "ne"], action="append")
    ap.add_argument("--preview")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project", "Resources", "Models"))
    ap.add_argument("--samples", type=int, default=24)
    a = ap.parse_args()
    keys = a.keys or sorted(models.MODELS)
    factions = a.faction or ["ca", "id"]
    os.makedirs(a.out, exist_ok=True)
    if a.preview:
        os.makedirs(a.preview, exist_ok=True)
    for key in keys:
        fn, fp = models.MODELS[key]
        for fac in factions_for(key, factions):
            kit.reset_scene()
            P = kit.Palette(fac)
            info = kit.ModelInfo(key, fac)
            if fp:
                info.footprint = fp
            fn(P, info)
            parts = kit.gather(info)
            path = os.path.join(a.out, f"{key}_{fac}.bytes")
            tris = kit.write_rtsm(path, info, parts)
            print(f"{key}_{fac}: {tris} triangles, parts {sorted(parts)} -> {os.path.getsize(path) // 1024} KB")
            if a.preview:
                kit.render_preview(os.path.join(a.preview, f"{key}_{fac}.png"), TEAMS[fac], samples=a.samples, from_front=not fp)


if __name__ == "__main__":
    main()
