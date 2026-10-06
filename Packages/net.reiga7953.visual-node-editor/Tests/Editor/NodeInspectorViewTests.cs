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

            // EventName のみ（Title はヘッダー、Id / Position は HideInInspector）
            Assert.That(inspector.NodeId, Is.EqualTo(node.Id));
            var fields = inspector.Query<PropertyField>().ToList();
            Assert.That(fields, Has.Count.EqualTo(1));
            Assert.That(fields[0].label, Is.EqualTo("Event Name"));
        }

        [Test]
        public void Show_HeaderHasTitleFieldWithPlaceholder()
        {
            var node = new EventNode();
            _graph.AddNode(node);
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, node.Id);

            var title = inspector.Q<TextField>(className: "vne-inspector-view__title");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.bindingPath, Does.EndWith("." + NodeInspectorView.TitlePropertyName));
            Assert.That(title.textEdition.placeholder, Is.EqualTo("(Title)"));
            Assert.That(inspector.Q(className: "vne-inspector-view__icon"), Is.Not.Null);
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
