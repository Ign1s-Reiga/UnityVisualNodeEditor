using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>最初の一歩: 新しいグラフの Entry と、空のキャンバス・空のコンテナの案内。</summary>
    public sealed class EmptyStateTests
    {
        private NodeGraphAsset _graph;

        [TearDown]
        public void TearDown()
        {
            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
            }
        }

        // ---- どの案内を出すか ----

        [Test]
        public void Hint_AtTheRoot()
        {
            Assert.That(EmptyStateHint.For(new NodeData[0], false), Is.EqualTo(EmptyStateKind.EmptyGraph));
            Assert.That(EmptyStateHint.For(new NodeData[] { new EntryNode() }, false), Is.EqualTo(EmptyStateKind.OnlyEntry));
            Assert.That(EmptyStateHint.For(new NodeData[] { new EntryNode(), new SceneNode() }, false), Is.EqualTo(EmptyStateKind.None));
            Assert.That(EmptyStateHint.For(new NodeData[] { new NoteNode() }, false), Is.EqualTo(EmptyStateKind.None),
                "any node the user placed counts");
            Assert.That(EmptyStateHint.For(null, false), Is.EqualTo(EmptyStateKind.EmptyGraph));
        }

        [Test]
        public void Hint_InsideAContainer()
        {
            Assert.That(EmptyStateHint.For(new NodeData[] { new ContainerEntryNode(), new ContainerExitNode() }, true),
                Is.EqualTo(EmptyStateKind.EmptyContainer));
            Assert.That(EmptyStateHint.For(new NodeData[0], true), Is.EqualTo(EmptyStateKind.EmptyContainer));
            Assert.That(EmptyStateHint.For(new NodeData[] { new ContainerEntryNode(), new StateNode() }, true),
                Is.EqualTo(EmptyStateKind.None));
        }

        [Test]
        public void Hint_ClassNames()
        {
            Assert.That(EmptyStateHint.GetClassName(EmptyStateKind.None), Is.Empty);
            Assert.That(EmptyStateHint.GetClassName(EmptyStateKind.OnlyEntry), Is.EqualTo("vne-empty-hint--only-entry"));
        }

        // ---- 新しいグラフ ----

        [Test]
        public void NewGraph_StartsWithAnEntryAndNoErrors()
        {
            _graph = NodeGraphFactory.CreateNew();

            Assert.That(_graph.Nodes.Single(), Is.TypeOf<EntryNode>());
            Assert.That(_graph.Nodes.Single().IsAtRoot, Is.True);
            Assert.That(GraphValidator.Validate(_graph).Where(i => i.Severity == GraphIssueSeverity.Error), Is.Empty);
        }

        // ---- 案内の表示とボタン ----

        [Test]
        public void NewGraph_ShowsTheAddFirstSceneHint()
        {
            _graph = NodeGraphFactory.CreateNew();
            var view = new NodeGraphView();

            view.Populate(_graph);

            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.OnlyEntry));
            Assert.That(view.Q(className: "vne-empty-hint").ClassListContains("vne-empty-hint--only-entry"), Is.True);
        }

        [Test]
        public void AddFirstScene_ConnectsItFromEntryAndSelectsIt()
        {
            _graph = NodeGraphFactory.CreateNew();
            var entry = _graph.Nodes.Single();
            var view = new NodeGraphView();
            view.Populate(_graph);

            var sceneView = view.AddFirstScene();

            Assert.That(sceneView.Data, Is.TypeOf<SceneNode>());
            Assert.That(_graph.Edges.Single().FromNodeId, Is.EqualTo(entry.Id));
            Assert.That(_graph.Edges.Single().ToNodeId, Is.EqualTo(sceneView.NodeId));
            Assert.That(view.selection.OfType<NodeView>().Single().NodeId, Is.EqualTo(sceneView.NodeId));
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.None));
            Assert.That(view.Q(className: "vne-empty-hint").ClassListContains("vne-empty-hint--only-entry"), Is.False);
        }

        [Test]
        public void AddFirstScene_OnAnEmptyGraph_AlsoCreatesTheEntry()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var view = new NodeGraphView();
            view.Populate(_graph);
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.EmptyGraph));

            view.AddFirstScene();

            var entry = _graph.Nodes.OfType<EntryNode>().Single();
            var scene = _graph.Nodes.OfType<SceneNode>().Single();
            Assert.That(_graph.Edges.Single().FromNodeId, Is.EqualTo(entry.Id));
            Assert.That(_graph.Edges.Single().ToNodeId, Is.EqualTo(scene.Id));
        }

        [Test]
        public void AddEntry_OnAnEmptyGraph()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var view = new NodeGraphView();
            view.Populate(_graph);

            view.AddEntry();

            Assert.That(_graph.Nodes.Single(), Is.TypeOf<EntryNode>());
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.OnlyEntry));
        }

        [Test]
        public void EmptyContainer_ShowsItsHint_AndAddStatePutsItBetweenEntryAndExit()
        {
            _graph = NodeGraphFactory.CreateNew();
            var view = new NodeGraphView();
            view.Populate(_graph);
            var container = (ContainerNode)view.CreateNode(typeof(ContainerNode), Vector2.zero).Data;
            view.EnterLevel(container.Id);
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.EmptyContainer));

            var state = view.AddInsideContainer(typeof(StateNode)).Data;

            var entry = _graph.GetChildren(container.Id).OfType<ContainerEntryNode>().Single();
            var exit = _graph.GetChildren(container.Id).OfType<ContainerExitNode>().Single();
            var edges = _graph.Edges.Select(e => (e.FromNodeId, e.ToNodeId)).ToList();
            Assert.That(state.ParentId, Is.EqualTo(container.Id));
            Assert.That(edges, Has.Member((entry.Id, state.Id)));
            Assert.That(edges, Has.Member((state.Id, exit.Id)));
            Assert.That(edges, Has.No.Member((entry.Id, exit.Id)), "the default pass-through is replaced");
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.None));
            Assert.That(GraphValidator.Validate(_graph).Where(i => i.Severity == GraphIssueSeverity.Error), Is.Empty);
        }

        [Test]
        public void AddInsideContainer_AtTheRoot_DoesNothing()
        {
            _graph = NodeGraphFactory.CreateNew();
            var view = new NodeGraphView();
            view.Populate(_graph);

            Assert.That(view.AddInsideContainer(typeof(StateNode)), Is.Null);
            Assert.That(_graph.Nodes, Has.Count.EqualTo(1));
        }
    }
}
