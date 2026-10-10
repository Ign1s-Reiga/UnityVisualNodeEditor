using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>グラフを読む MCP のツール（list_graphs / get_graph / validate_graph）。</summary>
    public sealed class McpGraphReadToolsTests
    {
        private McpTempGraphs _temp;
        private McpProtocol _protocol;
        private ContainerTestGraph _graph;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _temp = new McpTempGraphs();
            _graph = new ContainerTestGraph();
            _path = _temp.Save(_graph.Asset, "Flow");
            _protocol = new McpProtocol("test", "0", McpServerHost.CreateTools());
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void ListGraphs_FindsTheGraphByPath()
        {
            var result = McpTestClient.CallToolForObject(_protocol, "list_graphs");
            var graphs = ((List<object>)result["graphs"]).Cast<Dictionary<string, object>>().ToList();

            var flow = graphs.Single(g => (string)g["path"] == _path);
            Assert.That(flow["nodes"], Is.EqualTo((long)_graph.Asset.Nodes.Count));
        }

        [Test]
        public void GetGraph_DescribesNodesPortsAndEdges()
        {
            var result = McpTestClient.CallToolForObject(_protocol, "get_graph", new Dictionary<string, object> { ["graph"] = _path });
            var nodes = ((List<object>)result["nodes"]).Cast<Dictionary<string, object>>().ToDictionary(n => (string)n["id"]);
            var edges = ((List<object>)result["edges"]).Cast<Dictionary<string, object>>().ToList();

            var stage = nodes[_graph.Stage.Id];
            Assert.That(stage["label"], Is.EqualTo("Stage"));
            Assert.That(stage["parent"], Is.Null, "at the root");
            Assert.That(((List<object>)stage["exits"]).Cast<Dictionary<string, object>>().Select(e => e["name"]), Is.EqualTo(new[] { "Clear", "GameOver" }));
            var outputs = ((List<object>)((Dictionary<string, object>)stage["ports"])["outputs"]).Cast<Dictionary<string, object>>();
            Assert.That(outputs.Select(p => p["label"]), Is.EqualTo(new[] { "Clear", "GameOver" }), "a container's outputs are its exits");

            Assert.That(nodes[_graph.Play.Id]["parent"], Is.EqualTo(_graph.Stage.Id));
            Assert.That(((Dictionary<string, object>)nodes[_graph.ClearExit.Id]["exit"])["name"], Is.EqualTo("Clear"));
            Assert.That(edges, Has.Some.Matches<Dictionary<string, object>>(e =>
                (string)e["from"] == _graph.Entry.Id && (string)e["to"] == _graph.Stage.Id && (string)e["toPort"] == "in"));
        }

        [Test]
        public void ValidateGraph_ListsIssuesWithTheirKind()
        {
            _graph.Asset.AddNode(new SceneNode { Title = "Empty", Position = new Vector2(600f, 0f) });

            var result = McpTestClient.CallToolForObject(_protocol, "validate_graph", new Dictionary<string, object> { ["graph"] = _path });
            var issues = ((List<object>)result["issues"]).Cast<Dictionary<string, object>>().ToList();

            Assert.That(issues, Has.Some.Matches<Dictionary<string, object>>(i => (string)i["kind"] == "SceneNotSet"));
            Assert.That(result["warnings"], Is.GreaterThanOrEqualTo(1L));
        }

        [Test]
        public void UnknownGraph_SaysHowToFindOne()
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "get_graph", new Dictionary<string, object> { ["graph"] = "Assets/Nope.asset" });

            Assert.That(isError, Is.True);
            Assert.That(text, Does.Contain("list_graphs"));
        }
    }
}
