---
name: ux-scenario-run
description: Run the UX Scenarios A and B from docs/04-ux-audit.md in the real Unity editor with Computer Use, count the steps and time them, and record the result in the audit's Before/After. Use when asked to measure, time or re-run the UX scenarios, or to check that a new user could build the first game flow without docs.
---

# Run and time the UX scenarios (Computer Use)

`docs/04-ux-audit.md` defines two scenarios and an "After" walkthrough (Scenario A: 43 steps, Scenario B: 40 steps),
counted on paper. This skill performs them in the editor and reports what really happened.

Scenario A: a new graph where Title → Game → Result → Title advance with UI buttons, then Play.
Scenario B: from A's graph, put Game in a container with a Pause state, add Retry from Result, and check where the running flow is.

## Before you start

1. Read `docs/04-ux-audit.md` (the "シナリオ A（後）" and "シナリオ B（後）" tables). Follow those steps; do not invent shortcuts.
2. Unity is open with this project and idle. Take a screenshot to confirm.
3. Fixtures (not timed): three scenes `Assets/UxScenario/Title.unity`, `Game.unity`, `Result.unity`, each with a UI Button
   (Game also needs Pause and Resume buttons, Result a Retry button for Scenario B).
   If they are missing, ask the user whether to create them. If yes, create them through the editor menus
   (File > New Scene, GameObject > UI > Button, save into `Assets/UxScenario/`).
4. The scenario graph must not exist yet (`Assets/UxScenario/Flow.asset`). Check with the MCP `list_graphs` if available.
   Do **not** use MCP tools to perform scenario steps — the point is to measure the editor UI. MCP is only for checking results.

## Running a scenario

- Note the start time with `date +%s` (Bash) right before the first step and the end time after the last.
- Perform every step through the UI with Computer Use. Take a screenshot after each step.
- Count steps the same way as the audit: one click, drag, menu choice or field entry = one step.
  Count retries and mistakes separately (they are the most useful findings).
- Write down every moment of hesitation: something not where expected, an unclear label, a dialog that surprised you.
- After Scenario A, check the result: `get_graph` shows the flow, and pressing Play then the buttons moves through Title → Game → Result → Title
  (use `get_runtime_state` to confirm where the flow is). Stop Play afterwards.
- Scenario B starts from A's graph; check with the Now running panel (in the editor) that the location shows the container and Paused.

## Recording

Update the "まとめ" table and add notes under it in `docs/04-ux-audit.md`:

- 手数（後）: the steps actually taken (and retries in brackets).
- 時間（後）: the measured time, labelled as measured with Computer Use. An agent's speed is not a person's,
  so keep the paper estimate next to it and say which is which.
- 残った摩擦: what slowed you down, with the step number.

Leave the roadmap item "シナリオ A・B を実機で通して時間を計る（…ユーザーが行う）" unticked unless the user timed it themselves;
mention the agent's run in its note instead.

## Clean up

The scenario graph and fixtures stay in `Assets/UxScenario/` for the next run. Tell the user what was created.
Do not commit them unless the user asks.
