# Void Prospector — Gameplay Prototype Plan

A quick, **throwaway** prototype that answers one question: **is cracking meteors and rolling purity fun?**
Speed matters more than clean code. When it has answered its questions, it is deleted; only the lessons carry over into the real game.

## Keeping it removable

| Rule | Why |
| --- | --- |
| Everything lives in `Assets/Prototype/` (scripts, scene) | Deleting one folder removes it all |
| Own assembly definition (`VoidProspector.Prototype.asmdef`) and namespace `VoidProspector.Prototype` | Real game code can't accidentally reference it |
| Own scene `Prototype.unity`, which builds everything at runtime | The real game's scenes stay clean |
| No prefabs, sprite/audio/font files: shapes and tones are generated in code | Nothing else to clean up |
| No new packages, no project-setting changes for the prototype | `Packages/` and `ProjectSettings/` stay as they are |
| Tag in git before deleting (`prototype-v1`) | It can always be recovered |

## Scripts

| Script | Job |
| --- | --- |
| `ProtoBootstrap` | Builds the scene at runtime; holds the tuning config |
| `ProtoConfig` | All tuning values, editable in the Inspector during Play mode |
| `ProtoGame` | Credits, cargo, upgrade levels; sell, buy, refine |
| `MeteorSpawner` / `Meteor` | Drifting meteors, HP, cracking stages, chunk spills |
| `Chunk` | Rolls mineral + purity grade; flies to the ship or fades |
| `TapInput` | Touch/click damages the meteor under the finger |
| `ShipWeapons` | Auto-fire laser and orbiting drones |
| `ProtoUI` | Credits, cargo bar, Sell, upgrades, Refine, debug buttons |
| `ProtoFx` / `ProtoAudio` / `ProtoSprites` | Juice, generated sounds, generated sprites |

## Steps

- [x] 1. Scaffold: folder, asmdef, scene, bootstrap (≈1 h)
- [x] 2. Drifting meteors with pooling (≈1–2 h)
- [x] 3. Tap to damage, damage pop-ups (≈1 h)
- [x] 4. Cracking stages and chunks with mineral + purity rolls (≈2 h)
- [x] 5. Collect, cargo, sell; rare-drop toast (≈1–2 h)
- [x] 6. Upgrades: Drill, Auto-fire, Collector, Drone (≈1–2 h)
- [x] 7. Auto-fire laser and drones (≈1–2 h)
- [x] 8. Juice and sound: shake, particles, hit-stop, tones (≈2 h)
- [x] 9. Refine experiment (≈1 h)
- [ ] 10. Android build and playtest with 2–3 people (≈1–2 h)
- [ ] 11. Write findings below and update the GDD (≈1 h)

Roughly 2–4 days of focused work.

## Starting numbers

| Thing | Value |
| --- | --- |
| Meteor HP / chunks | Small 10 / 3, Medium 30 / 8, Large 80 / 15 |
| Tap damage | 1 (+1 per Drill level) |
| Minerals (value, odds) | Rock 1 (60%), Copper 5 (25%), Tin 6 (10%), Iron 25 (5%) |
| Purity (multiplier, odds) | Crude 1× 60%, Standard 2× 28%, Refined 5× 9%, Pristine 15× 2.7%, Flawless 50× 0.3% |
| Large meteor core | Last chunks roll +1 grade |
| Cargo | 50 chunks |
| Collection | Pickup radius 4.5 (+0.6 per Collector level); chunks outside it drift to the ship at 1.5/s and fade after 12 s |
| Upgrade base costs | Drill 10, Auto-fire 50, Collector 25, Drone 200; ×1.15 per level |
| Refine | Each chunk of the chosen mineral: 50% up one grade, 10% lost |

## Questions it must answer

- Is tapping fun for 5 minutes with no upgrades?
- Do people notice and get excited by a Pristine or Flawless drop?
- Do auto-fire and drones make it better, or make tapping pointless?
- Is refining interesting, or busywork?
- Does portrait feel right? Is the meteor field big enough?
- How long until someone stops playing without being asked?

## Findings

_Fill in after playtesting._

### What was fun

### What wasn't

### Changes for the GDD

## Removing it

1. `git tag prototype-v1 && git push origin prototype-v1`
2. Delete `Assets/Prototype/` in Unity (so the `.meta` files go too)
3. Remove `Prototype.unity` from the build scene list
4. Commit "Remove gameplay prototype"
