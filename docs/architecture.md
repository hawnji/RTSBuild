# Architecture

The game has two layers with a hard boundary between them:

* **Simulation** (`Assets/_Project/Scripts/Simulation`, assembly `RTS.Simulation`, namespace `RTS.Sim`). All game
  rules live here. The assembly definition sets `noEngineReferences: true`, so it cannot touch `UnityEngine`. It runs
  inside Unity and also in the plain .NET harness in `Tools/HeadlessSim`.
* **Client** (`Assets/_Project/Scripts/Client`, assembly `RTS.Client`, namespace `RTS.Client`). It renders the
  simulation, turns player input into simulation commands, and holds the UI, audio, settings and save files.

The client reads simulation state every frame, but it only changes that state by calling `World.Commands`. The one
exception is the camera position stored in save files.

## Simulation

### Loop

`World.Step()` advances one fixed tick (15 ticks per second, `World.Dt = 1/15`). The order within a tick is:

1. The spatial grid is rebuilt.
2. `MissionRunner.Update` runs triggers, objectives and countdowns.
3. Each AI controller updates.
4. Systems update in order: Economy, Production, Behaviour, Movement, Combat (buildings, projectiles, fires) and
   Support powers. Fog of war updates every 3 ticks.
5. Dead entities are flushed.
6. The victory check runs.

The simulation is deterministic for a given seed. The headless harness checks this by running the same seed twice.
All randomness goes through `World.Rng` (or per-AI `SimRandom` instances seeded from it), and there is no wall-clock
time.

### Main types

| Area | Files | Notes |
|---|---|---|
| Data | `Data/Definitions.cs`, `Data/Catalog.cs` | Defines units, buildings, weapons, warheads (armour multipliers), technologies, factions and support powers. `Catalog` builds them all in code. |
| World | `Core/World.cs`, `Core/Player.cs` | Holds entity lists, the id map, players, the map, the event stream and the systems. |
| Commands | `Core/Commands.cs` | The only entry point for orders: move (with formation slots), attack-move, attack, force fire, guard, patrol, stop, stances, deploy, enter/unload, capture, harvest, depot repair, follow, sell, repair, rally, production and support powers. |
| Map | `Map/MapData.cs`, `Map/MapGenerator.cs`, `Map/MapLibrary.cs` | Generates maps from a `MapSpec`: noise terrain, then features (rivers with bridges, fords, lakes, ocean, plateaus with ramps, ridges, forests, roads, ore and crystal fields), then symmetry, start-area clearing, cliffs and connectivity repair. |
| Pathfinding | `Pathfinding/Pathfinder.cs`, `MovementSystem.cs` | A* on the cell grid for each movement class (foot, vehicle, naval, air), with path smoothing, local avoidance, crushing, stuck detection and repathing. |
| Behaviour | `Entities/UnitBehaviorSystem.cs` | The order state machine: target acquisition by stance, chasing, transports, engineers, medics and mechanics, aircraft sorties with ammunition and rearming on hangar pads, and submarines. |
| Combat | `Combat/CombatSystem.cs` | Handles weapons with clips and reload, ten projectile kinds (hitscan, bullet, shell, ballistic, missile, flame, torpedo, bomb, beam, rail), splash, warhead × armour tables, suppression, burning ground, EMP, stealth and detection, veterancy (3 ranks) and structure weapons that need power. |
| Economy | `Economy/EconomySystem.cs` | Harvester cycle (seek, harvest, return, dock, unload), refinery docking queues, silo storage cap, ore regrowth from vents and power balance. |
| Production | `Production/ProductionSystem.cs` | One queue per category (structures, defences, infantry, vehicles, aircraft, naval, research), with pay-as-you-go cost, hold and cancel, a ready-to-place state, placement rules near your base, selling and repair. Low power slows production. |
| Support powers | `Combat/SupportPowerSystem.cs` | Charge timers, strikes with launch warnings, countermeasure interception and reduction, reveal zones and paradrops. |
| Fog of war | `FogOfWar/FogOfWar.cs` | Per-team visible and explored grids. Structures are remembered as last seen. Modes are shroud, explored and off. |
| Victory | `Core/VictorySystem.cs` | Destroy structures, annihilation or commander (lose every construction yard and mobile HQ). |
| AI | `AI/SkirmishAI.cs`, `AI/AIIntel.cs`, `AI/AIProfile.cs` | See below. |
| Campaign | `Missions/*.cs` | `MissionRunner` offers a scripting vocabulary: players, zones, objectives, triggers (`When`, `At`, `Every`), countdowns, spawns, base presets, reinforcements, attacks, dialogue, camera moves, reveals and cinematic mode. |
| Saves | `Core/SaveGame.cs` | Versioned binary snapshots built by reflection over public fields. See "Saving" below. |

