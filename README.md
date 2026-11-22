# Emberhold

A medieval city-builder with night defense, for mobile (Android first, then iOS).
Engine: **Unity 6 LTS, Universal Render Pipeline (URP)**.

## How to run it

1. Install **Unity Hub** and **Unity 6 LTS** (add the *Android Build Support* module).
2. In Unity Hub: **New project → Universal 3D** template, name it `Emberhold`.
3. Copy this repo's `Assets/Emberhold` folder into the new project's `Assets` folder.
4. In Unity: menu **Emberhold → Open Game Scene**, then press **Play**.

The whole game is built from code at start-up, so there is no scene to set up by hand.
Use **Emberhold → Delete Save** to start a fresh city.

## Controls

| Action | Touch | Mouse |
|---|---|---|
| Move camera | drag empty ground | drag |
| Zoom | pinch | scroll wheel |
| Build | **Build** / **Defenses** button, pick a card, drag the ghost, **OK** | same |
| Move a building | drag it | drag it |
| Rotate | small **Rotate** button beside it | same |
| Start the night early | **Night** button | same |

## Game loop (current skeleton)

- **Day (150 s):** build, gather. Farms make food, lumberjacks chop trees (trees regrow),
  quarries make stone, the workshop turns wood + stone into weapons, houses pay gold.
- **People:** each villager lives in a house, walks to a job, eats food. Houses add capacity.
  At dawn, settlers ask to join (Accept / Decline).
- **Upgrades:** timed in real time (keep running while the app is closed). The castle level
  limits other upgrades and grows the land you can build on.
- **Night:** fog and darkness fall. Enemies come from random, unannounced directions and are
  only visible inside light (castle, torches, watchtowers). Walls block them; watchtowers and
  the castle shoot arrows. The first 3 nights are gentle; every 5th night has a boss.
- **Map:** mountain to the north, sea to the south, forest east and west.

## Code layout (`Assets/Emberhold/Scripts`)

| Folder | What lives there |
|---|---|
| `Data` | `BuildingCatalog` (all balance numbers), `GameState` (save data) |
| `Core` | `GameBootstrap` (builds the game), `GameManager` (rules), save, audio, camera, input handling |
| `World` | grid, map generation, trees |
| `Buildings` | building behaviour and placeholder models |
| `Night` | day/night cycle, enemies, spawner, lights, arrows |
| `People` | villagers |
| `UI` | the HUD, built from code |

### Built to grow

- **Art:** every building looks for a prefab at `Resources/Models/<id>` (e.g. `Models/house`,
  `Models/villager`). Drop in free CC0 models (Kenney, Quaternius) and they replace the placeholders,
  no code change.
- **Sound:** clips load from `Resources/Audio/<name>`. Missing ones are skipped, so `owl`, `rooster`,
  `horn`, `day_ambience`, `night_ambience` can be added any time.
- **Save:** `GameState` is plain serializable data behind `ISaveBackend`, ready for a cloud save
  (Unity Cloud Save / PlayFab) when friends and alliances come.
- **Balance:** all numbers sit in `BuildingCatalog` and can move to Remote Config later.
