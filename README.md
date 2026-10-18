# Construction Empire

A construction-company management sim for Unity. **You can now press Play
and actually play it** - bid on jobs, hire and staff crews, buy equipment,
watch a real construction schedule play out, and get paid - through a
simple on-screen UI, no scene setup beyond the one component below.

---

## Setup (one-time, ~2 minutes)

1. **Copy the project in.** Copy this zip's `Assets` folder into your Unity
   project, merging with `Assets` if you already have one from an earlier
   drop of this project.

2. **Create a GameConfig asset.** In the Project window:
   `Assets > Create > ConstructionEmpire > Game Config`. Save it anywhere
   under `Assets/_Project/ScriptableObjects/` (the default name is fine).
   You can leave every field at its default - starting cash, starting
   date, simulation speed, etc. - or tweak them now; they're all plain
   Inspector fields.

3. **Create the Bootstrap scene.**
   - `File > New Scene` (a basic empty scene is fine - you don't need any
     default camera/light for this to work, though it's harmless if
     Unity adds them).
   - Save it as `Assets/_Project/Scenes/Bootstrap.unity`.

4. **Add one GameObject.**
   - In the Hierarchy: right-click → `Create Empty`. Rename it
     `GameBootstrap`.
   - With it selected, `Add Component` → search `Game Bootstrap` → add it.
     (This will also auto-add a `Game Time Manager` component - that's
     expected, leave it alone.)
   - Drag your `GameConfig` asset from the Project window into the
     **Game Config** slot on the `Game Bootstrap` component in the
     Inspector.

That's the entire one-time setup. You don't need to add anything else,
create any other GameObjects, or touch the UI system - the game's on-screen
interface attaches itself automatically the moment the game starts.

## Playing it

**Press Play.** You should immediately see a simple on-screen interface
appear in the Game window: a header bar (cash, reputation, date, weather,
time-scale buttons) and a row of tabs (Opportunities / Active Projects /
Workforce / Equipment / Log).

A normal first session looks like this:

1. **Opportunities tab** - you'll see a handful of project opportunities
   already on the board. Each shows the client, an estimated value, a
   deadline, and a recommended bid price. Click **"Bid at recommended
   price"** on one you like (or **"Bid aggressively (-15%)"** for a
   higher-risk, higher-margin attempt at a lower price). You'll see in the
   **Log** tab whether you won or lost.

2. **If you win**, switch to the **Workforce tab**: click a worker type
   (e.g. "GeneralLaborer") a couple of times to hire some workers, then
   click **"Create Crew From All Idle Workers"** to group them into a crew.

3. **Active Projects tab** - your won project is now listed with its
   phases. The first phase or two should show `[Ready]`. Click
   **"Assign Idle Crew"** on one to start it. If a phase needs equipment,
   go buy some in the **Equipment tab** first, then come back and click
   **"Assign Equipment"**. If it needs materials, click **"Order
   Materials"** (this charges cash immediately and schedules delivery -
   the game doesn't make you wait for delivery before crediting the phase
   for having materials "in the pipeline").

   Some phases (electrical, plumbing, roofing, some framing work) can
   instead be handed to a subcontractor - if so, you'll see subcontractor
   options as their own buttons right under that phase, priced and rated
   differently (cheaper options are less reliable).

4. **Speed up time** with the `2x` / `5x` / `10x` buttons in the header so
   you're not watching real-time seconds tick by for in-game days. Progress,
   payments, phase completions, inspections, and safety incidents all
   happen automatically from here - just check back and assign crews to
   newly-`[Ready]` phases as dependencies unlock.

5. **Log tab** shows a running history of what's happened - bids, phase
   completions, inspection results, safety incidents, and so on.

6. **Save / Load** buttons are in the header bar (single save slot called
   "default" for now).

That's the full loop: bid → staff → progress → get paid → repeat, with
quality, inspections, weather, and client relationships all genuinely
affecting outcomes in the background.

### A note on what this UI is

It's built with Unity's older immediate-mode GUI system (`OnGUI`/
`GUILayout`) rather than the modern Canvas/UI Toolkit systems, specifically
*because* that's the one UI approach that's pure code with no scene/prefab
authoring required - which is what let me build something you can actually
press Play and use, without needing the Unity Editor open on my end. It is
deliberately plain (boxy default-skin buttons and labels, no styling) - a
functional stand-in for the real Milestone 4 management UI, not a finished
one. Every button in it calls straight into the same underlying systems a
polished UI would use, so replacing it later won't touch any simulation
code - see `GameHUD.cs` if you want to reskin or restructure it, or
`DEVELOPMENT_PROGRESS.md` for where a proper Canvas-based UI would plug in.

## Requirements
Unity 2022.3 LTS or newer (2021 LTS will likely also work). Built-in render
pipeline is fine. The **Test Framework** package (usually included by
default) if you want to run the EditMode tests.

## Running the tests
`Window > General > Test Runner > EditMode tab > Run All`. 78 tests
covering the game clock, company finances, estimation math, bidding,
materials, crews, equipment, project phase dependencies and completion,
subcontracting, weather, quality scoring, inspections, and client
relationships. (The HUD itself isn't covered by these - `OnGUI` only runs
in an actual rendering loop, which EditMode tests don't provide. It's
reviewed carefully by hand instead - see `DEVELOPMENT_PROGRESS.md`.)

## What's implemented
Core simulation loop (clock, save/load, company finances), bidding and
cost estimation, construction phases with real dependencies, workers and
crews (with fatigue/morale), equipment (purchase/rent/condition/
breakdown), materials (ordering with lead times), subcontractors, weather,
a critical-path scheduler (logic only, not yet shown in the UI), quality
scoring, inspections, client relationships, and now a playable on-screen
UI tying all of it together.

## What's not implemented
A polished Canvas-based UI (this `OnGUI` version is the stand-in), the
site/world map, offices, change orders, the broader random-events grab-bag,
banking/loans/insurance/tax, research/upgrades, company-level progression,
specializations, real competitor companies, market conditions/seasonality,
a debug menu, a tutorial. See `DEVELOPMENT_PROGRESS.md` for the full,
honest running list.

## A note on how this was produced
Same process as every previous drop: written and reviewed as text, not
compiled or run in the Editor - that environment isn't available here. For
the UI specifically, I did extra verification beyond the usual brace/
paren balance check: every `GUILayout.Begin*`/`End*` pair is counted and
confirmed to match (a common source of runtime `ArgumentException`s in
Unity IMGUI code), and every single method call and event field the HUD
touches was checked against its actual declaration elsewhere in the
codebase. That gives me real confidence in the logic, but pressing Play
yourself is still the first true test of this - please tell me about any
layout weirdness, missing feedback, or rough edges you hit.
