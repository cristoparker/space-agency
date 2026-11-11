# Prompt for Sonnet 5.5: Space Agency design implementation (step by step)

Read this file fully before doing anything. Work ONE STEP at a time. Stop at the end of each step, report, and wait for the user before starting the next step.

## 1. Context

- Project: a Unity 6 (URP 6000.6.5f1, macOS) space agency management game.
- Project root: `/Users/tasrifibnmizan/Documents/GitHub/space-agency`
- Scripts: `Assets/Scripts/`. Key areas:
  - `Rocket/` (RocketBuilderUI, RocketLogic, SimpleRocket, DeploymentManager, LaunchSequence, FlightOutcome, FlightUI)
  - `Missions/` (MissionManager, MissionDefinition, Objectives, GlobalContracts)
  - `Base/` (BaseSim, BuildingSelector, BuildingTags, PlotPicker, FacilityPreview)
  - `Time/` (GameClock, DayNightCycle, CalendarUI)
  - `UI/` (HudController, UIKit, UIHover, ObjectiveMenuUI, ContractsPanelUI, FlightUI, SettingsUI, DeadlineBarUI, LaunchDayUI)
  - `Agency/Agency.cs` (money, reputation, stats)
- The design is final and is in `docs/DESIGN_PLAN.md`. Read it. Treat it as the source of truth. If something in the code conflicts with it, stop and ask.
- The UI was built in code (UIKit helpers). Use the existing helpers (`UIKit.Panel`, `UIKit.Btn`, `UIKit.TextAt`, `UIKit.Tint`, `UIHover.Attach`). Do not add new glass styles.

## 2. Hard rules (apply to every step)

1. **Do not commit or push** unless the user explicitly asks. Do not create branches.
2. **Do not break saved data.** Save keys are `SpaceAgency.*` (PlayerPrefs). Do not rename or delete existing keys. Add new keys with a version check. Old saves must still load.
3. **Do not wipe the user's base save** (`SpaceAgency.Base`).
4. **Time:** every new popup, timer, or consequence must follow the game clock (`GameClock`). When the game is paused, it must stop.
5. **Money and time costs are real.** Failing a test loses both money and time. Time does not come back.
6. **Trained scientists and pilots are not permanent.** Only buildings and facilities are permanent.
7. **Never compile in Play mode.** Stop Play mode before editing scripts that run in the editor.
8. **Unity checks.** After each script change:
   - Compile with `unity command script eval --code 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate); return "r";'`, then wait until `UnityEditor.EditorApplication.isCompiling` is false.
   - Read errors with `unity command console get`. Fix every `error CS` before continuing.
9. **Test in Play** with `unity command editor.playmode start`, then capture the screen with `unity command capture game_view`. Stop Play with `UnityEditor.EditorApplication.isPlaying=false` when done.
10. **Keep the code short.** Reuse existing UIKit helpers. Do not duplicate panels.
11. **Language:** the user speaks Bengali and English. Write the final report in Bengali. Keep code comments in English.

## 3. Design summary (short)

- Every big decision shows **benefit**, **cost**, and **risk/outcome** in a "consequence card" (`ImpactCard`).
- **Training and testing:**
  - Failing a test costs money AND time. Time does not come back.
  - Results are probabilistic. The card shows the chance.
  - Trained scientists and pilots are not permanent. Buildings and facilities are permanent.
- **Random events:**
  - Mission events: about 3–4 per mission, at most 3 of them mission events. Examples: test failure, extra budget, small improvement, small advice, a setback.
  - Client event: exactly one per mission, only while a contract is active. Accepting adds the requirement to the mission objectives.
  - A declined event never returns. Consequence: the client gives no extra budget, and future rating can drop.
  - Event UI is a popup card with 2–3 choices, each showing benefit/cost/risk.
- **Flight:**
  - Flight path is simulated from the real rocket design. If the rocket cannot lift off, the flight fails.
  - Orbital view is a simulated 2D graph of Earth with the rocket path. It is not a real view.
  - After a failure, the UI shows which parts were wrong.
  - Launch cutscene can be skipped. The **flight scene always loads next**, on an office monitor, and can also be skipped. Skipping the flight scene returns to the main game. The mission keeps running in the UI.
  - No "Watch the flight" button.
- **Power and money:**
  - Only electricity and money. No food, fuel, or raw materials.
  - Power generation (power grid) costs upkeep.
  - When power is short, construction, building upgrades, and testing stop. A warning says the power load is low and the player should upgrade.
  - Each game hour, one small popup shows the hour's electricity and money use. The top bar shows the average.
  - Upgrades raise power use. The popup shows "+" for the upgrade effect.
  - Hovering over the money in the top bar opens a panel with budget and daily cost.

## 4. Steps (do exactly one at a time)

