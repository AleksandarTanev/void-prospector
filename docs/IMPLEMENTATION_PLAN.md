# Void Prospector — Implementation Plan (Unity, Mobile)

How to build the game described in the Game Design Document (GDD), step by step.

- **GDD:** https://claude.ai/artifact/MXhwQ4eYDDQy4u3RpYsJHS
- **Engine:** Unity 6 (`6000.6.0f1`), 2D (URP 2D renderer)
- **Targets:** Android first, then iOS
- **Orientation:** portrait (the usual choice for idle games played one-handed; revisit if the meteor field feels cramped)

## Project status

| Item | State |
| --- | --- |
| Unity project | This repository, `void-prospector` (2D URP template) |
| Repository | `github.com/AleksandarTanev/void-prospector`, branch `main`; prototype work on branch `prototype` |
| Git LFS + `.gitattributes` | Done (images, audio, video, models, fonts, binaries via LFS; Unity YAML on LF with UnityYAMLMerge) |
| Input | New Input System only |
| Packages | Input System 1.20, uGUI 2.6 (includes TextMeshPro), Test Framework 1.8, URP 17.6 |
| Prototype | Built in `Assets/Prototype/` (Phase 1, steps 1–9); see [PROTOTYPE_PLAN.md](PROTOTYPE_PLAN.md) |
| Real game | Phases 2–5 done; Phases 6 (Sector 2, drones, milestones) and 7 (Sector 3, refinery, scanner, codex) built in `Assets/_Project/`, scene `Assets/_Project/Scenes/Game.unity` |

Work through the phases in order. Each phase ends with a **Done when** check. Don't start the next phase until it passes. The early phases are deliberately ugly: they prove that the game is fun before any art is made. Phase 1 is a quick, throwaway gameplay prototype; the real game is built from Phase 2 onward.

---

## Phase 0 — Project setup

**Done**
- [x] Install Unity 6 with the Android Build Support module
- [x] Create a **2D (URP)** project (`void-prospector/`)
- [x] Set up version control: git, GitHub remote, Unity `.gitignore`, Git LFS with `.gitattributes`
- [x] Packages: Input System, TextMeshPro (part of uGUI 2.x), Unity Test Framework
- [x] Input handling set to the new Input System only

**Still to do**
- [ ] Player Settings: portrait only (currently auto-rotate in all four directions)
- [ ] Player Settings: company name (currently `DefaultCompany`) and Android package name, e.g. `com.aleksandartanev.voidprospector` — can't change after publishing on Google Play
- [ ] Switch the active build target to Android (Build Profiles)
- [ ] Check in Unity Hub whether `6000.6` is an LTS release; plan to move to the current LTS before Phase 9 if it isn't
- [ ] Remove template leftovers: `Assets/Welcome/`, `Assets/Settings/Lit2DSceneTemplate.scenetemplate`
- [ ] Remove unused packages: Visual Scripting, Collab Proxy (Unity Version Control), Tutorials framework (`learn.iet-framework`). Keep `com.unity.ai.assistant` and `com.unity.ai.inference` (needed for the Unity connection Claude uses)
- [ ] Configure the UnityYAMLMerge merge driver in your global git config (commands at the bottom of `.gitattributes`)
- [x] Commit the Unity-generated settings changes (`ProjectSettings.asset`, `SceneTemplateSettings.json`) separately from feature work
- [x] Move this plan into the repo (`docs/IMPLEMENTATION_PLAN.md`) so it's version-controlled
- [ ] Reference resolution 1080×1920, Canvas Scaler set to "Scale With Screen Size" (the prototype already does this in code)
- [ ] Create the folder structure for the real game (below)
- [ ] Make a test build and run it on a real Android phone

