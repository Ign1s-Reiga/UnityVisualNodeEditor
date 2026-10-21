using System.Linq;
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
            var node = new StateNode();
            _graph.AddNode(node);
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, node.Id);

            // Description と Screen のみ（Title はヘッダー、Id / Position は HideInInspector、振る舞いは専用の一覧）。Screen は Create Screen… の付いた欄
            Assert.That(inspector.NodeId, Is.EqualTo(node.Id));
            var fields = inspector.Query<PropertyField>().ToList()
                .Where(f => f.GetFirstAncestorOfType<NodeBehaviourListView>() == null).ToList();
            Assert.That(fields.Select(f => f.label), Is.EqualTo(new[] { "Description", "Screen" }));
            Assert.That(fields[1].GetFirstAncestorOfType<NodeScreenField>(), Is.Not.Null);
            Assert.That(inspector.Q<NodeScreenField>().ButtonText, Is.EqualTo(NodeScreenField.CreateLabel));
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
        public void Show_ANodeThatGoesSeveralWays_ListsItsTransitions()
        {
            var play = new StateNode { Title = "Play" };
            var win = new EventNode { EventName = "Win" };
            var lose = new EventNode { EventName = "Lose" };
            var stage = new ContainerNode();
            foreach (var node in new NodeData[] { play, win, lose, stage })
            {
                _graph.AddNode(node);
            }

            _graph.AddEdge(new EdgeData(play.Id, "out", win.Id, "in"));
            _graph.AddEdge(new EdgeData(play.Id, "out", lose.Id, "in"));
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, play.Id);
            Assert.That(inspector.Q<TransitionListView>().IsShown, Is.True);
            Assert.That(inspector.Q<TransitionListView>().Items.Select(e => e.ToNodeId), Is.EqualTo(new[] { win.Id, lose.Id }));

            inspector.Show(_graph, win.Id);
            Assert.That(inspector.Q<TransitionListView>().IsShown, Is.False, "no edges out");

            inspector.Show(_graph, stage.Id);
            Assert.That(inspector.Q<TransitionListView>(), Is.Null, "containers lead out per exit");
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
