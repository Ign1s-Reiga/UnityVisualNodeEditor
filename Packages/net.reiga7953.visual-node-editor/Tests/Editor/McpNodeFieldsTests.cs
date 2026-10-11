using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP のツールでの、組み込み以外のノード型の値（<c>fields</c>）。</summary>
    public sealed class McpNodeFieldsTests
    {
        private McpTempGraphs _temp;
        private McpProtocol _protocol;
        private string _path;
        private NodeGraphAsset _graph;
        private ProbeNode _probe;

        [SetUp]
        public void SetUp()
        {
            _temp = new McpTempGraphs();
            _protocol = new McpProtocol("test", "0", McpServerHost.CreateTools());
            _path = McpTempGraphs.Folder + "/Fields.asset";
            McpTestClient.CallToolForObject(_protocol, "create_graph", Args(("path", _path)));
            _graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(_path);
            _probe = new ProbeNode();
            _graph.AddNode(_probe);
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void UpdateNode_SetsFields_AndGetGraphShowsThem()
        {
            McpTestClient.CallToolForObject(_protocol, "update_node", Args(
                ("graph", _path), ("node", _probe.Id), ("fields", new Dictionary<string, object> { ["Content"] = "Chapter1" })));

            Assert.That(_probe.Content, Is.EqualTo("Chapter1"));
            var graph = McpTestClient.CallToolForObject(_protocol, "get_graph", Args(("graph", _path)));
            var described = ((List<object>)graph["nodes"]).Cast<Dictionary<string, object>>().Single(n => (string)n["id"] == _probe.Id);
            Assert.That(((Dictionary<string, object>)described["fields"])["Content"], Is.EqualTo("Chapter1"));
            Assert.That(described["label"], Is.EqualTo("Chapter1"), "the label follows the content");
        }

        [Test]
        public void UnknownField_IsRefusedWithTheFieldNames()
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "update_node", Args(
                ("graph", _path), ("node", _probe.Id), ("fields", new Dictionary<string, object> { ["YarnNode"] = "x" })));

            Assert.That(isError, Is.True);
            Assert.That(text, Does.Contain("Fields: Content"));
        }

        [Test]
        public void WrongType_IsRefused()
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "update_node", Args(
                ("graph", _path), ("node", _probe.Id), ("fields", new Dictionary<string, object> { ["Content"] = 3L })));

            Assert.That(isError, Is.True);
            Assert.That(text, Does.Contain("must be a string"));
            Assert.That(_probe.Content, Is.Null, "nothing changes");
        }

        // ---- add_parameter ----

        [Test]
        public void AddParameter_AddsABlackboardParameter()
        {
            var added = McpTestClient.CallToolForObject(_protocol, "add_parameter", Args(("graph", _path), ("name", " Affection "), ("type", "int"), ("default", 2L)));

            Assert.That(added["name"], Is.EqualTo("Affection"));
            var parameter = _graph.FindParameterByName("Affection");
            Assert.That(parameter.Type, Is.EqualTo(GraphParameterType.Int));
            Assert.That(parameter.IntValue, Is.EqualTo(2));
        }

        [TestCase("Affection", "Int", 1.5, "does not fit")]
        [TestCase("Affection", "Number", null, "Bool, Int, Float or String")]
        [TestCase("Taken", "Bool", null, "already has a parameter named 'Taken'")]
        public void AddParameter_RefusesBadInput(string name, string type, object value, string reason)
        {
            _graph.AddParameter(new GraphParameter("Taken", GraphParameterType.Bool));
            var args = Args(("graph", _path), ("name", name), ("type", type));
            if (value != null)
            {
                args["default"] = value;
            }

            var (text, isError) = McpTestClient.CallTool(_protocol, "add_parameter", args);

            Assert.That(isError, Is.True);
            Assert.That(text, Does.Contain(reason));
            Assert.That(_graph.Parameters.Count, Is.EqualTo(1), "nothing is added");
        }

        private static Dictionary<string, object> Args(params (string Key, object Value)[] fields) =>
            fields.ToDictionary(f => f.Key, f => f.Value);
    }
}
