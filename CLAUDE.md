# Void Prospector — notes for Claude

Idle mining game for mobile (portrait): tap and auto-mine meteors in space, collect mineral chunks with purity grades, sell, upgrade, go deeper and further. Original design; similar genre to Obelisk-style idle miners.

- **GDD:** https://claude.ai/artifact/MXhwQ4eYDDQy4u3RpYsJHS (Claude Doc)
- **Plan and status:** [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md). Work phase by phase; each phase has a "Done when" check. Phases 0 (partly), 2, 3, 4 and 5 are done; Phase 6 (Sector 2, jump drive, drones, milestones) is built and waiting for a playtest.
- **Prototype (throwaway):** `Assets/Prototype/`, see [docs/PROTOTYPE_PLAN.md](docs/PROTOTYPE_PLAN.md). Don't reuse its code or reference it.
- **Economy:** [docs/economy/Economy.xlsx](docs/economy/Economy.xlsx), built from `EconomySimulator` output (see "Economy tooling").

## Engine and packages

- Unity **6000.6.0f1**, 2D URP, **Input System only** (no legacy `Input`), portrait 1080×1920 reference resolution.
- uGUI 2.6 (includes TextMeshPro), Test Framework 1.8, URP 17.6.
- **TextMeshPro**: essentials imported in `Assets/TextMesh Pro/`.
- **DOTween** (free, Asset Store): `Assets/Plugins/Demigiant/DOTween/`, settings in `Assets/Resources/DOTweenSettings.asset`.
- **NuGetForUnity** (git package) and **R3 1.3.1** core from NuGet; DLLs in `Assets/Packages/`, tracked by `Assets/packages.config` and `Assets/NuGet.config`.
- **R3.Unity** (git package `com.cysharp.r3`): `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`.
- **UniTask** (git package `com.cysharp.unitask`): `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`. Installed in case it's needed; not used yet (see "UniTask").
- `com.unity.ai.assistant` and `com.unity.ai.inference` must stay: they provide the Unity connection (MCP) Claude uses.

## Code layout and architecture

```
Assets/_Project/
  Scenes/Game.unity        Main Camera, Global Light 2D, GameRoot (first in Build Settings)
  Data/                    ScriptableObject assets; GameDatabase.asset references all of them
  Scripts/                 VoidProspector.asmdef (refs: Unity.InputSystem, UnityEngine.UI, Unity.TextMeshPro, DOTween.Modules, R3.Unity, UniTask, UniTask.DOTween)
    Core/        GameRoot (loads the save, builds the world at runtime), GameSession, GameNumber, NumberFormatter, TimeFormatter,
                 Formulas, WeightedRandom, SaveData, SaveSystem, SaveStore, SaveController
    Data/        ScriptableObject definitions (incl. MilestoneDefinition), GameDatabase (+ Validate; offline and drone settings), MiningSettings
    Economy/     Wallet, CargoHold, UpgradeTrack, Upgrades, EconomySimulator
    Mining/      MeteorState, MeteorRoller (pure) + MeteorField, Meteor, ChunkField, Chunk, TapInput, AutoFire
    Progression/ DepthProgress (one per sector), Milestones
    Drones/      DroneBay, DroneTargeting (pure) + DroneSwarm (view)
    Idle/        OfflineEarnings (pure), OfflineController
    Fx/          GameFx (particles, shake, hit-stop, popups, toasts), GameAudio (generated sounds), ShapeSprites
    UI/          Hud, UpgradePanel, StarMapPanel, WelcomeBackPopup, DebugPanel, UiFactory
    Editor/      VoidProspector.Editor.asmdef — EconomyExport menu
Assets/Tests/EditMode/     VoidProspector.Tests.EditMode.asmdef (149 tests)
tools/economy/             build_economy_xlsx.py
```

