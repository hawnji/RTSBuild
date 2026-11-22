# AURITE FRONT: War for Veldmark

An original real-time strategy game in the classic base-building tradition, built in **Unity 6000.6.9f1** with the
Universal Render Pipeline and an isometric camera. Two original factions — the **Continental Alliance** and the
**Iron Dominion** — fight over Aurite, a crystalline ore, across the fictional region of Veldmark.

All art, audio, names, maps and story are original and generated procedurally in code: there are no imported models,
textures, sounds or music files, and nothing is taken from any existing game.

> **Status:** feature-complete code base. The simulation has been tested headlessly (see
> [docs/testing-report.md](docs/testing-report.md)). The Unity client and editor scripts compile against Unity
> reference assemblies, but the project has **not yet been opened, played or built in the Unity editor**, and no
> Windows `.exe` has been produced yet. See [docs/known-limitations.md](docs/known-limitations.md) before shipping.

## Contents

| | |
|---|---|
| Factions | 2, each with 37 units (infantry, vehicles, aircraft, naval) and 28 structures |
| Skirmish maps | 7: four 1v1 maps and three 4-player maps (2v2 or free-for-all) |
| Campaign | Prologue cinematic, 12 Alliance missions and 12 Dominion missions with briefings, debriefings and scripting |
| AI | Layered skirmish AI with 4 difficulties (Easy, Normal, Hard, Brutal) and team coordination for 2v2 |
| Superweapons | Helios Orbital Lance, Hammerfall Missile, Pulse Array (EMP), Grand Battery, Skyeye Reveal, Paradrop; each faction has a countermeasure structure |
| Research | 9 technologies per faction |
| Saves | Full save/load in skirmish and campaign, quick save (F6) and quick load (F9) |

## Requirements

* Unity **6000.6.9f1** (Windows editor recommended for Windows builds; the *Windows Build Support (Mono or IL2CPP)*
  module is needed to build the player).
* No third-party packages. The Universal Render Pipeline package is installed automatically (see below).

## Opening the project

1. Clone the repository and open the folder in Unity Hub with editor version 6000.6.9f1.
2. On first load, `Assets/_Project/Editor/ProjectSetup.cs` runs once per editor session and:
   * creates `Assets/_Project/Scenes/Boot/Boot.unity` with a camera and adds it to the build settings,
   * sets the product name ("Aurite Front"), company name and default resolution,
   * installs `com.unity.render-pipelines.universal` through the Package Manager if it is missing,
   * creates a URP asset and renderer in `Assets/_Project/Settings` and assigns them to Graphics and every Quality level.

   You can re-run any of these from the **RTS → Setup** menu.
3. Open `Assets/_Project/Scenes/Boot/Boot.unity` and press **Play**.

The whole game boots from code: `GameRoot` is created automatically after the first scene loads
(`RuntimeInitializeOnLoadMethod`), so the Boot scene only needs a camera.

## Building for Windows

From the editor: **RTS → Build → Windows (64-bit)** or **Windows (64-bit, development)**.
The player is written to `Builds/Windows/AuriteFront.exe`.

From the command line:

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" -batchmode -quit -projectPath . ^
  -executeMethod RTS.EditorTools.BuildScript.BuildWindows -buildPath Builds/Windows/AuriteFront.exe -logFile build.log
```

Add `-development` for a development build. The method exits with code 0 on success and 1 on failure.

## Playing

* **Main menu:** Campaign, Skirmish, Load Game, Options, Field Manual (encyclopedia of every unit and structure), Credits.
* **Campaign:** choose a faction. Missions unlock in order; the first launch plays the prologue cinematic.
* **Skirmish:** pick a map, up to 4 slots (human, AI of any difficulty, or closed), faction, team, colour and start
  position; starting credits, base and units, fog mode, superweapons, victory condition, ore regrowth and game speed.
  A spectator mode lets you watch AI-only matches.

Controls are listed in [docs/controls.md](docs/controls.md); all keys can be rebound in Options → Controls.

## Repository layout

```
Assets/_Project/
  Scripts/Simulation/   engine-independent game simulation (no UnityEngine references)
  Scripts/Client/       Unity presentation: rendering, input, UI, audio, saves
  Editor/               project setup, build script, content tools
  Resources/Shaders/    four small vertex-colour shaders (lit, transparent, additive, water)
Tools/
  HeadlessSim/          .NET console harness that runs the simulation without Unity (QA)
  ClientCheck/          compile check of simulation + client against Unity reference assemblies
  EditorCheck/          compile check of the editor scripts
docs/                   architecture, controls, checklist, testing report, known limitations
```

## Documentation

* [docs/architecture.md](docs/architecture.md): how the code is organised and how to extend it
* [docs/controls.md](docs/controls.md): keyboard and mouse reference
* [docs/development-checklist.md](docs/development-checklist.md): feature checklist against the original brief
* [docs/testing-report.md](docs/testing-report.md): what was tested, how, and the results
* [docs/known-limitations.md](docs/known-limitations.md): what is not done or not verified

## Running the headless tests

Requires the .NET 8 SDK.

```sh
cd Tools/HeadlessSim
dotnet run -c Release -- all            # every suite
dotnet run -c Release -- skirmish --minutes 40
dotnet run -c Release -- missions       # also: teams, autopilot, save, determinism, data, maps, balance, slots, units
```

The exit code is the number of failed checks. Set `QA_REPORT=<file>` to append the report to a file.
