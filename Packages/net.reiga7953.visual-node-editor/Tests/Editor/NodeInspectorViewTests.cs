using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeInspectorViewTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Show_ListsVisibleFieldsOnly()
        {
            var node = new EventNode();
            _graph.AddNode(node);
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, node.Id);

            // Title と EventName のみ（Id / Position は HideInInspector）
            Assert.That(inspector.NodeId, Is.EqualTo(node.Id));
            Assert.That(inspector.Query<PropertyField>().ToList(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Show_UnknownNode_ShowsNothing()
        {
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, "missing");

            Assert.That(inspector.NodeId, Is.Null);
            Assert.That(inspector.Query<PropertyField>().ToList(), Is.Empty);
        }
    }
}