Rules that keep this working:
- **Game logic is plain C#** (no `MonoBehaviour`, no `UnityEngine.Object` state) in `GameSession`, `Wallet`, `CargoHold`, `Upgrades`, `MeteorState`, `DepthProgress`, etc. MonoBehaviours only display and forward input. This makes it unit-testable and lets `EconomySimulator` (and later offline progress) run the real rules without visuals.
- **All balance numbers live in ScriptableObjects** reached through `GameDatabase`; feel/juice numbers in `MiningSettings`. Never hard-code tuning in scripts or serialize it in scenes (scene values silently override code defaults; a prototype lesson). `GameDatabase.Validate` runs at startup and in tests.
- **Logic is tick-based**, driven by elapsed time, not `async` waits, so it can be simulated and fast-forwarded.
- Use `GameNumber` for anything that grows (credits, costs, HP, values) and `NumberFormatter` to display it. Formulas from the GDD live in `Formulas`.
- Randomness that affects the economy goes through the session's seeded `System.Random` (`Session.Rng`); `UnityEngine.Random` is only for visuals.
- `GameRoot` is the only scene object; the world and UI are built in code. UI is created through `UiFactory`.
- Namespaces follow folders: `VoidProspector.Core`, `.Data`, `.Economy`, `.Mining`, `.Progression`, `.Drones`, `.Idle`, `.Fx`, `.UI`, `.EditorTools`.

## Sectors, jump drive, drones, milestones (Phase 6)

- The session holds the **current sector** (`Sector`, `SectorRx`) and the **furthest unlocked** one (`UnlockedSector`, `UnlockedSectorRx`). `Depths` is the current sector's `DepthProgress`; `DepthsOf(sector)` gives any sector's. Change sectors only with `Session.TravelTo`; in the game use `GameRoot.TravelTo`, which saves and reloads the scene, so views never have to rebind to a new sector.
- The **jump drive** price is the next sector's `unlockCost` (credits and/or mineral chunk counts from the cargo hold). `JumpState`: NoNextSector → NeedsDepth (last depth of the furthest sector not unlocked) → Saving → Affordable. While it's available, `JumpReserve` lists the minerals that Sell and auto-sell keep (the cheapest chunks). Always sell through `Session.SellCargo` (never `Cargo.TakeAll` in gameplay) so the reserve is respected.
- `CargoHold` tracks value per (mineral, grade) stack: `Remove` and `TakeAllExcept` take or keep the lowest grades first.
- Upgrades tied to a later sector use `UpgradeDefinition.requiresSector`; buy through `Session.TryBuy` (checks it), and use `Session.IsAvailable` to show or hide them.
- **Drones**: count, damage factor and targeting rule come from upgrades; fire timing is the session's `DroneBay` (`Session.TickDrones`), so the simulator and offline earnings include drones. `DroneSwarm` only picks targets (`DroneTargeting.Pick`) and draws.
- **Milestones**: data in `Data/Milestones/` (goal MineMineral / BreakMeteors / ReachDepth, credit reward, permanent value bonus). `Session.TryCollect` and `RegisterBreak` feed the counters; rewards are paid by the session on `Milestones.Completed`. The value bonus goes into chunk values via `MeteorRoller.RollChunk(..., valueMultiplier)`.
- New sectors: add the asset with an `index` one higher than the last, a non-empty `unlockCost`, `backgroundColor`, and list it in `GameDatabase.sectors`; `Validate` checks the chain.

## Saving

