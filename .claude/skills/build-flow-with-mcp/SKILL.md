---
name: build-flow-with-mcp
description: Build or change a game flow graph (scene transitions, states, events, containers) in the Visual Node Editor through its MCP tools, from a description such as "title, then game, then result, back to title". Use when asked to create, extend, restructure or fix a Visual Node Editor graph without clicking through the editor.
---

# Build a game flow with the Visual Node Editor MCP tools

The `visual-node-editor` MCP server runs inside the Unity editor. Its tools read and change Node Graph assets
with the same rules as the editor; every change is recorded for Undo (Ctrl+Z in Unity) and saved.

If the tools are not available, ask the user to turn on **Edit > Preferences > Visual Node Editor > Enable MCP server**
and to reload MCP servers in Claude Code (`/mcp`). The project's `.mcp.json` already points at `http://127.0.0.1:8790/mcp`.

## How a graph works

- **Entry** (one, at the root) is where the flow starts.
- **Scene** nodes load their scene; **State** nodes are states inside a scene. Both *wait* until something moves the flow.
- An **Event** node right after a waiting node is a transition. `GraphRunner.Raise("Name")` (or a Graph Event Button on a UI button)
  takes it when the name matches exactly. A waiting node connected straight to another node moves there on `Advance()`.
- A **Container** groups nodes. It has named exits (its output ports). Inside, the flow starts at its Entry and leaves through an
  Exit node that points at one of the exits; outside, the flow continues from that exit's port.
- Edges only connect nodes in the same container (or both at the root).

## Workflow

1. `list_graphs` to find the graph, or `create_graph` (path inside `Assets/…/*.asset`). `get_graph` to see what is there.
2. Write the plan as a short list before editing: waiting nodes, the event names between them, containers.
3. For Scene nodes, find the scene assets first (Glob `Assets/**/*.unity`) and pass their paths as `scene`.
4. Add nodes left to right with positions so the graph reads like the flow: x = 0, 250, 500, …; branches on new rows (y += 150).
5. Connect: `connect` with `from` / `to` ids. Ports default to `out` → `in`; for a container's output give the exit name as `fromPort`.
6. Containers: `add_node` with `type: Container` and `exits`, then add the inner nodes with `parent` set to the container id,
   connect the container's Entry (find it with `get_graph`) to the first inner node and the last ones to the Exit nodes.
   Or build the nodes first and use `group_into_container` — it keeps the flow running the same way.
7. `validate_graph` and fix every error. Warnings about Build Settings or a missing Graph Runner are fixed by the editor's Play button.
8. `open_graph` so the user sees the result, then summarise: the nodes, and the **event names** the UI buttons must send.

## Rules of thumb

- Reuse event names exactly; `validate_graph` warns about names that differ only in case or spaces.
- Put an Event between two waiting nodes whenever a button or code should decide when to move on.
- Do not create a second root Entry, and do not connect across containers — the tools refuse it and say why.
- When something is refused, read the message: it says what to change.

## Example: Title → Game → Result → Title

1. `create_graph` `Assets/Flows/Main.asset` → returns the Entry id.
2. `add_node` Scene `Title` (scene `Assets/Scenes/Title.unity`, x 250), Event `StartGame` (x 500), Scene `Game` (x 750),
   Event `Finish` (x 1000), Scene `Result` (x 1250), Event `BackToTitle` (x 1500).
3. `connect` Entry → Title → StartGame → Game → Finish → Result → BackToTitle → Title.
4. `validate_graph`, then `open_graph`. Tell the user: buttons send `StartGame`, `Finish`, `BackToTitle`.
