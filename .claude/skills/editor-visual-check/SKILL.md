---
name: editor-visual-check
description: Check the Visual Node Editor by looking at and operating the running Unity editor with Computer Use — the Event node tag, the node tree, the bottom breadcrumbs, the issue list fix buttons, the scene field's Pick…/Create Scene…, and dropping a detached edge. Use when asked to look at, verify or screenshot the editor UI, or after changing USS/UXML or GraphView behaviour that EditMode tests cannot see.
---

# Visual check of the Visual Node Editor (Computer Use)

EditMode tests cover the logic but not what the editor looks like or how mouse operations feel.
This skill drives the real Unity editor with Computer Use, using the Visual Node Editor MCP tools
for setup and for checking the result, so clicks are only spent on what has to be seen.

## Before you start

1. Unity is open with this project and the editor is idle (no progress bar, no compile spinner).
   Take a screenshot to confirm. If a modal dialog is open, read it and stop to ask the user.
2. Check that the MCP tools are available (`list_graphs` of the `visual-node-editor` server).
   If not, ask the user to turn on **Edit > Preferences > Visual Node Editor > Enable MCP server**
   and to reload MCP servers in Claude Code (`/mcp`). Without MCP, set up the graph by hand in the editor.
3. Ground rules while operating Unity:
   - Take a screenshot before acting and after every step; base every judgement on a screenshot.
   - Only interact with the Unity window. Do not save scenes, change Project Settings, or press Play unless a step says so.
   - If a dialog appears, read it. Choose Cancel unless the step needs the other answer.
   - Work in a scratch graph only: `Assets/__VisualCheck/Check.asset`.

## Set up the scratch graph (MCP)

1. `create_graph` with path `Assets/__VisualCheck/Check.asset` (if it exists, `get_graph` it and reuse it).
2. Build: Entry → State "Title" → Event "StartGame" → State "Game"; a Container "Stage" with exits `Clear`, `GameOver`
   and a State "Play" inside it; a Scene node with no scene (it produces the "no scene assigned" issue).
   Give positions in columns (x = 0, 250, 500, …) so the screenshots are readable.
3. `open_graph` to show it in the Visual Node Editor window, then take a screenshot. Press `A` on the canvas to frame everything.

## Checklist

Check each item, keep the screenshot that shows the result, and note anything that looks wrong even if it is not on the list.

| # | What to check | Expected |
|---|---|---|
| 1 | Event node | Titled `StartGame` (no second line), drawn as a short tag with rounded corners, clearly different from the State boxes |
| 2 | Node tree (left pane) | Lists Entry, Title, StartGame, Game, Stage (bold) with its contents nested; coloured dots per category |
| 3 | Tree click | Clicking `Game` selects it in the graph and centres it |
| 4 | Tree double-click | Double-clicking `Stage` opens the container; the tree marks Stage; the breadcrumbs `Root > Stage` appear **below** the canvas |
| 5 | Breadcrumbs | Clicking `Root` goes back; at the root the breadcrumb bar is hidden |
| 6 | View menu | View > Node Tree hides and shows the left pane |
| 7 | Issue list | Toolbar issue toggle opens the list; the "no scene assigned" row has a **Pick Scene…** button; clicking it opens a menu of scenes ending with **Create Scene…** (press Esc; do not create) |
| 8 | Scene field | Selecting the empty Scene node shows **Pick…** and **Create Scene…** under the Scene field, and the note about coming back to a loaded scene |
| 9 | Detached edge | Drag the edge `Title → StartGame` by its **output end** (near Title's port) onto empty canvas. The node search opens offering nodes **with an output**. Pick State: the new node must connect **into StartGame's input** (confirm with `get_graph`). Then press Ctrl+Z |
| 10 | Edge drop from a port | Drag from Game's output port onto empty canvas: the search opens with Event listed first |

## Report

Give a table: item, result (pass / fail / unclear), screenshot reference, note.
Only mark "pass" when a screenshot shows it. For a failure, describe what you saw versus what was expected.

## Clean up

Tell the user that the scratch graph is at `Assets/__VisualCheck/Check.asset` and ask before deleting it.