- Everything that must survive a restart goes in `SaveData` (plain `[Serializable]` fields, `JsonUtility`), written by `SaveSystem.Capture` and restored by `SaveSystem.Apply`. **When you add persistent state, add it to all three and to the round-trip test** (`SaveTests.RoundTrip_ThroughJson_RestoresEverything`).
- Reference content by **id**, never by array index or asset name. `Apply` skips unknown ids and clamps out-of-range values (with warnings) instead of failing.
- Models restore through explicit methods (`Wallet.Restore`, `CargoHold.Restore`, `DepthProgress.Restore`, `Upgrades.SetLevel`), not by replaying gameplay.
- Changing the meaning of a saved field: bump `SaveSystem.CurrentVersion` and add a step to `Migrate`. Saves from a newer build are rejected (not overwritten until the next save).
- File writes go only through `SaveStore` (temp file → backup → rename). Don't write the save file anywhere else.
- `SaveController` decides when to save; call `RequestSave()` after something important happens (it coalesces to one write per frame).
- Offline earnings: `OfflineEarnings` is pure and tested; it estimates idle income by running `EconomySimulator` (no taps, no purchases) on a copy of the session. Keep new idle income sources (drones, refinery) inside `GameSession` rules so the simulator, and so offline earnings, include them automatically.
- Save format is **version 2** (Phase 6): per-sector depths, unlocked sector, milestones and counters, per-stack cargo value. `Migrate` turns version 1 saves into version 2.
- In the editor the save lives in `Application.persistentDataPath` too, so play sessions carry over. Use the debug panel's **Wipe save** for a fresh start and **Away 1h / 10h** to test offline earnings.

## TextMeshPro

- All UI text is `TextMeshProUGUI`. Never use legacy `UnityEngine.UI.Text` or `Outline` in game code (only the throwaway prototype still does).
- Create labels with `UiFactory.Text(parent, name, text, size, TextAlignmentOptions, color)`: default font (LiberationSans SDF), no wrapping, overflow allowed, rich text on. Buttons come from `UiFactory.Button(..., out TextMeshProUGUI label, ...)`.
- Text over the game field (popups, toasts) gets an outline with `UiFactory.AddOutline(text)`, which uses the shared preset `Resources/Fonts & Materials/LiberationSans SDF - Outline`. Don't set `outlineWidth` per label: it creates a material instance per text.
- Use `FontStyles` / `TextAlignmentOptions`, `textWrappingMode` (not the obsolete `enableWordWrapping`). Rich text `<b>`, `<size=26>` is used in labels.
- Special characters in use: × → —. If a new symbol shows as a box, the default font lacks it: use a fallback font asset or plain text.
- Font sizes are in reference pixels on the 1080×1920 canvas.

## DOTween

- For one-off and UI animation: button squash, punch on hit, credits counting up, coin cascade on Sell, panels and toasts sliding in. Planned for Phase 9 juice; use it from now on instead of hand-written timers for new one-off effects.
- **Keep hand-written per-frame code** for motion that reacts to the game every frame: chunk flight/magnet, meteor drift, screen shake. Don't tween those.
- **Damage popups are pooled and moved by hand** in `GameFx`; keep it that way (many per second; avoid tween allocations there).
- Kill tweens with their target (`transform.DOKill()` / `SetLink(gameObject)`) so they don't run on pooled or destroyed objects; pooled objects must kill their tweens when released.
- Prefer `SetUpdate(true)` (unscaled time) for UI tweens so hit-stop and the debug speed-up don't affect them.
- Core API (`DOTween.To`, `Sequence`, `transform.DOScale/DOMove/DOPunchScale`) is in `DOTween.dll`. The UI/Sprite shortcuts (`Image.DOFade`, `CanvasGroup.DOFade`, `RectTransform.DOAnchorPos`, `SpriteRenderer.DOColor`) are in `Modules/*.cs`, compiled into the `DOTween.Modules` asmdef (made with Utility Panel → Create ASMDEF), which `VoidProspector.asmdef` references. If DOTween is re-set up and that asmdef disappears, recreate it the same way.
- `defaultRecyclable` is off in `DOTweenSettings`: consider turning **Recycle tweens** on to reduce allocations on mobile (then never hold references to tweens after they complete).
- The TextMeshPro module (`DOText`, etc.) is DOTween Pro only; tween TMP via `DOTween.To` on `text.color`/`alpha` or a counter value instead.

## R3 (reactive extensions)

