using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Issues;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>グラフを読むツール（<c>list_graphs</c> / <c>get_graph</c> / <c>validate_graph</c> / <c>open_graph</c>）。</summary>
    public static class GraphReadTools
    {
        internal const string GraphArgument = "graph";
        internal const string GraphArgumentDescription = "Asset path of the Node Graph, e.g. 'Assets/Flows/Main.asset' (see list_graphs).";

        /// <summary>このクラスのツール（<c>tools/list</c> に出す順）。</summary>
        public static IEnumerable<McpTool> Create()
        {
            yield return new McpTool(
                "list_graphs",
                "List the Visual Node Editor graphs (Node Graph assets) in the Unity project, with their asset paths and node counts. " +
                "Start here to find the path other tools take as 'graph'.",
                McpSchema.Empty(),
                _ => new Dictionary<string, object>
                {
                    ["graphs"] = GraphAssets.FindPaths().Select(path =>
                    {
                        var graph = GraphAssets.Load(path);
                        return new Dictionary<string, object>
                        {
                            ["path"] = path,
                            ["name"] = graph.name,
                            ["nodes"] = graph.Nodes.Count(n => n != null),
                        };
                    }).ToList(),
                });

            yield return new McpTool(
                "get_graph",
                "Read a graph: every node (id, type, label, parent container, position, type-specific values such as eventName or scene, " +
                "and its ports), the edges between ports, and the parameters. Nodes inside a container have 'parent' set to the container's id. " +
                "Flow: Entry starts it; Scene and State nodes wait; an Event node after a waiting node is a transition that GraphRunner.Raise(eventName) takes.",
                McpSchema.Object(new Dictionary<string, object> { [GraphArgument] = McpSchema.String(GraphArgumentDescription) }, GraphArgument),
                args =>
                {
                    var path = args.GetString(GraphArgument);
                    return GraphDescription.Describe(GraphAssets.Load(path), path);
                });

            yield return new McpTool(
                "validate_graph",
                "Check a graph for problems (the same list as the editor's issue list): missing Entry, Scene nodes without a scene, " +
                "scenes not in Build Settings, event names that differ only in case, container rules and more. Returns errors first.",
                McpSchema.Object(new Dictionary<string, object> { [GraphArgument] = McpSchema.String(GraphArgumentDescription) }, GraphArgument),
                args =>
                {
                    var issues = GraphIssues.Collect(GraphAssets.Load(args.GetString(GraphArgument)))
                        .OrderBy(i => i.Severity == GraphIssueSeverity.Error ? 0 : 1)
                        .ToList();
                    return new Dictionary<string, object>
                    {
                        ["errors"] = issues.Count(i => i.Severity == GraphIssueSeverity.Error),
                        ["warnings"] = issues.Count(i => i.Severity == GraphIssueSeverity.Warning),
                        ["issues"] = issues.Select(i => new Dictionary<string, object>
                        {
                            ["severity"] = i.Severity,
                            ["kind"] = i.Kind,
                            ["message"] = i.Message,
                            ["node"] = i.NodeId,
                        }).ToList(),
                    };
                });

            yield return new McpTool(
                "open_graph",
                "Open a graph in the Visual Node Editor window inside Unity (useful before looking at the editor with Computer Use).",
                McpSchema.Object(new Dictionary<string, object> { [GraphArgument] = McpSchema.String(GraphArgumentDescription) }, GraphArgument),
                args =>
                {
                    var path = args.GetString(GraphArgument);
                    NodeGraphEditorWindow.Open(GraphAssets.Load(path));
                    return $"Opened '{path}' in the Visual Node Editor window.";
                });
        }
    }
}
