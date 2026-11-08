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
| Real game | Phase 2 (data) done; Phase 3 (core loop greybox) built in `Assets/_Project/`, scene `Assets/_Project/Scenes/Game.unity` |

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
- [ ] Play it yourself for 5 minutes (scene `Assets/_Project/Scenes/Game.unity`, portrait Game view 1080×1920)

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

**Done when:** you can tap meteors for 5 minutes and it's already a bit satisfying with no upgrades.

---

## Phase 4 — Upgrades and economy

- [ ] `UpgradeTrack` runtime: level, next cost, apply effect
- [ ] Upgrade tracks: Drill damage, Fire rate, Crit chance, Collector radius, Cargo capacity
- [ ] **Auto-fire:** the Pulse Laser tool tier fires at the nearest meteor on a timer
- [ ] Upgrades panel UI: scrolling list; cost shown and greyed out when unaffordable; buy ×1 / ×10 / max
- [ ] Auto-sell threshold setting ("sell anything below Standard")
- [ ] **Depth levels** within a sector: HP ×1.18 and value ×1.15 per depth (the GDD's formulas)
- [ ] Build the **economy spreadsheet** (Google Sheets or Excel) with the same formulas, and simulate the first 30 minutes
- [ ] Debug panel (development builds only): add credits, speed up time ×10, reset save

**Done when:** in the first 5 minutes the player can buy something every 5–15 seconds, matching the GDD's pacing table.

---

## Phase 5 — Save system and offline progress

Do this early. Mobile operating systems kill apps without warning, and saving gets harder to add the more systems exist.

- [ ] `SaveData` class (plain C#), serialised to JSON in `Application.persistentDataPath`
- [ ] Save on: app pause or focus loss, every 30 seconds, after purchases
- [ ] Write to a temp file, then rename it (so a crash mid-save can't corrupt the save)
- [ ] Save version number, for migrating old saves later
- [ ] **Offline earnings:** on return, estimate income per second × time away, capped at 8 hours; show a "Welcome back" popup
- [ ] Clock-cheat guard: if the saved time is in the future, ignore the time away

**Done when:** you can kill the app at any moment, reopen it and lose nothing, and offline earnings look right.

---

## Phase 6 — Sector 2 and drones (the idle engine)

- [ ] Data: Iron, Nickel; Metallic meteor type; Sector 2 definition
- [ ] **Jump drive:** an upgrade that unlocks the next sector, paid with the current sector's signature mineral
- [ ] Sector select: travel between unlocked sectors and depths
- [ ] **Drones:** orbit the ship, pick targets and deal damage; upgrades for count, damage and targeting
- [ ] Income-per-second readout (used for offline earnings too)
- [ ] Milestones: "Mine 1,000 Iron" style one-off rewards

**Done when:** you can leave the game running with no input and progress steadily; reaching Sector 2 takes about 10 minutes.

---

## Phase 7 — Sector 3, refinery and scanner (the purity hook)

- [ ] Data: Silver, Quartz; Crystalline meteors; Sector 3
- [ ] **Refinery:** queue a batch of one mineral; each cycle has a chance to raise the grade; success odds and duration per grade
- [ ] Refinery upgrades: speed, success chance, batch size, Stabiliser (prevents mass loss)
- [ ] Refinery UI: queue, progress bar, collect or sell
- [ ] **Scanner:** shows the composition (and later the purity) of meteors in range; alert for rare meteors
- [ ] Large-meteor **core bonus**: the last chunks roll +1 purity grade
- [ ] **Mineral Codex:** each mineral × grade is an entry; filling one gives a permanent small value bonus

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
