# Known limitations

## Not verified (no Unity editor was available)

1. **The project has never been opened in Unity 6000.6.9f1.** The code compiles against older Unity reference
   assemblies (2021.3 engine modules, 2021.1 editor). If Unity 6 reports a compile error, it will most likely come
   from a renamed API in the editor scripts (`ProjectSetup.cs`, which reaches URP types by reflection for this reason).
2. **No Windows build has been produced.** Use RTS → Build → Windows (64-bit) or the command line in the README.
3. **Shaders have not been compiled by Unity.** They are plain CG vertex/fragment shaders with no lighting passes, so
   they should work in URP and the built-in pipeline. If any material renders pink, check the Console for the shader
   name.
4. **Rendering, UI layout, input, audio and frame rate have not been seen running.** Things to check on first launch:
   * Main menu with the background demo battle
   * Skirmish start
   * Building placement
   * Box selection
   * The minimap
   * Save and load
   * Options → Graphics changes
   * A campaign mission briefing
   * The prologue cinematic
5. **URP is installed by an editor script on first load**, instead of being listed in `Packages/manifest.json`, so
   that Unity resolves the version that matches 6000.6.9f1. If package installation is blocked (offline), the game
   still renders with the built-in pipeline, because the shaders support both.
6. **No `.meta` files are committed.** Unity creates them on first import. Commit them after the first open, so asset
   GUIDs stay stable.

## Design simplifications

* **AI state is not saved.** Loading a skirmish or mission recreates the AI controllers. They re-learn the map within
  seconds, but forget scouting memory and their current attack wave.
* **Voices** are synthesised radio chirps with on-screen subtitles, not recorded speech. All other audio and music
  is synthesised at start-up too.
* **Audio mixing** uses per-bus gain on pooled `AudioSource`s rather than a Unity `AudioMixer` asset, because no
  binary assets are committed.
* **Art** is procedural low-poly geometry with vertex colours. There are no textures, normal maps or skeletal
  animation. Infantry move with procedural bobbing.
* **UI** uses Unity IMGUI (scaled to resolution) rather than UI Toolkit or uGUI, so that it can be generated
  entirely in code.
* **No multiplayer.** Skirmish is single player against AI, or AI only.
* **Mission scripting** is C# code in the simulation assembly rather than ScriptableObject assets. Unit, building and
  weapon stats can be overridden by a `DefinitionDatabase` ScriptableObject, but missions and maps are code.
* **Balance** has been tuned against AI-vs-AI results only, with no human playtesting.

## Known gameplay rough edges

* The AI controlling a player's ally does not recognise mission-specific objectives (only relevant to allied AIs in
  missions such as Ironhold).
* The cursor offers "Attack" over neutral units. In The Prisoners, the prisoners are neutral until freed, so they can
  be attacked by mistake with a deliberate right click.
* Escort missions (Iron Convoy, Colossus Road) rely on the player keeping units near the convoy. Trucks stop and
  wait when no escort is near.
