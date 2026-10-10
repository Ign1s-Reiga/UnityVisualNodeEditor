---
name: debug-flow-with-mcp
description: Inspect and drive a running Visual Node Editor flow in Play mode through the MCP tools — where the game is (node and containers), which events it accepts, parameter values — and work out why a button or Raise call does not move it. Use when asked "where is the game now", "why doesn't this button work", or to step through a flow while playing.
---

# Debug a running flow with the Visual Node Editor MCP tools

`get_runtime_state` and `send_event` show and drive the graphs that are running (the same as the Now running panel in the editor).
They need Unity in Play mode with a Graph Runner for the graph. The MCP tools cannot start Play mode:
ask the user to press Play (the Play button in the Visual Node Editor toolbar also sets up the scene and the runner).

If the tools are not available, ask the user to turn on **Edit > Preferences > Visual Node Editor > Enable MCP server**
and to reload MCP servers in Claude Code (`/mcp`).

## Where is the flow?

`get_runtime_state` (optionally with `graph`) returns, per running graph:

- `current`: the node the flow waits on, and `location`: the containers around it, outermost first (`Stage › Play`).
- `actions`: what can happen next — `Raise` with the event name, or `Advance` to the next non-event node.
- `parameters`: the live values (the asset's defaults are not changed).

If `runners` is empty, the graph is not running: no Graph Runner for it in the open scene, or not in Play mode.
`validate_graph` reports a missing Graph Runner as a warning.

## Moving it

`send_event` with `event: "Name"` raises it; with `advance: true` it advances. It returns the new state.
If nothing happens, the reply lists what the current node accepts.

## Why does a button not move the flow?

1. `get_runtime_state`: is the flow where you think? A button only works for events leaving the **current** node.
2. Compare names exactly: the button's event name versus the Event node's `eventName` (`get_graph`).
   `validate_graph` warns about names that differ only in case or spaces. Graph Event Buttons store the name in the scene file
   (search the scene YAML for `_eventName:`).
3. Is the Event node connected straight after the current waiting node? `Raise` only looks at Event nodes directly connected to it.
4. Does the event lead somewhere? An Event with no outgoing edge only notifies; the flow stays where it is.
5. Inside a container: is the inner Entry connected, and does the Exit node's exit connect onward outside? The Console shows a warning
   when the flow stops before a container or exit.

Report what you found with the evidence (the state before and after, the names compared) and the fix,
and make graph fixes with the build tools only when the user agrees.