```
Assets/
  _Project/
    Scripts/
      Core/        GameManager, time, save/load, big numbers
      Mining/      Meteors, spawning, damage, chunks
      Economy/     Wallet, cargo, selling, upgrades
      Idle/        Drones, refinery, offline earnings
      Progression/ Sectors, depth, codex, prestige
      UI/
    Data/          ScriptableObject assets (minerals, meteors, sectors, upgrades)
    Prefabs/
    Scenes/
    Art/
    Audio/
  Tests/
```

**Done when:** an empty scene builds and runs on your phone in portrait.

---

## Phase 1 — Gameplay prototype (throwaway)

Answer one question as fast as possible: **is cracking meteors and rolling purity fun?** Speed matters more than clean code here. Hard-coded numbers, primitive shapes and one big script are all fine. Keep it in its own `Assets/Prototype/` folder and scene so it doesn't leak into the real project.

Detailed plan, starting numbers and removal steps: [PROTOTYPE_PLAN.md](PROTOTYPE_PLAN.md).

**Built** (on branch `prototype`; scene `Assets/Prototype/Prototype.unity`, everything created at runtime by `ProtoBootstrap`)
- [x] One scene: the ship at the bottom, a field of 6–10 drifting meteors (circles)
- [x] Tap a meteor to damage it; damage numbers pop up
- [x] Meteors crack at 66% / 33% HP and break at 0, spilling chunks
- [x] Chunks roll a mineral (Rock, Copper, Tin, Iron) and a purity grade (Crude → Flawless), tinted by grade
- [x] Chunks fly to the ship; a cargo counter fills up
- [x] Sell button turns cargo into credits
- [x] 4 upgrades: drill damage, auto-fire, collector radius, drones
- [x] Refine button (raises purity of one mineral, with a chance of loss and a cooldown)
- [x] Juice: screen shake, particle bursts, damage numbers, glow, toast and generated sounds for rare drops, hit-stop on Flawless

**Still to do**
- [ ] Play it yourself in the editor with a portrait Game view (1080×1920); tune values live on `ProtoBootstrap`
- [x] Commit the prototype on the `prototype` branch
- [ ] Android build; play it on a phone; let 2–3 other people try it
- [ ] Fill in the Findings section of `PROTOTYPE_PLAN.md` and update the GDD

**Already learned while building it**
- Chunks that land outside the pickup radius and fade felt like lost loot: chunks now always drift to the ship, and the Collector upgrade makes collection faster instead of preventing loss. Consider the same rule for the real game.
- Drones orbiting around the ship were hidden by the bottom UI; they now orbit above it. The real game's layout should keep the ship's surroundings clear of UI.

**Technical notes for the real game**
- In this Unity version, `Light2D` lives in `Unity.RenderPipelines.Universal.2D.Runtime`; assembly definitions need that reference.
- Values serialized in a scene override changed code defaults. Keep balance data in ScriptableObjects (Phase 2) so there's one source of truth.
- `FindObjectsByType` with `FindObjectsSortMode` and `GetInstanceID()` are deprecated in 6000.6; use the newer overloads and `GetEntityId()`.

**Questions to answer:**
- Does tapping feel good for 5 minutes with no upgrades?
- Is a rare purity drop exciting, or does it go unnoticed?
- Does auto-fire or a drone make the game feel better or more boring?
- Portrait or landscape? 2D side view or top-down? (GDD open questions)

**Done when:** you've written down what's fun and what isn't, and updated the GDD with what you learned. Then put the prototype aside; the code isn't reused, only the lessons.

---

## Phase 2 — Data foundations

Put all balance numbers in **ScriptableObjects**, not in code. Then you can tune the game without recompiling, and the values can be checked against the economy spreadsheet.

