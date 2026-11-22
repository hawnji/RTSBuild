# Testing report

Date: 2026-11-22. Branch `dev`. The raw log of the final full run is in [qa/headless-run.log](qa/headless-run.log).

## What could and could not be tested

The work was done in a Linux container **without the Unity editor**. That shaped the testing:

| Check | Method | Result |
|---|---|---|
| Simulation compiles | `Tools/HeadlessSim` (.NET 8) | ✅ |
| Simulation behaviour | Headless suites below, running the real simulation code | ✅ 0 failures in the final run |
| Client compiles | `Tools/ClientCheck`: simulation + client against UnityEngine module reference assemblies (2021.3 API surface) | ✅ |
| Editor scripts compile | `Tools/EditorCheck`: adds UnityEditor reference assembly, `UNITY_EDITOR` defined | ✅ |
| Shaders compile | Not possible without Unity | ❌ not verified |
| Game runs in Unity 6000.6.9f1, rendering, UI, input, audio | Needs the editor | ❌ not verified |
| Windows build | Needs the editor with Windows build support | ❌ no `.exe` produced |
| Frame rate in the client | Needs the editor | ❌ not measured (simulation cost measured below) |

The reference assemblies are older than Unity 6, so APIs that changed or were removed in Unity 6 could still fail
to compile there. The client only uses long-standing APIs (IMGUI, `Mesh`, `Graphics.DrawMesh`, `AudioSource`,
`Texture2D`, `JsonUtility`), so the risk is low, but it is not zero.

## Headless test suites

Run with `dotnet run -c Release -- <suite>` in `Tools/HeadlessSim`. Every run checks these invariants every
10 seconds of game time:

* no NaN positions or health
* units stay in bounds
* health never exceeds the maximum
* the id map and building grid agree with the entity lists
* no negative credits

AI exceptions are caught and reported.

| Suite | What it does |
|---|---|
| `skirmish` | AI vs AI 1v1 on all 7 maps, rotating through all four difficulties, plus a difficulty ladder (Easy vs Hard, Normal vs Brutal, both sides) |
| `teams` | 2v2 AI matches on the three 4-player maps (mixed factions, Hard + Normal per team) |
| `missions` | Each of the 24 missions and the prologue for 8 minutes with an idle player: checks setup, objectives and trigger execution, then saves, loads and runs the loaded mission for 60 s |
| `autopilot` | Each mission for 12 minutes with a skirmish AI controlling the player (it does not know about objectives) |
| `save` | Save/load round trips at 0, 3 and 10 minutes in 1v1 and 2v2. The loaded world must match the original exactly (every unit position and health value, credits, ore) and keep running |
| `determinism` | Two 15-minute matches with the same seed must end in identical states |
| `data` | Every weapon reference resolves, health is positive, mission maps exist |
| `maps` | Ore available within 12 and 20 cells of every start position |
| `balance`, `slots`, `units` | Faction win rates with sides swapped, start-position bias, and a stat table of every unit |

## Final run (all suites, 40-minute limit per match)

**0 failures.** Simulation cost is 0.02–0.33 ms per tick (15 ticks per second) in matches with up to 211 units. One
naval mission under the autopilot peaked at about 1.3 ms per tick. Simulation stays a small fraction of a 60 fps
frame budget.

### Skirmish

| Map | Matchup | Result |
|---|---|---|
| Twin Rivers | Easy vs Normal | Normal wins, 10.7 min |
| Dust Basin | Normal vs Hard | Hard wins, 7.8 min |
| Frostbite Pass | Hard vs Brutal | Brutal wins, 14.4 min |
| Iron Strait | Brutal vs Easy | Brutal wins, 9.5 min |
| Crossroads | Easy vs Normal | Normal wins, 9.7 min |
| Scorched Plateau | Normal vs Hard | Normal wins, 12.4 min |
| Glacier Bay | Hard vs Brutal | Brutal wins, 8.6 min |
| Twin Rivers ladder | Easy vs Hard, both sides | Hard wins both (8.2 and 7.6 min) |
| Twin Rivers ladder | Normal vs Brutal, both sides | Brutal wins both (12.9 and 20.8 min) |
| Crossroads 2v2 | | Team 1 wins, 13.3 min, peak 211 units |
| Scorched Plateau 2v2 | | Team 1 wins, 37.4 min |
| Glacier Bay 2v2 | | Team 1 wins, 17.7 min |

Every match reached a decisive result, and the higher difficulty won 10 of 11 difficulty matches.