- Installed: the **R3 core** (NuGet) and **R3.Unity** (git package). R3.Unity provides `.AddTo(Component)`, the Unity frame/time providers (time-based operators like `Delay`, `Debounce`, `ThrottleFirst` run on the main thread via `UnityTimeProvider`), uGUI bindings (`button.OnClickAsObservable()`, `SubscribeToText`) and the ObservableTracker window.
- Gameplay time-based operators should use `UnityTimeProvider.Update` (scaled; respects hit-stop and debug speed) or `UnityTimeProvider.UpdateIgnoreTimeScale` for UI. Don't use R3 timers inside plain-C# game logic: logic stays tick-based so the simulator can fast-forward it.
- **Model → UI pattern (in use):** each model keeps a private `ReactiveProperty<T>` and exposes two views:
  - a plain getter for game logic and the simulator (`Wallet.Credits`, `Cargo.Count`, `Depths.Current`, `track.Level`, `Session.AutoSellBelow`);
  - a `ReadOnlyReactiveProperty<T>` with the `Rx` suffix for UI binding (`CreditsRx`, `CountRx`/`ValueRx`/`CapacityRx`, `CurrentRx`/`DeepestRx`/`ProgressRx`, `LevelRx`, `AutoSellBelowRx`). It emits the current value on subscribe, then only real changes.
  - One-off happenings (not state) are a `Subject` exposed as `Observable<T>`, e.g. `Upgrades.Bought`.
  - Only the model sets values; never expose the writable `ReactiveProperty`. No `event Action Changed` in models anymore.