- [x] `MineralDefinition`: id, name, tier, base value, colour, icon
- [x] `PurityTable` (grades): grade name, purity range, value multiplier, drop weight, glow, tint
- [x] `MeteorSizeDefinition`: HP multiplier, chunk count range, radius, spawn weight, core bonus
- [x] `MeteorTypeDefinition`: HP multiplier, mineral composition table (mineral + weight)
- [x] `SectorDefinition`: base HP, meteor type weights, max depth, signature mineral, unlock cost, depth scaling
- [x] `UpgradeDefinition`: id, base cost, growth rate, max level, cost mineral (empty = credits), effect type, Add/Multiply mode, effect per level
- [x] `GameDatabase`: references all data; lookup by id; `Validate()` checks ids, references, weights and purity ranges
- [x] Create assets for Sector 1: Rock, Copper, Tin (+ Iron, ready for Sector 2); Stony meteors; all five sizes; Debris Ring; the five Phase 4 upgrades
- [x] Number type: `GameNumber` struct wrapping `double`, ready to swap for BreakInfinity later
- [x] Number formatting: 7.5 → 999 → 1.23K → 4.56M → 7.89B … Dc, then scientific; truncated, never rounded up
- [x] GDD formulas in `Formulas`: upgrade cost, chunk value, meteor HP, Stardust, upgrade effect
- [x] EditMode tests (56 cases): formulas, formatter, `GameNumber`, weighted random, and validation of the real data assets

**Layout**
```
Assets/_Project/Scripts/   VoidProspector.asmdef — Core/ (GameNumber, NumberFormatter, WeightedRandom, Formulas), Data/ (ScriptableObjects)
Assets/_Project/Data/      GameDatabase.asset, Minerals/, Purity/, Meteors/Sizes/, Meteors/Types/, Sectors/, Upgrades/
Assets/Tests/EditMode/     VoidProspector.Tests.EditMode.asmdef
```
To add data: create the asset (right-click → Create → Void Prospector → …), give it a unique `id`, and add it to `GameDatabase`. `DataValidationTests` fails if something is inconsistent.

**Done when:** the Sector 1 data exists as assets and a unit test checks `cost(L) = base × g^L`. ✅

---

## Phase 3 — Core loop greybox (Sector 1)

Use coloured circles and squares only. Rebuild the core loop properly on top of the Phase 2 data, keeping what the prototype proved and dropping what it didn't.

- [x] **Meteor field:** a spawner keeps 6–12 meteors drifting slowly; respawn at the edges; object pooling from day one (`UnityEngine.Pool.ObjectPool`)
- [x] **Meteor:** HP = sector base × size × type; rolls its composition when it spawns; coloured veins hint at Copper/Tin inside
- [x] **Tap to damage:** Input System touch/mouse → hit test against meteor circles (with padding for fat fingers) → damage; a damage number pops up
- [x] **Cracking stages:** at 66% and 33% HP, spill a quarter of the chunks each, add crack lines and darken the tint
- [x] **Chunks:** spawned with a burst; each mineral is rolled at spawn, the purity grade when it spills; Large/Colossal cores give the last 3 chunks +1 grade
- [x] **Collector:** chunks inside the pickup radius fly to the ship; the rest drift slowly toward it, so nothing is lost unless the hold is full
- [x] **Cargo hold:** capacity limit (from the Cargo upgrade at level 0), shown as a fill bar; chunks bounce off and fade when it's full
- [x] **Sell button:** chunk values (base × purity multiplier) go into the credits wallet
- [x] **Basic juice:** small screen shake on hit, particle burst on crack, tint/glow by purity grade, toast + sting for Pristine+, hit-stop on Flawless, generated sounds
- [x] EditMode tests (73 in total): wallet, cargo hold, crack/spill logic, meteor and chunk rolls on the real data, selling
- [x] Play it yourself for 5 minutes (scene `Assets/_Project/Scenes/Game.unity`, portrait Game view 1080×1920)

