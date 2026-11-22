# Development checklist

Status of each part of the original brief.

* ✅ implemented, and exercised by the headless tests where it is simulation logic
* 🟡 implemented, but only compile-checked (client code that needs the Unity editor to see running)
* ❌ not done

## Project and architecture

| Item | Status | Where |
|---|---|---|
| Unity 6000.6.9f1 project, URP, Windows target | 🟡 | `ProjectSettings/ProjectVersion.txt`, `Editor/ProjectSetup.cs` installs and assigns URP |
| Modular architecture with ScriptableObject data | 🟡 | Simulation and client assemblies; `DefinitionDatabase` and `FactionStyle` ScriptableObjects override the code catalog |
| Original content only (names, art, audio, maps, story) | ✅ | Everything is generated procedurally; no imported assets |
| Performance | ✅ / 🟡 | Simulation measured at ≤0.33 ms per tick with 200+ units; batched effects meshes and chunked terrain in the client, frame rate not measured |

## Gameplay systems

| Item | Status | Notes |
|---|---|---|
| Two original factions | ✅ | Continental Alliance, Iron Dominion: 37 units and 28 structures each |
| Base building, placement rules, sell, repair | ✅ | Placement near own structures, wall drag, rotation, power drain |
| Ore economy, refineries, silos, ore regrowth, gem-grade crystals | ✅ | |
| Power grid, low-power penalties | ✅ | Slower production, defences and radar offline |
| Infantry, vehicles, aircraft (ammo + rearming on pads), naval incl. submarines | ✅ | |
| Commands: move, attack-move, attack, force fire/move, guard, patrol, stop, scatter, deploy, waypoints | ✅ / 🟡 | Simulation ✅, input handling 🟡 |
| Formations, stances (4), veterancy (3 ranks) | ✅ | |
| Projectiles and armour/warhead tables | ✅ | 10 projectile kinds, 9 armour types |
| Tech research and tech tiers | ✅ | 9 technologies per faction |
| Varied terrain maps, 1v1 and 2v2 | ✅ | 7 maps, 4 themes; checked for ore near every start |
| Fair fog of war (AI sees only what fog allows) | ✅ | Shroud, explored or off |
| Layered AI, 4 difficulties, 2v2 coordination | ✅ | Tested 1v1, 2v2 and difficulty ladder |
| Superweapons with countermeasures | ✅ | 6 support powers, 2 countermeasure structures |
| Capturable tech structures, engineers, transports, medics and mechanics | ✅ | |

## Campaign

| Item | Status | Notes |
|---|---|---|
| 12 Alliance + 12 Dominion missions with scripting | ✅ | All load, run, save and load headlessly. 4 won by the autopilot |
| Intro cinematic | ✅ / 🟡 | Scripted prologue completes in simulation; camera and letterbox presentation 🟡 |
| Briefings, debriefings, progress and unlocks | 🟡 | Teletype briefing screen, results screen, `campaign.json` progress |
| Mission saves | ✅ | Round trip tested on every mission |

## Modes, UI, audio, settings

| Item | Status |
|---|---|
| Skirmish setup with all settings, spectator mode | 🟡 |
| Main menu with live background battle, campaign screen, load and save, options, credits, Field Manual | 🟡 |
| HUD: sidebar, production tabs, minimap with radar requirement, selection panel, support powers, objectives, messages, subtitles | 🟡 |
| Audio buses, synthesised SFX and music, voice pool, positional audio | 🟡 |
| Settings (audio, graphics, gameplay, controls rebinding) persisted to JSON | 🟡 |
| Save/load with slots, quick save and quick load | ✅ (simulation) / 🟡 (UI) |

## Deliverables

| Item | Status |
|---|---|
| Windows build | ❌ Needs the Unity editor. Build script and CLI are ready (`RTS.EditorTools.BuildScript.BuildWindows`) |
| README with setup, build and launch steps | ✅ `README.md` |
| Architecture and controls documentation | ✅ `docs/architecture.md`, `docs/controls.md` |
| Testing report | ✅ `docs/testing-report.md`, raw log in `docs/qa/headless-run.log` |
| Known limitations | ✅ `docs/known-limitations.md` |
| Automated tests | ✅ `Tools/HeadlessSim` (13 suites) |
| Git: `dev` branch pushed, PR merged to `main` | see the final commit on `main` |

## Next steps for whoever opens the project in Unity

1. Open with 6000.6.9f1 and let the setup script install URP. Fix any Unity 6 API errors in the Console.
2. Commit the generated `.meta` files, `ProjectSettings` and URP assets.
3. Play the checks listed in `known-limitations.md`.
4. Build Windows with RTS → Build → Windows (64-bit) and attach the build to a release.