### Step 1: Impact card component
**Goal:** one reusable UI card that shows Benefit, Cost, and Risk/Outcome, with optional probability.

- Create `Assets/Scripts/UI/ImpactCard.cs`.
- API suggestion: `ImpactCard.Show(title, benefits[], costs[], risks[], float? chance, Action onConfirm, Action onCancel)`.
- Use `UIKit.Panel` for the card and `UIKit.Btn` for buttons.
- Show a clear "Confirm" and "Cancel" button.
- Clicking outside cancels (same as `UIConfirm`).
- Pause the game clock while the card is open, and restore the speed on close (same as `LaunchResultUI.Modal`).
- Add a static `ResetStatics()` and call it from `UI/StaticReset.cs`.

**Test:** open the card from a temporary debug call (remove after the test). Check it is readable at 640×360 reference. Check the time pauses.

**Stop here.** Report the result and wait.

### Step 2: Training and testing consequences
**Goal:** trainings and tests show the impact card, use probability, and charge money AND time on failure. Time is never refunded.

- Locate the training/testing start code in `Base/BaseSim.cs` (`Program`, `Start`, `Job`, `Progress`, `DaysLeft`) and the UI in `Base/BuildingSelector.cs`.
- Before starting a test or training, open `ImpactCard` with benefit, cost (money and days), and the chance of success.
- Roll the result when the job completes:
  - Success: benefit applied.
  - Failure: money and time are lost; a partial benefit may still apply (decide and state it in the card).
- Scientists and pilots are **not** permanent. Do not store them as permanent state. Only facilities stay.
- Keep saves backward compatible.

**Test:** start a test, confirm the card shows, confirm both money and days change on failure, confirm time does not come back.

**Stop here.** Report and wait.

### Step 3: Random mission events
**Goal:** per mission, 3–4 random events (up to 3 mission events plus exactly 1 client event during an active contract).

- Create `Missions/RandomEvents.cs` (data + selection) and `UI/EventPopupUI.cs` (display).
- Mission events: test failure, extra budget, small improvement, small advice, setback (e.g., a pylon). Each has 2–3 choices with benefit/cost/risk.
- Client event: exactly one per contract, active only while the contract runs. Accepting adds a requirement to the contract objectives (`Objectives.cs`).
- Declined events never return (store the declined IDs per mission, in a versioned key).
- Declining a client event: no extra budget from the client, and future rating can drop (add a rating penalty field).
- Use the game clock for timing.

**Test:** start a contract, confirm events appear, accept one client event, confirm the objective appears, decline another, confirm it does not return.

**Stop here.** Report and wait.

### Step 4: Power and money
**Goal:** hourly power/money popup, power shortage shutdown, warning, money hover panel.

- Power: power generation (power grid buildings) costs upkeep. Consumption comes from facilities. If demand exceeds supply, these stop: construction, upgrades, testing. Show a warning: "power load is low, upgrade".
- Hourly popup: once per game hour, a small icon popup with the hour's electricity and money use. Keep it short (e.g., "-120 kWh, -$4.2M"). Show the average in the top bar.
- Upgrade: raises power use; the popup shows "+" with the change.
- Money hover panel: hovering the money text in the top bar opens a panel with budget and daily cost.
- Use `DayNightCycle` / `GameClock` time events. Do not use real time.

**Test:** check hourly popup timing (speed up the clock), force a power shortage, check that construction stops, check the warning, check the money hover panel.

**Stop here.** Report and wait.

### Step 5: Flight simulation and flight scene
**Goal:** flight path from the real rocket design; a simulated Earth graph; a new office monitor scene; skip and return.

- Flight path comes from `RocketStats.Evaluate` (`Rocket/RocketLogic.cs`) and `FlightOutcome.Evaluate` (`Rocket/FlightOutcome.cs`). If the rocket cannot lift off (`liftoffTwr < 1`), it fails.
- Failure cause: show in UI which parts were wrong (use existing `FlightRecord.lines` / `cause`).
- Orbital view: a simple 2D simulated graph of Earth and the rocket path. Not a real 3D view.
- Flight scene: a new scene "3. Flight" loaded after the launch cutscene ends or is skipped. Office room, camera points at a wall monitor. The monitor shows the graph.
- Skipping or ESC in the flight scene loads "2. Game" and the mission keeps running in UI (`FlightManager`).
- Do not add a "Watch the flight" button.
- Add the scene to Build Settings. Do not break "1. Main Menu" or "2. Game".

**Test:** launch a rocket, confirm launch cutscene, confirm flight scene loads, skip it, confirm return to the game and the mission panel continues.

**Stop here.** Report and wait.

## 5. Finish

After all steps, do NOT commit. Report to the user in Bengali:
- What works, step by step.
- What was tested and what was not.
- Any conflict with `docs/DESIGN_PLAN.md`.
- Anything the user must decide.

Then wait for the user's next message.
