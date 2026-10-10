using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// グラフを書き換えるツール（<c>create_graph</c> / <c>add_node</c> / <c>update_node</c> / <c>remove_node</c> /
    /// <c>connect</c> / <c>disconnect</c> / <c>group_into_container</c>）。処理は <see cref="GraphEdits"/>。
    /// </summary>
    public static class GraphEditTools
    {
        private const string GraphArgument = GraphReadTools.GraphArgument;

        // add_node / update_node で値を入れられる欄
        private static Dictionary<string, object> NodeFields() => new()
        {
            ["title"] = McpSchema.String("Title shown on the node. Leave empty to show the scene name (Scene), event name (Event) or type name."),
            ["eventName"] = McpSchema.String("Event nodes: the name GraphRunner.Raise(...) and Graph Event Button send. Reuse names exactly (case matters)."),
            ["scene"] = McpSchema.String("Scene nodes: asset path of the scene, e.g. 'Assets/Scenes/Title.unity'. Empty string clears it."),
            ["description"] = McpSchema.String("State nodes: a description of the state."),
            ["text"] = McpSchema.String("Note nodes: the note text."),
            ["exit"] = McpSchema.String("Exit nodes (inside a container): the name of the container exit this Exit node leaves through."),
            ["x"] = McpSchema.Number("Position on the canvas (pixels, grows to the right). Optional; by default the node goes right of the rightmost node."),
            ["y"] = McpSchema.Number("Position on the canvas (pixels, grows downwards)."),
        };

        /// <summary>このクラスのツール（<c>tools/list</c> に出す順）。</summary>
        public static IEnumerable<McpTool> Create()
        {
            yield return new McpTool(
                "create_graph",
                "Create a new Node Graph asset (it starts with an Entry node, like Assets > Create > Visual Node Editor > Node Graph). Missing folders are created.",
                McpSchema.Object(new Dictionary<string, object>
                {
                    ["path"] = McpSchema.String("Asset path for the new graph, inside Assets and ending in .asset, e.g. 'Assets/Flows/Main.asset'."),
                }, "path"),
                args =>
                {
                    // 前後の空白や \ を直したパスで作るので、返すのは実際に作ったアセットのパス
                    var graph = GraphEdits.CreateGraph(args.GetString("path"));
                    return new Dictionary<string, object>
                    {
                        ["path"] = AssetDatabase.GetAssetPath(graph),
                        ["entry"] = graph.Nodes.OfType<EntryNode>().First().Id,
                    };
                });

            var addProperties = new Dictionary<string, object>
            {
                [GraphArgument] = McpSchema.String(GraphReadTools.GraphArgumentDescription),
                ["type"] = McpSchema.String("Node type: Scene, State, Event, Container, Exit, Note or Entry (a display name, menu path such as 'Flow/Scene', or class name)."),
                ["parent"] = McpSchema.String("Id of the container to put the node in. Leave out for the root of the graph. Exit nodes must be inside a container."),
                ["exits"] = McpSchema.StringArray("Container nodes: names of the exits (output ports) to create instead of the default 'Next', e.g. ['Clear', 'GameOver']."),
            };
            foreach (var field in NodeFields())
            {
                addProperties[field.Key] = field.Value;
            }

            yield return new McpTool(
                "add_node",
                "Add a node. Typical flow: Entry -> Scene/State (waits) -> Event (transition named by eventName) -> next Scene/State. " +
                "A Container gets an Entry and one Exit node per exit inside it automatically; connect inside it with 'parent' set to the container. " +
                "Returns the new node (with its id and ports). Use connect afterwards.",
                McpSchema.Object(addProperties, GraphArgument, "type"),
                args =>
                {
                    var asset = GraphAssets.Load(args.GetString(GraphArgument));
                    var type = NodeTypes.Resolve(args.GetString("type"));
                    var exits = args.Has("exits") ? args.GetStringList("exits") : null;
                    if (exits != null && !typeof(ContainerNode).IsAssignableFrom(type))
                    {
                        throw new McpToolException("'exits' is only for Container nodes.");
                    }

                    var node = GraphEdits.AddNode(asset, type, args.GetOptionalString("parent"), null, created =>
                    {
                        var apply = PrepareFields(asset, created, args);
                        if (exits != null)
                        {
                            SetExits((ContainerNode)created, exits);
                        }

                        apply();
                    });
                    return GraphDescription.DescribeNode(asset, node);
                });

            var updateProperties = new Dictionary<string, object>
            {
                [GraphArgument] = McpSchema.String(GraphReadTools.GraphArgumentDescription),
                ["node"] = McpSchema.String("Id of the node to change."),
            };
            foreach (var field in NodeFields())
            {
                updateProperties[field.Key] = field.Value;
            }

            yield return new McpTool(
                "update_node",
                "Change a node's title, event name, scene, description, note text, exit or position. Only the given values change.",
                McpSchema.Object(updateProperties, GraphArgument, "node"),
                args =>
                {
                    var asset = GraphAssets.Load(args.GetString(GraphArgument));
                    var node = GraphEdits.UpdateNode(asset, args.GetString("node"), target => PrepareFields(asset, target, args));
                    return GraphDescription.DescribeNode(asset, node);
                });

            yield return new McpTool(
                "remove_node",
                "Remove a node and its edges. Removing a container removes everything inside it. A container's own Entry cannot be removed on its own.",
                McpSchema.Object(new Dictionary<string, object>
                {
                    [GraphArgument] = McpSchema.String(GraphReadTools.GraphArgumentDescription),
                    ["node"] = McpSchema.String("Id of the node to remove."),
                }, GraphArgument, "node"),
                args =>
                {
                    GraphEdits.RemoveNode(GraphAssets.Load(args.GetString(GraphArgument)), args.GetString("node"));
                    return "Removed.";
                });

            var edgeProperties = new Dictionary<string, object>
            {
                [GraphArgument] = McpSchema.String(GraphReadTools.GraphArgumentDescription),
                ["from"] = McpSchema.String("Id of the node the edge starts from (its output port)."),
                ["to"] = McpSchema.String("Id of the node the edge goes to (its input port)."),
                ["fromPort"] = McpSchema.String("Output port id or label. Optional when the node has one output ('out'); for a container, the exit name."),
                ["toPort"] = McpSchema.String("Input port id or label. Optional when the node has one input ('in')."),
            };

            yield return new McpTool(
                "connect",
                "Connect an output port to an input port. Both nodes must be in the same container (or both at the root). " +
                "Put an Event node between two waiting nodes (Scene/State) so GraphRunner.Raise(eventName) can take the transition; " +
                "a waiting node connected straight to another node is taken by Advance().",
                McpSchema.Object(edgeProperties, GraphArgument, "from", "to"),
                args =>
                {
                    var edge = GraphEdits.Connect(GraphAssets.Load(args.GetString(GraphArgument)),
                        args.GetString("from"), args.GetOptionalString("fromPort"), args.GetString("to"), args.GetOptionalString("toPort"));
                    return new Dictionary<string, object>
                    {
                        ["from"] = edge.FromNodeId,
                        ["fromPort"] = edge.FromPort,
                        ["to"] = edge.ToNodeId,
                        ["toPort"] = edge.ToPort,
                    };
                });

            yield return new McpTool(
                "disconnect",
                "Remove the edges from one node to another (only between the given ports, if given).",
                McpSchema.Object(edgeProperties, GraphArgument, "from", "to"),
                args =>
                {
                    var removed = GraphEdits.Disconnect(GraphAssets.Load(args.GetString(GraphArgument)),
                        args.GetString("from"), args.GetOptionalString("fromPort"), args.GetString("to"), args.GetOptionalString("toPort"));
                    return new Dictionary<string, object> { ["removed"] = removed };
                });

            yield return new McpTool(
                "group_into_container",
                "Move nodes into a new container, rewiring the edges that cross its boundary so the flow runs the same way " +
                "(like right-click > Group into Container). Event nodes go with the nodes that lead into them. " +
                "Refused, with the reason, if an event comes from both inside and outside or the nodes are entered at more than one place.",
                McpSchema.Object(new Dictionary<string, object>
                {
                    [GraphArgument] = McpSchema.String(GraphReadTools.GraphArgumentDescription),
                    ["nodes"] = McpSchema.StringArray("Ids of the nodes to group (all in the same container or all at the root)."),
                    ["title"] = McpSchema.String("Title for the new container, e.g. 'Stage'. Optional."),
                }, GraphArgument, "nodes"),
                args =>
                {
                    var asset = GraphAssets.Load(args.GetString(GraphArgument));
                    var container = GraphEdits.Group(asset, args.GetStringList("nodes"), args.GetOptionalString("title"));
                    return GraphDescription.DescribeNode(asset, container);
                });
        }

        // 欄の値をすべて確かめ（型に合わない欄・無いシーン・無い出口などはここで例外）、変更を行う処理を返す。
        // 確かめる途中では何も変えないので、失敗してもノードは元のまま
        private static Action PrepareFields(NodeGraphAsset asset, NodeData node, McpArguments args)
        {
            var changes = new List<Action>();
            if (args.Has("title"))
            {
                var title = args.GetOptionalString("title");
                changes.Add(() => node.Title = title);
            }

            if (args.Has("eventName"))
            {
                var eventNode = As<EventNode>(node, "eventName");
                var eventName = args.GetOptionalString("eventName");
                changes.Add(() => eventNode.EventName = eventName);
            }

            if (args.Has("scene"))
            {
                var sceneNode = As<SceneNode>(node, "scene");
                var scenePath = args.GetOptionalString("scene");
                var scene = string.IsNullOrEmpty(scenePath) ? new SceneReference() : GraphEdits.FindScene(scenePath);
                changes.Add(() => sceneNode.Scene = scene);
            }

            if (args.Has("description"))
            {
                var state = As<StateNode>(node, "description");
                var description = args.GetOptionalString("description");
                changes.Add(() => state.Description = description);
            }

            if (args.Has("text"))
            {
                var note = As<NoteNode>(node, "text");
                var text = args.GetOptionalString("text");
                changes.Add(() => note.Text = text);
            }

            if (args.Has("exit"))
            {
                var exitNode = As<ContainerExitNode>(node, "exit");
                var exit = GraphEdits.FindExit(asset.FindNode(exitNode.ParentId) as ContainerNode, args.GetOptionalString("exit"));
                changes.Add(() => exitNode.ExitId = exit.Id);
            }

            var x = args.GetOptionalFloat("x");
            var y = args.GetOptionalFloat("y");
            if (x != null || y != null)
            {
                changes.Add(() => node.Position = new Vector2(x ?? node.Position.x, y ?? node.Position.y));
            }

            return () => changes.ForEach(change => change());
        }

        // 新しいコンテナの出口を、既定の Next の代わりに指定の名前にする（中身を作る前に呼ぶ）
        private static void SetExits(ContainerNode container, IReadOnlyList<string> names)
        {
            var trimmed = names.Select(n => (n ?? string.Empty).Trim()).ToList();
            if (trimmed.Any(n => n.Length == 0) || trimmed.Distinct().Count() != trimmed.Count)
            {
                throw new McpToolException("Exit names must be non-empty and different from each other.");
            }

            foreach (var exit in container.Exits.ToList())
            {
                container.RemoveExit(exit.Id);
            }

            foreach (var name in trimmed)
            {
                container.AddExit(name);
            }
        }

        private static T As<T>(NodeData node, string field) where T : NodeData =>
            node as T ?? throw new McpToolException(
                $"'{field}' is not used by {NodeDisplay.GetTypeDisplayName(node.GetType())} nodes (it is for {NodeDisplay.GetTypeDisplayName(typeof(T))} nodes).");
    }
}