**Layout**
```
Assets/_Project/Scenes/Game.unity   Main Camera, Global Light 2D, GameRoot (the only object you edit)
Assets/_Project/Data/MiningSettings.asset   field size, drift speed, crack thresholds, chunk speeds, juice — referenced by GameDatabase
Assets/_Project/Scripts/
  Core/      GameRoot (builds the world), GameSession (pure C# state: data, sector, wallet, cargo, rolls)
  Economy/   Wallet, CargoHold (pure C#)
  Mining/    MeteorState + MeteorRoller (pure C#), MeteorField, Meteor, ChunkField, Chunk, TapInput
  Fx/        GameFx (particles, shake, hit-stop, popups, toasts), GameAudio, ShapeSprites
  UI/        Hud, UiFactory (greybox uGUI built in code)
```
Game logic lives in plain C# classes so it can be unit-tested; MonoBehaviours only display it. Stats (damage, collector radius, cargo capacity) come from the upgrade assets at level 0, so Phase 4 only has to add levels. `Game.unity` is now first in the build list; the prototype scene stays second.

**Done when:** you can tap meteors for 5 minutes and it's already a bit satisfying with no upgrades. ✅

---

## Phase 4 — Upgrades and economy

- [x] `UpgradeTrack` runtime: level, next cost, effect, quotes for ×1 / ×10 / MAX; `Upgrades` spends credits from the wallet
- [x] Upgrade tracks: Drill damage, Fire rate (Auto-fire), Crit chance (×2 damage), Collector radius, Cargo capacity
- [x] **Auto-fire:** the Pulse Laser fires at the nearest meteor on a timer once the Auto-fire upgrade is bought; the collector radius is drawn as a faint ring
- [x] Upgrades panel UI: scrolling list; current → next effect; cost shown and greyed out when unaffordable; buy ×1 / ×10 / MAX toggle
- [x] Auto-sell threshold setting: off / below Standard / below Refined / below Pristine; matching chunks are sold the moment they're collected
- [x] **Depth levels** within a sector: HP ×1.18 and value +15% per depth; break 40 meteors at the deepest depth to unlock the next; `<` / `>` in the top bar to move between unlocked depths (chunks keep the value of the depth their meteor came from)
- [x] **Economy spreadsheet** `docs/economy/Economy.xlsx`: inputs, cost/effect per level, HP and value per depth, and three simulated first 30 minutes
- [x] Debug panel (editor and development builds; DEV button): +$1K, +$1M, speed ×10, unlock depths, restart (save reset comes with Phase 5)
- [x] EditMode tests (89 in total), including a pacing test that runs the simulator on the real data
- [ ] Play it yourself for 5–10 minutes and check the pacing feels right

**Economy tuning** (done with `EconomySimulator`, which plays the real `GameSession` without visuals: 3 taps/s, sells when full, always buys the cheapest upgrade, mines at the deepest depth)

| Upgrade | Base cost | Growth | Was |
| --- | --- | --- | --- |
| Drill damage | 25 | 1.20 | 10 × 1.15 |
| Auto-fire | 200 | 1.20 | 50 × 1.15 |
| Crit chance | 400 | 1.25 | 100 × 1.20 |
| Collector radius | 100 | 1.20 | 25 × 1.15 |
| Cargo capacity | 160 | 1.23 | 40 × 1.18 |

Median time between purchases (3 seeds): **6–7 s** in minutes 0–5 (target 5–15 s), 17–19 s in minutes 5–15, 37–39 s in minutes 15–30. With the old prices it was 2.6 s. The simulated player reaches depth 10 after about 7 minutes and buys Auto-fire at about 1.5 minutes.

To redo the spreadsheet after changing data: Unity menu **Void Prospector → Export Economy Data** (writes `docs/economy/data/`), then `python tools/economy/build_economy_xlsx.py docs/economy/data docs/economy/Economy.xlsx` (needs `openpyxl`), and open it in Excel.

**Open questions for playtesting**
- The GDD suggests taps deal bonus damage (e.g. 3×) over auto-fire. `MiningSettings.tapDamageMultiplier` is 1 for now so the Phase 3 feel doesn't change; try 2–3 once drones arrive.
- The GDD says depth also raises purity odds slightly; not done yet (needs a rule for shifting drop weights).
- Collector radius matters less now that chunks always drift to the ship. Consider turning it into a magnet-speed upgrade.

