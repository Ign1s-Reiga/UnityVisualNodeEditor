using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Issues;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>アドオン（Yarn Spinner 連携など）のための拡張点: 内容から決まる名前、ノード自身の検証、エディタの検証、Runner の検索。</summary>
    public sealed class ExtensionPointTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void ContentTitle_LabelsAnUntitledNode()
        {
            var node = new ProbeNode { Content = " Chapter1 " };

            Assert.That(NodeDisplay.GetNodeLabel(node), Is.EqualTo("Chapter1"));
            Assert.That(NodeDisplay.GetNodeLabel(new ProbeNode()), Is.EqualTo("Probe"), "falls back to the type name");

            node.Title = "Opening";
            Assert.That(NodeDisplay.GetNodeLabel(node), Is.EqualTo("Opening"));
        }

        [Test]
        public void NodeIssues_AreValidated()
        {
            var node = new ProbeNode();
            _graph.AddNode(node);

            Assert.That(GraphValidator.Validate(_graph).Any(i => i.NodeId == node.Id && i.Message == "Probe has no content."), Is.True);
        }

        [Test]
        public void EditorChecks_AreFoundAutomatically()
        {
            _graph.name = ProbeIssueCheck.GraphName;

            Assert.That(GraphIssues.Collect(_graph).Select(i => i.Message), Has.Member("Found by the probe check."));
        }

        [Test]
        public void FindRunning_PicksTheRunnerOfTheGraph()
        {
            _graph.AddNode(new EntryNode());
            var runner = new GraphRunner(_graph);
            Assert.That(GraphRunner.FindRunning(_graph), Is.Null);

            runner.Start();
            try
            {
                Assert.That(GraphRunner.FindRunning(_graph), Is.SameAs(runner));
                Assert.That(GraphRunner.FindRunning(), Is.Not.Null);
            }
            finally
            {
                runner.Stop();
            }

            Assert.That(GraphRunner.FindRunning(_graph), Is.Null);
        }
    }
}
