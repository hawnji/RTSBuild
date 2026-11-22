# Blender models

Unit and structure models built with Blender from Python. They replace the procedural models from
`ModelLibrary.cs` one key at a time; any key without a file still uses the code-built model.

## Build

```
pip install bpy          # Blender as a Python module (needs the Python version bpy supports, 3.13 for bpy 5.3)
python3 build.py                          # every model, both factions
python3 build.py tank_mbt --faction id    # one model
python3 build.py --preview ../../Temp/model-previews   # also render a PNG per model
```

Output goes to `Assets/_Project/Resources/Models/<key>_<ca|id>.bytes`. Then check the files with the game's own
reader:

```
cd ../ModelCheck && dotnet run
```

## Map scenery

```
python3 build_scenery.py                  # every scenery piece into Resources/Models/Scenery
python3 build_scenery.py tree_pine --preview ../../Temp/scenery-previews
```

Scenery pieces (trees, bushes, rocks, mountains, cliffs, rubble, ore, bridges) live in `scenery.py`, with variant counts in
`VARIANTS`. Pure green (0, g, 0) becomes the map theme's foliage colour and pure blue (0, 0, b) the theme's rock colour, with
the channel kept as shading. Vertex alpha from 0.5 to 1 is how much a vertex sways in the wind; below 0.5 it glows.

## Writing models

* `kit.py` holds the primitives (bevelled boxes, lofted hulls, prisms, cylinders, capsules), the exporter and the
  preview renderer. `models.py` holds one function per model key, registered with `@model("key")`.
* 1 Blender unit = 1 map cell. Z is up, +Y is the front of a unit. Structures are centred on their footprint with
  the entrance facing -Y; pass `footprint=(w, h)` so the game only uses the model on that footprint.
* Use the palette materials (`P.hull`, `P.dark`, `P.metal`, ...). `P.team` becomes the owner's colour in game, and
  `P.light` glows.
* Put turrets, rotors and other moving pieces in their own part (`part="Turret"`) and set `info.pivots[...]`.

## Your own models

You can also drop a prefab or FBX into `Assets/_Project/Resources/Models/` named after the key
(`tank_mbt_ca` or `tank_mbt`). Child objects named `Body`, `Turret`, `Rotor` and `Extra` become the animated parts.
The game shader uses vertex colours (not textures); colour team areas pure magenta (255, 0, 255) and enable
Read/Write in the import settings so they can be recoloured.