Separate samples:

* **Faction balance** (`balance`: 32 matches at equal Normal or Hard difficulty, 4 maps, sides swapped):
  Alliance 17, Dominion 13, 2 without a result.
* **Start-position bias** (`slots`: 16 mirror-faction matches): slot 0 won 9, slot 1 won 7.

These are AI-vs-AI results, not human playtesting.

### Campaign

All 25 scripted scenarios load, run 8 minutes without errors, and save and load mid-mission.

With an idle player:

* The prologue completes on its own.
* The defence missions Holding the Line, Kettle Pass and Iron Tide end in defeat after 6–8 minutes, as designed.
* No mission is won or lost in its first minute any more.

With the objective-unaware autopilot (a Normal skirmish AI playing the human side), First Light, The Foundry,
Severed Wings and Eye of Helios were won in under 12 minutes, so those victory paths work end to end. Escort, rescue
and defence missions need a human who protects the objective units, which the autopilot does not do.

## Bugs found by QA and fixed

| Area | Bug | Fix |
|---|---|---|
| Maps | Rotationally symmetric maps authored on the lower half had that half overwritten by the mirrored upper half. Twin Rivers had almost no ore (9,302 in total, none near either base), so AI economies starved and matches stalled. | The generator now mirrors from the half that holds the first start position. Twin Rivers now has 173,772 ore and 34,577 within 20 cells of each start. A `maps` check guards against this. |
| AI | Spent all money on tech buildings and units while short of harvesters. Second refinery depended on a narrow ore check. | Economy before tech, money kept back for missing harvesters, refinery rules relaxed, Easy gets 2 harvesters. |
| AI | Never launched attacks when its army stayed under the threshold. Scouts never rejoined the army. Retreated units stayed in the retreat role forever when a repair depot existed. | Overdue waves attack with a lower threshold, surplus defenders join, scouts and repaired units return to the army. |
| Balance | Dominion units were 40–230% more cost-efficient than their Alliance mirrors. Dominion won 14 of 16 equal-difficulty matches. | Cost and damage pass on 20 mirrored units (Hellbrand flame tank damage cut), retuned after the AI fixes. Result above. |
| AI | The AI sometimes built defences across the only ramp of its plateau (Dust Basin) and sealed its own base. Units then searched for paths every tick, and the simulation cost jumped to 2–3 ms per tick. | AI placement now rejects spots that cut the base off from the map (flood-fill check). Cost is back to about 0.05 ms per tick. |
| AI | Easy hoarded a large army at home and beat Hard, which attacked early. While an attack was running, new units piled up at the rally point. | Easy caps its army size. Hard and Brutal compare their army with the enemy army they have seen before attacking. Rally groups reinforce running attacks. |
| Economy | Harvesters kept crawling over thin regrowth cells next to depleted fields instead of driving to rich fields a little further away (Scorched Plateau matches stalled at 40 minutes). | Ore choice prefers cells with at least a quarter load within 28 cells, and weighs richness against distance. |
| Missions | Mission AI players took over script-placed units and marched them away, so trapped allies were killed within seconds (Broken Column failed after 6 s). | AI ignores tagged mission units. |
| Missions | Scripted building placement only checked the origin cell. Buildings overlapping preset bases corrupted the building grid (invariant failure), and in Hammer and Anvil and Ironhold the superweapon was never placed, so the mission was won at 0:00. | Full footprint check with a search for the nearest free spot. |
| Missions | Iron Convoy trucks drove into hidden ambushes before the player could act (lost at 0:18). | Trucks wait for an escort, sense ambushes at 11 cells, and start after 15 s. Briefing updated. |
| Missions | Iron Marshal: the target reached the airfield about 1 minute after leaving, although the radio says twelve minutes. | The column moves in legs and arrives after about ten minutes. |
| Missions | Severed Wings' "destroy the Skyshield batteries" objective completed at 0:05 because no batteries were placed. | Five batteries placed and tracked by tag. |
| Missions | The Prisoners: the prison guns shot the prisoners at once and the mission failed at 0:04. | Prisoners are non-hostile until freed. |

## Not covered by automated tests

* Anything visual or audible, menu flow, input handling, HUD layout at different resolutions.
* Human-vs-AI feel and difficulty curve, campaign pacing and briefing text quality.
* Long sessions in the client (memory, texture and mesh churn).
* Loading saves made with an older save version (only one version exists).

These need a play session in the Unity editor. [known-limitations.md](known-limitations.md) lists what to check
first.