**Done when:** in the first 5 minutes the player can buy something every 5–15 seconds, matching the GDD's pacing table.

---

## Phase 5 — Save system and offline progress

Do this early. Mobile operating systems kill apps without warning, and saving gets harder to add the more systems exist.

- [x] `SaveData` class (plain C#), serialised to JSON in `Application.persistentDataPath`
- [x] Save on: app pause or focus loss, every 30 seconds, after purchases
- [x] Write to a temp file, then rename it (so a crash mid-save can't corrupt the save)
- [x] Save version number, for migrating old saves later
- [x] **Offline earnings:** on return, estimate income per second × time away, capped at 8 hours; show a "Welcome back" popup
- [x] Clock-cheat guard: if the saved time is in the future, ignore the time away
- [ ] Kill the app at random moments (editor and phone), reopen, check nothing is lost; check the offline numbers feel right

How it works:
- `Core/SaveData` (fields only, content by id) ⇄ `SaveSystem.Capture/Apply` ⇄ JSON. Unknown ids are skipped and out-of-range levels or depths clamped, with warnings, so data changes don't break old saves. `SaveSystem.CurrentVersion` = 1; `Migrate` is where format changes go.
- `SaveStore` writes `save.json.tmp` (flushed to disk), moves the old save to `save.json.bak`, then moves the temp file into place. Loading tries the save, then the backup.
- `SaveController` saves every 30 s (real time), after purchases, depth unlocks and auto-sell changes, and on pause, focus loss, quit and scene unload.
- `Idle/OfflineEarnings`: income per second is estimated by running `EconomySimulator` on a copy of the session for 10 minutes with no taps and no purchases (auto-fire only, at the current depth). Paid = rate × min(away, cap) × efficiency. Nothing below 60 s away; nothing if the auto-fire upgrade isn't bought (a toast says to buy it). Settings in `GameDatabase.offline` (cap 8 h, min 60 s, efficiency 1, sample 600 s).
- `OfflineController` pays when the game starts from a save and when the app resumes from the background. Credits go into the wallet and are saved immediately; the popup only reports them.
- Debug panel: "Away 1h", "Away 10h" (save as if written that long ago, then restart) and "Wipe save". "Restart" keeps the save.

Open questions:
- Offline efficiency: the GDD doesn't say. Currently 100% of the idle (auto-fire only) rate. Many idle games pay less while away; decide after playtesting.
- Rewarded "double offline earnings" (Phase 12) will need a Collect choice before the credits are added, and storing pending earnings in the save.
- Offline income ignores the cargo hold filling up (as if auto-sell were on). Fine for now; revisit with drones in Phase 6.

**Done when:** you can kill the app at any moment, reopen it and lose nothing, and offline earnings look right.

---

## Phase 6 — Sector 2 and drones (the idle engine)

- [x] Data: Iron, Nickel; Metallic meteor type; Sector 2 definition
- [x] **Jump drive:** an upgrade that unlocks the next sector, paid with the current sector's signature mineral
- [x] Sector select: travel between unlocked sectors and depths
- [x] **Drones:** orbit the ship, pick targets and deal damage; upgrades for count, damage and targeting
- [x] Income-per-second readout (used for offline earnings too)
- [x] Milestones: "Mine 1,000 Iron" style one-off rewards
- [x] EditMode tests (149 in total), including "reaching Sector 2 takes 7–14 minutes" and "idle with drones keeps buying" simulator checks
- [ ] Play it: reach the Iron Belt, buy drones, leave it alone for a few minutes; check the numbers feel right

How it works:
- **Sector 2, Iron Belt** (`Data/Sectors/IronBelt.asset`): base HP 40, Stony 35 / Metallic 65. **Metallic** meteors: HP ×1.5, Rock 25 / Iron 45 / Nickel 25 / Copper 5, reddish body. **Nickel**: tier 2, value 30. Each sector has its own background colour; each meteor type its own body colour.
- **Jump drive** = the next sector's `unlockCost`: Iron Belt costs **$5,000 + 40 Copper** from the cargo hold. It becomes available once the **last depth** of the furthest sector is unlocked. From then on, Sell and auto-sell **keep the cheapest Copper chunks** needed for it (the Sell button says so), and payment takes the cheapest chunks. The map button turns gold and a toast says when it's affordable.
- **Star Map** (tap the sector name in the top bar): each sector shows "here", **Travel**, **Buy jump drive** with the cost (green when you have it), or Locked; plus the milestone list with progress. Travelling saves and reloads the scene in the new sector. Each sector keeps its own depth progress; cargo, credits and upgrades go with the ship.
- **Drones** (shown and buyable once Sector 2 is unlocked: `UpgradeDefinition.requiresSector`): up to 6 orbit the ship. Each shoots every 0.8 s (`GameDatabase.drones.fireInterval`) for drill damage × Drone Damage (100% +25% per level), and can crit. **Drone Targeting** levels: random → nearest → weakest (finishes meteors) → richest (most valuable contents). Timers are pure (`Drones/DroneBay`) and live in the session, so the simulator and offline earnings include drones; the view (`DroneSwarm`) picks targets and draws beams.
- **Idle income readout** under the credits: the same estimate offline earnings use (simulate 10 min with no taps and no purchases), recalculated 0.3 s after purchases, depth changes and milestones.
- **Milestones** (`Data/Milestones/`, `Progression/Milestones`): Mine 200 Copper, Mine 150 Tin, Break 150 meteors in the Debris Ring, Reach depth 10 in the Debris Ring, Mine 1,000 Iron, Mine 500 Nickel, Reach depth 10 in the Iron Belt. Rewards: credits once and a permanent value bonus (one mineral or all), applied in `Formulas.ChunkValue` through the codex-bonus factor. Lifetime counters and completed milestones are saved.
- **Save version 2**: depth per sector, furthest unlocked sector, milestones and counters, value per cargo stack. Version 1 saves are migrated (the old depth becomes the current sector's).
- **Simulator**: drones fire (they don't wait for the tap player's retarget delay), and once the jump drive is available the player stops buying upgrades, saves for it, buys it and travels. The jump time is in `Result.jumps`.
- Debug panel: **Unlock sector** unlocks the next sector without paying.

Open questions:
- Tuning not checked yet: the simulator test expects the jump at 7–14 minutes (estimate: about 9–10). If it fails, change the Iron Belt's credit cost first.
- The GDD says drone upgrades are paid with Titanium and Nickel. Titanium only appears in Sector 4, so drones cost credits for now; Nickel costs could come with the refinery, using the same "keep in the hold" rule as the jump drive.
- Keeping Copper for the jump drive changes what Sell does. Watch whether players understand it; an alternative is a separate mineral stock.
- Drone shots don't play a sound (it would be constant); give drones their own quiet sound in Phase 9.
- Drone targeting only matters on screen: the simulator has one meteor at a time, so offline earnings ignore it.

**Done when:** you can leave the game running with no input and progress steadily; reaching Sector 2 takes about 10 minutes.

---

## Phase 7 — Sector 3, refinery and scanner (the purity hook)

- [x] Data: Silver, Quartz; Crystalline meteors; Sector 3
- [x] **Refinery:** queue a batch of one mineral; each cycle has a chance to raise the grade; success odds and duration per grade
- [x] Refinery upgrades: speed, success chance, batch size, Stabiliser (prevents mass loss)
- [x] Refinery UI: queue, progress bar, collect or sell
- [x] **Scanner:** shows the composition (and later the purity) of meteors in range; alert for rare meteors
- [x] Large-meteor **core bonus**: the last chunks roll +1 purity grade (already in since Phase 3: `MiningSettings.coreChunks`, Large and Colossal sizes)
- [x] **Mineral Codex:** each mineral × grade is an entry; filling one gives a permanent small value bonus
- [x] EditMode tests (170 in total), including "refining is worth it at low grades and a gamble at the top" and "reaching Sector 3 takes 18–50 minutes"
- [ ] Play it: reach the Crystal Drift, refine a few batches, and check that "sell now or refine?" is a real question

How it works:
- **Sector 3, Crystal Drift**: base HP 260, Stony 15 / Metallic 45 / Crystalline 40. Jump drive **$50,000 + 40 Iron** (the Iron Belt's signature mineral), after depth 10 of the Iron Belt. **Crystalline** meteors: HP ×0.6, Rock 20 / Quartz 40 / Silver 30 / Nickel 10, blue-violet. **Silver** 120, **Quartz** 140 (tier 3). New milestones: Mine 300 Quartz, Mine 300 Silver, Reach depth 10 in the Crystal Drift.
- **Refinery** (`Refining/Refinery`, pure; settings in `GameDatabase.refinery`): one batch of up to N chunks of one mineral and grade, loaded from a cargo stack. Each attempt takes the stage's time; it succeeds with the stage's chance (value × the grade ratio) or fails and loses a share of the chunks. It keeps trying until the **target grade** ("Stop at", default the top). Collect puts the batch back in the hold (only if all of it fits); Sell sells it directly. It runs during offline time too (capped like earnings) and the Welcome back popup reports it.

  | Step | Time | Chance | Loss on failure | Average per try |
  | --- | --- | --- | --- | --- |
  | Crude → Standard | 15 s | 85% | 10% | ×1.84 |
  | Standard → Refined | 30 s | 65% | 15% | ×1.92 |
  | Refined → Pristine | 60 s | 40% | 25% | ×1.65 |
  | Pristine → Flawless | 120 s | 15% | 50% | **×0.93** (a gamble) |

  Upgrades (from Sector 3): Refinery Speed (×1.15 per level), Refine Success (+3% per level, max 95%), Refinery Batch (10, +5 per level), Stabiliser (−20% loss per level; no loss at level 5). The dialog shows the odds and "×N on average" for the batch and for every stack in the hold, so the choice is visible. The decision: refining is time in a single slot, so cheap stacks are better sold; valuable stacks at low grades are worth refining; the last step loses on average until the Stabiliser or success upgrades are bought.
- **Mineral Codex** (`Progression/Codex`, CODEX button on the right): an entry per mineral × grade, filled when a chunk of that grade is collected or refined. Bonus to that mineral's value: +1% / 2% / 3% / 4% / 5% from Crude to Flawless (+15% for a full row). Multiplies with milestone bonuses (`Session.ValueMultiplier`). New entries from Standard up get a toast.
- **Scanner** (upgrade from Sector 3, range 3 m +0.7 per level): meteors within range show their remaining contents under them; rare meteors (4+ chunks of tier 3 or higher, `GameDatabase.scanner`) are marked RARE anywhere on screen and announced with a toast (at most every 8 s).
- **Save version 3**: refinery batch (mineral, grade, count, value, progress, target) and codex entries.
- HUD: CODEX and REFINE buttons on the right under the top bar. REFINE appears with Sector 3, shows the attempt's progress and turns gold when the batch is done.

Open questions:
- Refinery is a single slot (GDD open question). Parallel slots could be a later upgrade.
- The simulated player never refines, so pacing tests don't include it. If refining turns out to be a big income source, add a refining strategy to the simulator.
- "Later the purity" for the scanner: not done; a natural higher scanner level once purity is rolled at spawn instead of when chunks spill.
- Tuning not checked yet: Sector 3 is expected at 18–50 minutes (GDD ~30). Refinery and scanner prices are guesses relative to Sector 3 income.

**Done when:** the player faces a real choice between "sell now" and "refine first", and both are sometimes right.

---

## Phase 8 — Playtest and tune (MVP complete)

At this point the MVP checklist in the GDD is done (Sectors 1–3).

- [ ] Analytics events (Unity Analytics or Firebase): session length, time to each sector, upgrades bought, where players quit
- [ ] Hand out test builds to 5–10 people (Google Play internal testing track)
- [ ] Compare real timings with the GDD's pacing targets and adjust the ScriptableObject values
- [ ] Look for walls: any point where the next meaningful upgrade takes more than about 10 minutes of active play
- [ ] Answer the GDD's open questions using what you observed

**Done when:** testers play for 30+ minutes without being asked to, and the pacing roughly matches the targets.

---

## Phase 9 — Vertical slice (Sector 1 polished)

- [ ] Final art for the ship, Sector 1 meteors and chunks, and the background
- [ ] Each mineral gets one distinct colour; purity glow and particle tiers
- [ ] Audio: ambient track, hit and crack sounds, a pickup sound per mineral, a rare-drop sting
- [ ] UI pass: top bar, bottom tab panel, toasts
- [ ] Feel pass: hit-stop on big cracks, slow-motion beat on Flawless drops, coin cascade on selling
- [ ] Performance check on a cheap Android phone: steady 60 FPS, no stutter from creating objects

**Done when:** a 2-minute screen recording of Sector 1 looks like a real game.

---

## Phase 10 — Content build-out

- [ ] Sector 4: Gold, Titanium; Volatile meteors with chain explosions; **alloys** (crafting)
- [ ] Sector 5: Platinum, Iridium; hazards (solar flares, gravity wells); more Large and Colossal meteors
- [ ] Sector 6: Voidstone; Ancient meteors
- [ ] Tool tiers: Beam Laser, Fracture Cannon
- [ ] Colossal meteor event (warning, pull effect, big payout)
- [ ] Switch `GameNumber` to BreakInfinity if values get close to `double` limits

**Done when:** a full first run, from start to Sector 6, plays without gaps; it takes about 8–12 hours.

---

## Phase 11 — Prestige (Warp Reset)

- [ ] Stardust formula: `floor(sqrt(runCredits / 1e6)) + voidstoneBonus`
- [ ] Warp flow: confirmation screen showing the Stardust gained, a reset of run data, and kept data preserved (Stardust, Codex, achievements, skins)
- [ ] **Star Chart:** skill tree with the Extraction, Purity, Logistics, Exploration and Automation branches
- [ ] Void Lance tool (prestige unlock)
- [ ] Check in the spreadsheet: the first warp is worthwhile at 1–2 hours; the second run through Sectors 1–3 is 3–5× faster

**Done when:** warping feels like a reward, not a punishment, and players choose to do it.

---

## Phase 12 — Monetisation and launch prep

- [ ] Rewarded ads (Unity LevelPlay or AdMob): double offline earnings; 30-minute purity boost
- [ ] Unity IAP: Remove Ads, permanent 2× offline cap, supporter skin pack
- [ ] Cosmetics: ship hulls, beam colours, particle styles
- [ ] Privacy: GDPR/consent prompt; privacy policy URL
- [ ] Cloud save (Google Play Games / Game Center), optional but recommended
- [ ] Store listings: icon, screenshots, short video
- [ ] Closed beta → open beta → launch on Android; then iOS

---

## Conventions

- **Data-driven:** balance numbers live only in ScriptableObjects (and the spreadsheet). No magic numbers in code.
- **Pooling:** meteors, chunks, damage numbers and particles all come from pools.
- **One source of time:** a `GameClock` service handles the debug ×10 speed and offline calculations.
- **Event-driven UI:** systems raise C# events (`OnCreditsChanged`, `OnChunkCollected`); the UI listens and never polls.
- **Tests:** unit tests for formulas (costs, values, HP, Stardust, offline earnings) and for save/load round trips.
- **Commit after each checkbox** that works.
