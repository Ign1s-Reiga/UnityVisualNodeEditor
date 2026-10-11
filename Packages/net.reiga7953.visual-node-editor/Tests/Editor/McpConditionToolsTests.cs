using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP のツールでの Event のトリガーと条件（<c>add_node</c> / <c>update_node</c> / <c>get_graph</c>）。</summary>
    public sealed class McpConditionToolsTests
    {
        private McpTempGraphs _temp;
        private McpProtocol _protocol;
        private string _path;
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _temp = new McpTempGraphs();
            _protocol = new McpProtocol("test", "0", McpServerHost.CreateTools());
            _path = McpTempGraphs.Folder + "/Conditions.asset";
            McpTestClient.CallToolForObject(_protocol, "create_graph", Args(("path", _path)));
            _graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(_path);
            _graph.AddParameter(new GraphParameter("Affection", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Met", GraphParameterType.Bool));
            _graph.AddParameter(new GraphParameter("Route", GraphParameterType.String));
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void AddNode_SetsTriggerAndConditions()
        {
            var id = (string)McpTestClient.CallToolForObject(_protocol, "add_node", Args(
                ("graph", _path), ("type", "Event"), ("trigger", "automatic"),
                ("conditions", new List<object>
                {
                    Condition("Affection", ">=", 5L),
                    Condition("Met", "is true"),
                    Condition("Route", "==", "Alice"),
                })))["id"];

            var node = (EventNode)_graph.FindNode(id);
            Assert.That(node.Trigger, Is.EqualTo(EventTriggerMode.Automatic));
            Assert.That(node.Conditions.Select(c => c.Describe(_graph.FindParameter(c.ParameterId))),
                Is.EqualTo(new[] { "Affection >= 5", "Met", "Route == \"Alice\"" }));
        }

        [Test]
        public void UpdateNode_ReplacesConditions_AndGetGraphShowsThem()
        {
            var id = (string)McpTestClient.CallToolForObject(_protocol, "add_node", Args(("graph", _path), ("type", "Event"), ("eventName", "Done")))["id"];

            McpTestClient.CallToolForObject(_protocol, "update_node", Args(
                ("graph", _path), ("node", id), ("conditions", new List<object> { Condition("Affection", "<", 3L) })));
            var graph = McpTestClient.CallToolForObject(_protocol, "get_graph", Args(("graph", _path)));
            var described = ((List<object>)graph["nodes"]).Cast<Dictionary<string, object>>().Single(n => (string)n["id"] == id);

            Assert.That(described["trigger"], Is.EqualTo("Raise"));
            var condition = (Dictionary<string, object>)((List<object>)described["conditions"]).Single();
            Assert.That(condition["parameter"], Is.EqualTo("Affection"));
            Assert.That(condition["op"], Is.EqualTo("<"));
            Assert.That(condition["text"], Is.EqualTo("Affection < 3"));

            McpTestClient.CallToolForObject(_protocol, "update_node", Args(("graph", _path), ("node", id), ("conditions", new List<object>())));
            Assert.That(((EventNode)_graph.FindNode(id)).Conditions, Is.Empty, "[] clears them");
        }

        [TestCase("Love", ">=", 1L, "no parameter named 'Love'")]
        [TestCase("Met", ">", 1L, "cannot compare the Bool parameter")]
        [TestCase("Affection", ">=", 1.5, "whole number")]
        [TestCase("Route", "==", 3L, "must be a string")]
        [TestCase("Affection", ">=", 3000000000L, "must be a whole number")]
        public void BadConditions_AreRefusedWithAReason(string parameter, string op, object value, string reason)
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "add_node", Args(
                ("graph", _path), ("type", "Event"), ("conditions", new List<object> { Condition(parameter, op, value) })));

            Assert.That(isError, Is.True, text);
            Assert.That(text, Does.Contain(reason));
            Assert.That(_graph.Nodes.OfType<EventNode>(), Is.Empty, "nothing is added");
        }

        [Test]
        public void UnknownTrigger_IsRefused()
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "add_node", Args(("graph", _path), ("type", "Event"), ("trigger", "Sometimes")));

            Assert.That(isError, Is.True);
            Assert.That(text, Does.Contain("'Raise' or 'Automatic'"));
        }

        private static Dictionary<string, object> Condition(string parameter, string op, object value = null)
        {
            var condition = new Dictionary<string, object> { ["parameter"] = parameter, ["op"] = op };
            if (value != null)
            {
                condition["value"] = value;
            }

            return condition;
        }

        private static Dictionary<string, object> Args(params (string Key, object Value)[] fields) =>
            fields.ToDictionary(f => f.Key, f => f.Value);
    }
}
