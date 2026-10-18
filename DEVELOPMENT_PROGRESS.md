# Current verified implementation — Milestone 11

Milestones 0.5–11 are installed in Assets/_Project. Full suite: 224 passed; compiler: zero errors/warnings. See Milestone-11-Verification.md and Milestone-Audit.md. The historical implementation notes below are retained for context and do not override this status.

# Current verified implementation — 25 September 2026

Milestones 0.5–10 are installed in Assets/_Project. Milestone 10 passed 216 tests with zero compiler warnings/errors. See Milestone-10-Verification.md. Historical notes below concern the earlier parallel architecture and are superseded for this implementation.

# 2026-09-24 — Milestone 9 implemented

- Milestone 8 finished and packaged. Milestone 9 risk systems installed in `_Project`.
- 207/207 Edit Mode tests passed, including 13 new risk regression tests. Zero compiler errors/warnings.
- Weather/site slots retain the original progress formula. Risk events affect work, finance, staffing and inspections.
- Event-driven forecasts and resolving proactive warnings are integrated into the dashboard; forced-event debug menus are available.
- Risk settings are editable on ManagementConfiguration.Risks. Full session save restoration remains a later milestone.
- See Milestone-9-Verification.md and Milestone-Audit.md in the delivered package.

# Verified scheduling milestone — 23 September 2026

Milestone 8 is implemented in the active Assets/_Project architecture: dependency-based critical paths and float, preserved planned dates, actual dates and delays, crew/equipment reservation windows, double-booking notifications, tick-driven forecasts, schedule UI and the selected-project critical-path debug menu. Milestone 7 remains complete.

Validation: 194/194 Edit Mode tests passed. Scheduling compilation: 0 errors and 0 compiler warnings. The editor logger verified the house-renovation critical graph and planned completion at hour 91 (Day 4, 19:00). The schedule UI preview handles empty authoring schedules by deriving the plan from the template.

Resource reservations pause unfinished work when their window expires. They flag conflicts rather than automatically leveling resources. Full-session save restoration remains outside this milestone.

---
# Verified _Project milestones — 22 September 2026

The active SampleScene uses the UI Toolkit dashboard and Assets/_Project architecture. Milestones 0.5–7 requested in this task are implemented within the scope and limitations recorded in the milestone audit. Milestone 7 adds twelve subcontractor profiles, specialty phase definitions, competing seeded quotes, award/capacity management, finance obligations, common construction-work integration, dashboard controls and the starter generator.

Verification: 185/185 Edit Mode tests passed. Final clean Unity compilation: 0 errors, 0 warnings. Generator rerun: 0 created, 12 skipped, all existing company asset hashes unchanged. Play Mode quote UI and prerequisite-gated award buttons verified. Unity's account-service package may independently emit connectivity warnings.

Full project/ledger/order/award persistence remains a later milestone; current save/load is the company/clock foundation. The pre-existing Assets/Scripts architecture is preserved and is not the active SampleScene driver.

---

## Earlier development notes (preserved)
# Development Progress — Construction Empire

## Current Milestone
Milestones 1-3 and 7 complete, plus slices of 5, 6, 8, 9, 20, 21, 22, 30, 31,
and now a playable stand-in UI. Untested in-editor - same standing caveat as
every previous drop, though this round it matters more than ever (see
Testing).

## Completed this round — Playable UI

### `GameHUD` (Scripts/UI/GameHUD.cs)
The headline ask this round: press Play and actually be able to play the
game. Built with Unity's `OnGUI`/`GUILayout` (immediate-mode GUI) rather
than Canvas/UI Toolkit, specifically because IMGUI is pure code - no scene,
prefab, or RectTransform authoring needed, which is the one path that let
this be built and verified without the Unity Editor. `GameBootstrap.Awake`
now auto-attaches it (`gameObject.AddComponent<GameHUD>()`), so there's no
additional manual setup step beyond what Milestone 1 already required.

Five tabs, all reading/writing straight through `GameContext`:
- **Opportunities** - browse generated opportunities (with a live
  estimate), bid at the recommended price or 15% under it, or generate more
  opportunities once the board runs dry.
- **Active Projects** - each project's phases with status/progress, and
  per-phase buttons for assigning an idle crew, assigning available
  equipment, ordering materials, or (where applicable) awarding a
  subcontractor directly from a list of live quotes.
- **Workforce** - hire by worker type, group all idle workers into a new
  crew with one click, see fatigue/morale per worker.
- **Equipment** - purchase by type, see the fleet's condition/status.
- **Log** - a running feed of bid results, phase completions, inspection
  outcomes, subcontract awards, safety incidents, and equipment breakdowns,
  built by subscribing to the same events every other system already
  publishes.

Header bar: cash, reputation, date, weather, pause/1x/2x/5x/10x, and
Save/Load buttons (single fixed slot, "default").

### Design choices worth noting
- **"Assign idle crew/equipment" rather than a picker.** IMGUI doesn't have
  a built-in dropdown/multi-select as simple as a button, and building one
  well is real UI work I'd rather not rush blind. Every assignment button
  just grabs the first idle/available match. This is a real, if blunt,
  functional choice - it works, but a player with many crews or equipment
  units has no way to pick a *specific* one from the HUD. Flagging this as
  the clearest limitation of the current UI, and the most obvious thing a
  real Canvas UI would improve first.