- **UI binds each widget to the values it shows** (see `Hud.Bind`, `UpgradePanel.Bind`): `Observable.Merge(...)` when a widget redraws from several values, `CombineLatest` + `DistinctUntilChangedBy` when it derives something (an upgrade's quote from level + credits + buy mode) so it redraws only when the result changes. Because subscribe emits the current value, binding also draws the initial state; no separate `Refresh()`.
- When a model updates two related properties, set them in an order that leaves subscribers seeing a consistent state (e.g. `CargoHold` sets `Value` before `Count`).
- Button clicks stay as `Button.onClick` listeners via `UiFactory.Button` (the button owns them, nothing to dispose).
- Every UI subscription is disposed with its owner: `.AddTo(this)` (R3.Unity) in MonoBehaviours, or a `CompositeDisposable` / `DisposableBag` in plain classes. Use R3.Unity's ObservableTracker window to find leaks.
- Keep models usable without subscribers: setting a `ReactiveProperty` with no subscribers must stay cheap (the simulator sets values thousands of times).
- Unlike classic Rx, errors don't complete the stream by default; handle exceptions in subscribers.
- Asmdefs: `VoidProspector.asmdef` picks up `R3.dll` automatically (`overrideReferences: false`) and references `R3.Unity`. The test asmdef uses `overrideReferences: true` and lists `R3.dll` and `Microsoft.Bcl.TimeProvider.dll` in its `precompiledReferences`.
- NuGetForUnity: manage via the **NuGet** menu → Manage NuGet Packages. DLLs go to `Assets/Packages/` (committed; `.dll` files go through Git LFS). R3.Unity needs the NuGet R3 core; update both together.

## UniTask

- Installed and referenced from `VoidProspector.asmdef` (`UniTask`, `UniTask.DOTween`), but not used in code yet. Asmdef assemblies don't get auto-referenced packages (only `Assembly-CSharp` does), so new packages must be added to the asmdef references explicitly.
- **Use it for real async work only:** saving/loading (Phase 5), loading scenes or Addressables, network calls (ads, IAP, cloud save, remote config), waiting for one-off UI flows (e.g. `await` a dialog's result).
- **Don't use it for game logic or timing.** Mining, auto-fire, respawns, offline progress stay tick-based in plain C# so `EconomySimulator` can fast-forward them. No `UniTask.Delay` in gameplay rules.
- For one-off animation prefer DOTween; for value-change reactions prefer R3. Use UniTask only when you need to `await` something in sequence.
- Pass a `CancellationToken` to every await in a MonoBehaviour: `destroyCancellationToken` (Unity 2022.2+) or `this.GetCancellationTokenOnDestroy()`, so tasks stop when the object is destroyed or the scene restarts.
- Fire-and-forget with `.Forget()`, never `async void` (except Unity event handlers, which should immediately call a `UniTask` method and `.Forget()` it). Exceptions in forgotten tasks are logged via `UniTaskScheduler.UnobservedTaskException`.
- Delays respect time scale by default (`UniTask.Delay(ms)` uses scaled time, so hit-stop and debug ×10 affect it); pass `DelayType.UnscaledDeltaTime` / `ignoreTimeScale: true` for UI.
- **DOTween integration** (`await tween.ToUniTask()`, `await tween` with cancellation) lives in the `UniTask.DOTween` assembly, which only compiles when the scripting define **`UNITASK_DOTWEEN_SUPPORT`** is set (DOTween isn't a UPM package, so it isn't detected automatically). It is set in Player Settings for Standalone, Android and iOS. **Any new build platform must get the define too**, or `ToUniTask()` on tweens stops compiling there. Prefer it over DOTween's own `AsyncWaitForCompletion()` (allocates).
- R3 interop: `observable.FirstAsync(ct)` etc. return `Task`/`ValueTask`; `await` them directly or convert with `.AsUniTask()`.
- Debug leaks with Window → UniTask Tracker.

## Tests

- EditMode tests in `Assets/Tests/EditMode/`: formulas, formatter, `GameNumber`, weighted random, data validation (loads the real `GameDatabase.asset`), core loop, upgrades, depth, auto-sell, crits, saving, offline earnings, sectors and the jump drive, drones, milestones, and **pacing tests** that run the simulator on the real data: `EconomyTests.FirstFiveMinutes_BuySomethingEvery5To15Seconds`, `Phase6PacingTests.ReachingSector2_TakesAbout10Minutes` (7–14 min) and `Phase6PacingTests.IdleWithDrones_ProgressesSteadily`.
- Run them in Unity: Window → General → Test Runner → EditMode → Run All. (Through the Unity MCP connection `TestRunnerApi` is blocked; a reflection-based runner over `[Test]`/`[TestCase]` was used instead.)
- New pure-C# logic gets EditMode tests. If you change balance data, the pacing tests must still pass.

## Economy tooling

1. Unity menu **Void Prospector → Export Economy Data** (writes `docs/economy/data/*.csv`: data snapshot + three 30-minute simulations).
2. `python tools/economy/build_economy_xlsx.py docs/economy/data docs/economy/Economy.xlsx` (needs `openpyxl`).
3. Open in Excel (formulas recalculate on open). LibreOffice isn't installed on this machine.

Current pacing (3 seeds): median time between purchases 6–7 s in minutes 0–5 (GDD target 5–15 s), ~18 s in 5–15, ~38 s in 15–30.

## Workflow

- **Git:** repo `github.com/AleksandarTanev/void-prospector`. One branch per phase or task (e.g. `phase-4-upgrades`, `tmp-text`), merged into `main` by fast-forward. Commit, push and merge **only when the user asks**. End commit messages with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Git LFS for binaries (images, audio, fonts, `.dll`, etc.) via `.gitattributes`; Unity YAML uses UnityYAMLMerge (`merge=unityyamlmerge`, configure the driver in global git config, commands at the bottom of `.gitattributes`).
- **Unity MCP connection:**
  - Use it only when needed (creating assets/scenes, Play-mode checks the user asked for). **For script-only changes, just edit the files** and tell the user what to check in Unity; the user imports packages and compiles themselves.
  - Before any MCP command, guard on the project: `if (!Application.dataPath.Contains("void-prospector")) return;` (the connection has been attached to another project before).
  - MCP command class must be `internal class CommandScript : IRunCommand`; `using System.Reflection` is blocked; NUnit can't be referenced.
  - If Unity reports "compiling" for a long time, it's usually unfocused: ask the user to click into the Unity window.
- Scene changes through MCP: set references with `SerializedObject` + `ApplyModifiedProperties`, then verify the saved `.unity` file actually contains them.
- Keep `docs/IMPLEMENTATION_PLAN.md` up to date when a phase or task finishes (tick items, add notes and open questions).