### Events

Systems call `World.Emit(SimEvent)` for anything the presentation should react to, such as shots, impacts, deaths,
construction, alerts, mission dialogue and objective changes. The simulation never clears `World.Events`; the
consumer does. `GameSession` clears it after turning events into effects, audio and notifications, and the headless
harness clears it after each tick.

### AI

Each AI player gets a `SkirmishAI`. It works in layers:

* **Intel** (`AIIntel`) only sees what fog of war allows. It remembers enemy structures it has seen, the
  composition of enemy forces and which start positions it has scouted.
* **Economy and base:** power first, then refineries and the harvester fleet, before production and tech
  buildings. It saves up for missing harvesters, expands with a second mobile HQ to free ore fields, builds silos when
  storage is nearly full, and builds a countermeasure once it learns the enemy has a superweapon.
* **Production** weighs the units it can build against the enemy composition it has seen. How strongly it counters
  depends on difficulty.
* **Military:** units get roles (rally, defence, attack, harass, scout, air, naval, retreat). The AI defends threats
  near its base and harvesters, launches attack waves when army value passes a threshold (lower if the wave is
  overdue), flanks and focus-fires on higher difficulties, retreats damaged units for repair, and uses support powers.
* **Team play:** a `TeamBlackboard` per team shares the attack target, launch time and threat reports, so allied AIs
  join each other's attacks and defend each other, including a human ally's base.

Difficulty (`AIProfile`) changes reaction time, decision quality, expansion, tech ceiling, attack timing, tactics and
counter-building. It never gives free resources or combat bonuses.

In missions, AI players use the same controller. Mission flags can restrict it (`AIBuilds`, `AIAttacks`,
`AIHarvests`). Tagged, script-placed units are left to the script.

## Client