- **Materials are "available" the instant they're ordered**, not when
  delivered (this was already true of the underlying simulation from
  Milestone 3 - `MaterialSystem.GetAvailable` counts on-order stock - the
  UI just surfaces it plainly rather than hiding a pre-existing behavior).
- **Ordering materials/equipment only applies to Ready/InProgress in-house
  phases** - the buttons don't appear at all for Locked, Completed, or
  subcontracted phases, matching the guards already built into
  `ConstructionProjectSystem` in earlier rounds.

## Known Issues / Deliberate Simplifications (new this round)
- **This is genuinely the first thing in the whole project I couldn't
  unit-test.** `OnGUI` only executes inside Unity's actual rendering loop
  (Layout/Repaint/event passes), which EditMode tests don't provide -
  there's no equivalent of the brace-balance-and-signature-checking
  confidence I've had for every pure-logic system so far. To compensate, I
  added a check nothing else in this project needed: counting every
  `GUILayout.Begin*`/`End*` pair (BeginHorizontal/EndHorizontal,
  BeginVertical/EndVertical, BeginArea/EndArea, BeginScrollView/
  EndScrollView) to confirm they match, since a mismatch there is a classic
  Unity IMGUI runtime exception. I also individually verified every method
  call and every event field the HUD touches against its actual
  declaration. That's real diligence, but it is categorically less certain
  than "the Test Runner passed" - pressing Play yourself is the actual
  first test of this file.
- **No entity picker.** See "Design choices" above - assignment is always
  "first available match," never a specific choice.
- **Single save slot.** The Save/Load buttons always use the slot name
  "default" - fine for one playthrough, not built for multiple saves.
- **The critical-path scheduler isn't shown anywhere in the UI** - it's
  been available as pure logic since an earlier round (`CriticalPathCalculator`)
  but nothing surfaces it visually yet (no Gantt view).
- **No confirmation/undo on any button** - every click fires immediately.
  Fine for a functional stand-in, not how a polished UI should feel for,
  e.g., purchasing a $300,000 crane by accident.
- Same standing gaps as before: scheduled events (subcontracts, deliveries,
  rentals) aren't persisted across save/load, weather state isn't saved,
  `GameContext.Initialize` remains a long positional call.

## Architecture (this round)
- `GameHUD` depends only on `GameContext` (the service locator) and the
  event structs it subscribes to - it has no constructor dependencies of
  its own and needs none, since `GameContext` is guaranteed populated by
  the time any `MonoBehaviour.Start()` runs (all `Awake()` calls, including
  `GameBootstrap`'s, complete before any `Start()` does).
- Deliberately no new gameplay logic lives in `GameHUD` itself - every
  button is a thin call into an existing system method. The one exception
  is the "first idle/available match" selection logic (`AssignIdleCrew`,
  `AssignAvailableEquipment`), which is UI-side convenience, not
  simulation logic, and was placed in the HUD file rather than polluting
  `ConstructionProjectSystem`'s public API with a "just pick one for me"
  method that a real UI wouldn't want anyway.

## Testing
- Full repo-wide brace/paren/bracket balance check (62 files, all
  balanced).
- New for this round: `GUILayout.Begin*`/`End*` pairing check specific to
  `GameHUD.cs` (BeginHorizontal/EndHorizontal: 8/8, BeginVertical/
  EndVertical: 2/2, BeginArea/EndArea: 3/3, BeginScrollView/EndScrollView:
  5/5 - all matched).
- Every method call and event-field access in `GameHUD.cs` individually
  checked against its declaration elsewhere in the codebase (`GameContext`
  members, `BiddingSystem.SubmitBid`, `ConstructionProjectSystem`'s
  assignment/subcontract/query methods, `SaveSystem.SaveGame`/`LoadGame`,
  every subscribed event struct's fields, every data class field the HUD
  reads or displays).
- 78 EditMode tests, unchanged in count from last round - this round added
  no new pure-logic systems, only UI wiring, which (as noted above) isn't
  something EditMode tests can exercise.
- None of the simulation code has been run inside Unity. The HUD has
  *never* been run inside Unity, by me or anyone. Both are the real next
  step, and the HUD is the higher-priority one to check first.

## Next Steps (in spec order)
- **Try it and report back** - this is the first round where "does it
  actually work" is a meaningfully open question rather than a formality.
- **Entity pickers** for crew/equipment assignment, if the "first
  available" behavior feels too blunt in practice.
- **Surface the critical path** somewhere in the Active Projects tab (even
  a plain-text "N days of slack" label per phase would use the existing
  `CriticalPathCalculator` with no new logic needed).
- **Milestone 19 - Change orders**: natural next addition now that clients
  are a real system with a relationship to protect or damage.
- **Milestone 10+**: expansion, market/competitor depth, research, a real
  Canvas-based UI, visuals, polish - still genuinely later-stage per the
  spec's own order.


