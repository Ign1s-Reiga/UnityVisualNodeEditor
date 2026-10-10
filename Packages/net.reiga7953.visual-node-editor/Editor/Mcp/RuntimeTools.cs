using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Debugging;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// 実行中の流れを確かめ・進めるツール（<c>get_runtime_state</c> / <c>send_event</c>）。Now running パネルと同じ情報と操作。
    /// 実行中の Runner（<see cref="GraphRunner.Running"/>）が対象で、ふつうは Play 中だけ。
    /// </summary>
    public static class RuntimeTools
    {
        private const string GraphArgument = GraphReadTools.GraphArgument;
        private const string NotRunningMessage =
            "No graph is running. Enter Play mode in a scene that has a Graph Runner for the graph " +
            "(the Play button in the Visual Node Editor toolbar sets this up).";

        /// <summary>このクラスのツール（<c>tools/list</c> に出す順）。</summary>
        public static IEnumerable<McpTool> Create()
        {
            yield return new McpTool(
                "get_runtime_state",
                "While a graph runs (Play mode): where each running graph is (current node and the containers it is in, e.g. 'Stage › Play'), " +
                "what can happen next (events it can raise, or Advance), and the current parameter values.",
                McpSchema.Object(new Dictionary<string, object>
                {
                    [GraphArgument] = McpSchema.String("Only this graph (asset path). Optional; by default every running graph."),
                }),
                args =>
                {
                    var runners = FindRunners(args.GetOptionalString(GraphArgument));
                    return new Dictionary<string, object>
                    {
                        ["playing"] = EditorApplication.isPlaying,
                        ["runners"] = runners.Select(DescribeRunner).ToList(),
                        ["message"] = runners.Count == 0 ? NotRunningMessage : null,
                    };
                });

            yield return new McpTool(
                "send_event",
                "While a graph runs: raise an event (GraphRunner.Raise) or advance along the non-event edge (GraphRunner.Advance), " +
                "the same as the buttons in the Now running panel. Returns the new state. Use get_runtime_state to see what is possible.",
                McpSchema.Object(new Dictionary<string, object>
                {
                    [GraphArgument] = McpSchema.String("The running graph (asset path). Optional when only one graph is running."),
                    ["event"] = McpSchema.String("Event name to raise. Leave out and set 'advance' to advance instead."),
                    ["advance"] = McpSchema.Boolean("Advance instead of raising an event."),
                }),
                args =>
                {
                    var runners = FindRunners(args.GetOptionalString(GraphArgument));
                    if (runners.Count == 0)
                    {
                        throw new McpToolException(NotRunningMessage);
                    }

                    if (runners.Count > 1)
                    {
                        throw new McpToolException("Several graphs are running. Say which one with 'graph': " +
                                                   string.Join(", ", runners.Select(r => AssetDatabase.GetAssetPath(r.Graph))));
                    }

                    var runner = runners[0];
                    var eventName = args.GetOptionalString("event");
                    var advance = args.GetBool("advance");
                    if (string.IsNullOrEmpty(eventName) == !advance)
                    {
                        throw new McpToolException("Give either 'event' (an event name to raise) or 'advance': true.");
                    }

                    var action = advance ? RuntimeAction.Advance(string.Empty) : RuntimeAction.Raise(eventName);
                    if (!action.Run(runner))
                    {
                        var possible = RuntimeActions.For(runner.Query, runner.Current).Select(a => a.Label).ToList();
                        throw new McpToolException(
                            $"Nothing happened: from '{NodeDisplay.GetNodeLabel(runner.Current)}' the runner cannot " +
                            (advance ? "advance" : $"raise '{eventName}'") +
                            (possible.Count > 0 ? $". It can: {string.Join(", ", possible)}." : ". It has no way forward from here."));
                    }

                    return DescribeRunner(runner);
                });
        }

        private static List<GraphRunner> FindRunners(string graphPath)
        {
            var graph = string.IsNullOrEmpty(graphPath) ? null : GraphAssets.Load(graphPath);
            return GraphRunner.Running.Where(r => r.IsRunning && (graph == null || r.Graph == graph)).ToList();
        }

        private static Dictionary<string, object> DescribeRunner(GraphRunner runner)
        {
            var current = runner.Current;
            return new Dictionary<string, object>
            {
                ["graph"] = AssetDatabase.GetAssetPath(runner.Graph),
                ["current"] = current == null
                    ? null
                    : new Dictionary<string, object>
                    {
                        ["id"] = current.Id,
                        ["label"] = NodeDisplay.GetNodeLabel(current),
                        ["type"] = NodeDisplay.GetTypeDisplayName(current.GetType()),
                    },
                ["location"] = RuntimePanel.GetLocation(runner),
                ["actions"] = RuntimeActions.For(runner.Query, current).Select(a => new Dictionary<string, object>
                {
                    ["kind"] = a.Kind,
                    ["label"] = a.Label,
                    ["event"] = a.Kind == RuntimeActionKind.Raise ? a.EventName : null,
                }).ToList(),
                ["parameters"] = runner.Graph.Parameters.Where(p => p != null).Select(p =>
                {
                    var value = RuntimeParameterText.GetValue(runner, p, out var hasValue);
                    return new Dictionary<string, object> { ["name"] = p.Name, ["type"] = p.Type, ["value"] = hasValue ? value : null };
                }).ToList(),
            };
        }
    }
}