| Area | Files | Notes |
|---|---|---|
| Boot and flow | `Core/GameRoot.cs` | Created by `RuntimeInitializeOnLoadMethod`. Owns the screen state machine (menus, loading, in-game, pause, results), a background demo battle behind the menus, saving and loading, and global keys. |
| Session | `Core/GameSession.cs` | Wraps one `World`: fixed-step accumulator with interpolation alpha, game speed, event processing into effects, audio, notifications, subtitles and minimap pings, and end-of-game detection. |
| Rendering | `Rendering/*.cs` | `MeshBuilder` builds every mesh in code with vertex colours. `ModelLibrary` holds the procedural unit and structure models for both faction styles. `TerrainRenderer` builds chunked terrain with water, decoration and ore. `EntityViews` handles interpolation, turrets, recoil, rotors, banking and build-up and sell animations. `OverlayRenderer` draws selection, health, orders, placement ghosts and range rings. `FogTexture` feeds the fog grid to shaders. |
| Imported models | `Rendering/Models/ImportedModels.cs`, `RtsmFile.cs` | Replace procedural models per key with Blender-built models from `Resources/Models/<key>_<ca\|id>.bytes` (made by `Tools/BlenderModels`) or with a prefab/FBX of the same name. Magenta vertices become team colour; vertex alpha below 1 glows. Keys without a file keep the procedural model. Animated pieces (walking legs, wheels, tracks, recoil, spinners, lights, flags) are driven in `EntityViews.Animate`. |
| Effects | `Effects/FxRenderer.cs` | Batched billboard particles, tracers, projectiles and burning ground in two dynamic meshes. |
| Camera | `Camera/RTSCamera.cs` | Orthographic isometric camera (50° pitch, 45° yaw in 90° steps), with edge, key and drag scrolling, zoom, shake, and picking by ray-marching the height field. |
| Input | `Input/InputState.cs`, `Input/PlayerController.cs` | Input is collected from IMGUI events, so it works with either Unity input backend, and has rebindable key bindings. `PlayerController` handles selection, control groups, context orders and cursor modes. |
| UI | `UI/*.cs` | IMGUI scaled to a 1080-pixel virtual height. The HUD has a top bar, sidebar with cameo grid and production, minimap, selection panel, support powers, objectives, messages and subtitles. `Menus` covers every menu screen and the Field Manual. Icons are rendered from the 3D models offscreen. |
| Audio | `Audio/SynthLibrary.cs`, `Audio/AudioManager.cs` | All sounds and music are synthesised at start-up. Five buses (Master, Music, SFX, Voice, UI), a 32-voice pool with priority stealing, positional sound around the camera focus and crossfaded music. |
| Saves and settings | `SaveSystem/SaveManager.cs`, `Core/ClientSettings.cs` | Save slots (`.sav` + `.json` metadata), campaign progress and settings, all in `Application.persistentDataPath`. |
| Data assets | `Data/*.cs` | Optional ScriptableObjects: `DefinitionDatabase` (designer overrides of unit, building and weapon stats, loaded from `Resources/RTSDefinitions`) and `FactionStyle`. |

### Shaders

`Resources/Shaders` holds four small shaders: two-sided vertex-lit, transparent, additive and animated water. They
have no `LightMode` tag, so they render under URP (as `SRPDefaultUnlit`) and under the built-in pipeline. Lighting,
ambient light and fog of war come from global shader properties (`_RTSLightDir`, `_RTSAmbient`, `_RTSSun`,
`_RTSFogTex`, `_RTSFogParams`), which `SceneLighting` and `FogTexture` set.

## Saving

`SaveGame.Capture` copies the world into a `WorldSnapshot`:

* settings, tick and random state
* players
* units, buildings, projectiles and fires
* map arrays
* fog explored grids
* pending strikes and reveal zones
* mission state: fired triggers, flags, objectives and countdowns
* the camera

`SimSerializer` writes the snapshot as versioned binary (magic `RTS1`). It reflects over public, non-`[NonSerialized]`
fields in name order.

`Restore` works as follows:

* **Missions:** rebuilds the mission with `CreateMission(def, difficulty, restoring: true)`, so trigger closures exist
  again, then overlays the saved state.
* **Skirmish:** regenerates the map from its spec and overlays the saved arrays.

Definitions, spatial grids, fog and powers are relinked after loading. AI controllers are recreated fresh (see
known limitations).

## Extending

* **New unit or structure:** add it in `Catalog.cs` (`U(...)` / `B(...)`), add a model builder case in
  `ModelLibrary` for its model key, and add it to the faction's list. For the high-detail look, also add a Blender model for the key in
  `Tools/BlenderModels` (see its README), run `python3 build.py <key>` and check it with `Tools/ModelCheck`. The Field Manual, sidebar, icons and AI pick it
  up automatically.
* **New map:** add a `MapSpec` in `MapLibrary.cs`, then run `dotnet run -c Release -- maps` in `Tools/HeadlessSim` to
  check ore near each start, and `skirmish --only=<id>` to watch AIs play it.
* **New mission:** add a `MissionDef` in `AllianceCampaign.cs` or `DominionCampaign.cs`, then run
  `missions --only=<id>` and `probe --only=<id>` to check its first minutes.
* **Balance:** edit `Catalog.cs`, or export a `DefinitionDatabase` asset (RTS → Data → Export Definitions Asset) and
  tune it in the inspector. Overrides apply at start-up. `units` and `balance` in the harness print stat tables and
  faction win rates.
