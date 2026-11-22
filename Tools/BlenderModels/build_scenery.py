"""Builds map scenery pieces into Assets/_Project/Resources/Models/Scenery/<name>_<variant>.bytes.

    python3 build_scenery.py [names...] [--preview DIR]
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import kit  # noqa: E402
import scenery  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project", "Resources", "Models", "Scenery")


def preview_colours():
    """Temperate stand-ins for the marker colours so previews look like the game."""
    for m in kit.bpy.data.materials:
        rgb = m.get("rts_rgb")
        if rgb is None or not m.use_nodes:
            continue
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if m.name.startswith("LEAF"):
            c = [x * rgb[1] / 0.8 for x in (0.26, 0.44, 0.16)]
        elif m.name.startswith("ROCK"):
            c = [x * rgb[2] / 0.8 for x in (0.48, 0.46, 0.44)]
        else:
            continue
        bsdf.inputs["Base Color"].default_value = (*[kit.srgb_to_lin(min(1, x)) for x in c], 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--preview")
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    names = a.names or sorted(scenery.SCENERY)
    for name in names:
        for v in range(scenery.VARIANTS.get(name, 1)):
            kit.reset_scene()
            kit.Palette("ca")
            info = kit.ModelInfo(name, "sc")
            info.floating = name.startswith("bridge")
            scenery.SCENERY[name](1000 + v * 17 + len(name))
            # store marker shades scaled into 0..1
            for m in kit.bpy.data.materials:
                if m.name.startswith(("LEAF", "ROCK")):
                    m["rts_rgb"] = [min(1.0, x * 0.8) for x in m["rts_rgb"]]
            parts = kit.gather(info, scenery=True)
            path = os.path.join(a.out, f"{name}_{v}.bytes")
            tris = kit.write_rtsm(path, info, parts)
            print(f"{name}_{v}: {tris} triangles")
            if a.preview:
                preview_colours()
                kit.render_preview(os.path.join(a.preview, f"{name}_{v}.png"), samples=10, size=256)


if __name__ == "__main__":
    main()
