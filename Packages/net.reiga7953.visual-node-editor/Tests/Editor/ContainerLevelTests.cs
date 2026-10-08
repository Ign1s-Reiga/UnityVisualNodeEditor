using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Clipboard;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>NodeGraphView の階層の切り替え（ドリルダウン）と、階層ごとの表示・作成・貼り付け。</summary>
    public sealed class ContainerLevelTests
    {
        private ContainerTestGraph _g;
        private NodeGraphView _view;
        private int _levelChanges;

        [SetUp]
        public void SetUp()
        {
            _g = new ContainerTestGraph();
            _view = new NodeGraphView();
            _view.Populate(_g.Asset);
            _levelChanges = 0;
            _view.LevelChanged += () => _levelChanges++;
        }

        [TearDown]
        public void TearDown() => _g.Dispose();

        // ---- 階層ごとの表示 ----

        [Test]
        public void Root_ShowsOnlyRootLevelElements()
        {
            Assert.That(VisibleNodeIds(), Is.EquivalentTo(new[] { _g.Entry.Id, _g.Stage.Id, _g.Result.Id }));
            Assert.That(_view.edges.ToList(), Has.Count.EqualTo(2));
            Assert.That(_view.graphElements.ToList().OfType<GroupView>(), Is.Empty);
            Assert.That(_view.graphElements.ToList().OfType<StickyNoteView>(), Is.Empty);
            Assert.That(_view.CurrentContainerId, Is.Empty);
            Assert.That(_view.LevelPath, Is.Empty);
        }

        [Test]
        public void EnterLevel_ShowsTheContainersContents()
        {
            _view.EnterLevel(_g.Stage.Id);

            Assert.That(VisibleNodeIds(), Is.EquivalentTo(new[] { _g.StageEntry.Id, _g.Play.Id, _g.Inner.Id, _g.ClearExit.Id }));
            Assert.That(_view.edges.ToList(), Has.Count.EqualTo(2), "StageEntry → Play → ClearExit");
            Assert.That(_view.graphElements.ToList().OfType<GroupView>().Single().GroupId, Is.EqualTo(_g.StageGroup.Id));
            Assert.That(_view.graphElements.ToList().OfType<StickyNoteView>().Single().StickyNoteId, Is.EqualTo(_g.StageNote.Id));
            Assert.That(_view.CurrentContainerId, Is.EqualTo(_g.Stage.Id));
            Assert.That(_levelChanges, Is.EqualTo(1));
        }

        [Test]
        public void EnterNestedLevel_BuildsThePathFromTheRoot_AndExitLevelGoesBackUp()
        {
            _view.EnterLevel(_g.Inner.Id);
            Assert.That(_view.LevelPath, Is.EqualTo(new[] { _g.Stage.Id, _g.Inner.Id }));
            Assert.That(VisibleNodeIds(), Is.EquivalentTo(new[] { _g.InnerEntry.Id, _g.Boss.Id, _g.InnerExit.Id }));

            _view.ExitLevel();
            Assert.That(_view.LevelPath, Is.EqualTo(new[] { _g.Stage.Id }));

            _view.ExitLevel();
            Assert.That(_view.LevelPath, Is.Empty);
            Assert.That(VisibleNodeIds(), Has.Member(_g.Stage.Id));

            var changes = _levelChanges;
            _view.ExitLevel();
            Assert.That(_levelChanges, Is.EqualTo(changes), "already at the root");
        }

        [Test]
        public void EnterLevel_WithANonContainer_ShowsTheRoot()
        {
            _view.EnterLevel(_g.Stage.Id);

            _view.EnterLevel(_g.Play.Id);

            Assert.That(_view.CurrentContainerId, Is.Empty);
            Assert.That(VisibleNodeIds(), Has.Member(_g.Stage.Id));
        }

        [Test]
        public void Rebuild_WhenTheShownContainerIsGone_FallsBackToTheNearestParent()
        {
            _view.EnterLevel(_g.Inner.Id);
            var changes = _levelChanges;

            // Undo などで、表示中のコンテナが消えた状態を作る
            _g.Asset.RemoveNode(_g.Inner);
            _view.Populate(_g.Asset);

            Assert.That(_view.CurrentContainerId, Is.EqualTo(_g.Stage.Id));
            Assert.That(VisibleNodeIds(), Has.Member(_g.Play.Id));
            Assert.That(_levelChanges, Is.EqualTo(changes + 1));
        }

        [Test]
        public void Rebuild_KeepsTheCurrentLevel()
        {
            _view.EnterLevel(_g.Stage.Id);
            var changes = _levelChanges;

            _view.Populate(_g.Asset);

            Assert.That(_view.CurrentContainerId, Is.EqualTo(_g.Stage.Id));
            Assert.That(_levelChanges, Is.EqualTo(changes), "the level did not change");
        }

        [Test]
        public void PopulateWithLevel_OpensThatLevelDirectly()
        {
            var view = new NodeGraphView();

            view.Populate(_g.Asset, _g.Inner.Id);

            Assert.That(view.LevelPath, Is.EqualTo(new[] { _g.Stage.Id, _g.Inner.Id }));
        }

        // ---- 作成 ----

        [Test]
        public void NewElements_GoToTheCurrentLevel()
        {
            _view.EnterLevel(_g.Stage.Id);

            var node = _view.CreateNode(typeof(StateNode), Vector2.zero);
            var group = _view.CreateGroup(Vector2.zero);
            var note = _view.CreateStickyNote(Vector2.zero);

            Assert.That(node.Data.ParentId, Is.EqualTo(_g.Stage.Id));
            Assert.That(_g.Asset.FindGroup(group.GroupId).ParentId, Is.EqualTo(_g.Stage.Id));
            Assert.That(_g.Asset.FindStickyNote(note.StickyNoteId).ParentId, Is.EqualTo(_g.Stage.Id));
        }

        [Test]
        public void NewNodeAtTheRoot_HasNoParent()
        {
            var node = _view.CreateNode(typeof(StateNode), Vector2.zero);

            Assert.That(node.Data.IsAtRoot, Is.True);
        }

        [Test]
        public void NewContainer_GetsAnEntryAndOneExitNodePerDefaultExit()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.AddNode(new EntryNode());
                var view = new NodeGraphView();
                view.Populate(graph);

                var container = (ContainerNode)view.CreateNode(typeof(StageTestContainer), Vector2.zero).Data;

                var children = graph.GetChildren(container.Id).ToList();
                Assert.That(children.OfType<ContainerEntryNode>().Count(), Is.EqualTo(1));
                Assert.That(children.OfType<ContainerExitNode>().Select(e => e.ExitId),
                    Is.EqualTo(container.Exits.Select(e => e.Id)), "one Exit node per exit, in exit order");
                Assert.That(VisibleIds(view), Has.No.Member(children[0].Id), "the contents stay inside the container");
                Assert.That(GraphValidator.Validate(graph).Where(i => i.Severity == GraphIssueSeverity.Error), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void NewExitNode_InsideAContainer_PointsAtItsFirstExit()
        {
            _view.EnterLevel(_g.Stage.Id);

            var exitNode = (ContainerExitNode)_view.CreateNode(typeof(ContainerExitNode), Vector2.zero).Data;

            Assert.That(exitNode.ExitId, Is.EqualTo(_g.Stage.Exits[0].Id));
        }

        // ---- 階層をまたぐ操作 ----

        [Test]
        public void FocusNode_InAnotherLevel_OpensThatLevelAndSelectsIt()
        {
            _view.FocusNode(_g.Boss.Id);

            Assert.That(_view.CurrentContainerId, Is.EqualTo(_g.Inner.Id));
            Assert.That(_view.selection.OfType<NodeView>().Single().NodeId, Is.EqualTo(_g.Boss.Id));
        }

        [Test]
        public void FindVisibleNodeId_MapsNestedNodesToTheirContainerOnThisLevel()
        {
            Assert.That(_view.FindVisibleNodeId(_g.Result.Id), Is.EqualTo(_g.Result.Id));
            Assert.That(_view.FindVisibleNodeId(_g.Play.Id), Is.EqualTo(_g.Stage.Id));
            Assert.That(_view.FindVisibleNodeId(_g.Boss.Id), Is.EqualTo(_g.Stage.Id));

            _view.EnterLevel(_g.Stage.Id);

            Assert.That(_view.FindVisibleNodeId(_g.Boss.Id), Is.EqualTo(_g.Inner.Id));
            Assert.That(_view.FindVisibleNodeId(_g.Result.Id), Is.Null, "a parent level is not shown");
            Assert.That(_view.FindVisibleNodeId("missing"), Is.Null);
        }

        [Test]
        public void RunningNodeInsideAContainer_HighlightsTheContainer()
        {
            _view.SetRunningNode(_g.Boss.Id);
            Assert.That(_view.FindNodeView(_g.Stage.Id).IsRunning, Is.True);

            _view.EnterLevel(_g.Stage.Id);
            Assert.That(_view.FindNodeView(_g.Inner.Id).IsRunning, Is.True);
            Assert.That(_view.FindNodeView(_g.Play.Id).IsRunning, Is.False);

            _view.EnterLevel(_g.Inner.Id);
            Assert.That(_view.FindNodeView(_g.Boss.Id).IsRunning, Is.True);
        }

        [Test]
        public void IssuesInsideAContainer_AreShownOnTheContainer()
        {
            _view.ShowIssues(new List<GraphIssue> { new GraphIssue(GraphIssueSeverity.Error, "Boss is broken", _g.Boss.Id) });

            var stage = _view.FindNodeView(_g.Stage.Id);
            Assert.That(stage.ClassListContains("vne-node--error"), Is.True);
            Assert.That(stage.tooltip, Does.Contain("Boss is broken"));
            Assert.That(_view.FindNodeView(_g.Result.Id).ClassListContains("vne-node--error"), Is.False);
        }

        [Test]
        public void CompatiblePorts_AreOnlyOnTheSameLevel()
        {
            _view.EnterLevel(_g.Stage.Id);

            // 別の階層のノードが紛れ込んでも、そのポートには繋がせない
            var stray = NodeViewFactory.Create(_g.Boss, _g.Asset);
            _view.AddElement(stray);
            var start = _view.FindNodeView(_g.Play.Id).FindPort(NodeView.OutputPortName, Direction.Output);

            var compatible = _view.GetCompatiblePorts(start, new NodeAdapter());

            Assert.That(compatible, Has.Member(_view.FindNodeView(_g.Inner.Id).FindPort(ContainerNode.InputPortId, Direction.Input)));
            Assert.That(compatible, Has.No.Member(stray.FindPort(NodeView.InputPortName, Direction.Input)));
        }

        [Test]
        public void PasteInsideAContainer_GoesToTheCurrentLevel()
        {
            var data = GraphClipboard.Serialize(_g.Asset, new[] { _g.Result.Id }, null, null);
            _view.EnterLevel(_g.Stage.Id);

            _view.unserializeAndPaste("Paste", data);

            var pasted = _g.Asset.Nodes.OfType<StateNode>().Single(n => n.Title == "Result" && n != _g.Result);
            Assert.That(pasted.ParentId, Is.EqualTo(_g.Stage.Id));
            Assert.That(VisibleNodeIds(), Has.Member(pasted.Id));
        }

        [Test]
        public void PasteAtTheRoot_LeavesOutContainerExitNodes()
        {
            var data = GraphClipboard.Serialize(_g.Asset, new[] { _g.ClearExit.Id }, null, null);
            var count = _g.Asset.Nodes.Count;

            _view.unserializeAndPaste("Paste", data);

            Assert.That(_g.Asset.Nodes, Has.Count.EqualTo(count));
        }

        [Test]
        public void PasteExitNodeIntoAnotherContainer_PointsAtThatContainersExit()
        {
            var data = GraphClipboard.Serialize(_g.Asset, new[] { _g.ClearExit.Id }, null, null);
            _view.EnterLevel(_g.Inner.Id);

            _view.unserializeAndPaste("Paste", data);

            var pasted = _g.Asset.GetChildren(_g.Inner.Id).OfType<ContainerExitNode>().Single(n => n != _g.InnerExit);
            Assert.That(pasted.ExitId, Is.EqualTo(_g.Inner.Exits[0].Id));
            Assert.That(GraphValidator.Validate(_g.Asset).Where(i => i.NodeId == pasted.Id), Is.Empty);
        }

        [Test]
        public void ContainerEntry_IsNotDeletedWithTheSelection()
        {
            _view.EnterLevel(_g.Stage.Id);
            _view.AddToSelection(_view.FindNodeView(_g.StageEntry.Id));
            _view.AddToSelection(_view.FindNodeView(_g.Play.Id));

            _view.DeleteSelection();

            Assert.That(_g.Asset.FindNode(_g.StageEntry.Id), Is.Not.Null, "the Entry is removed only with its container");
            Assert.That(_g.Asset.FindNode(_g.Play.Id), Is.Null);
        }

        [Test]
        public void DeletingAContainer_RemovesItsContents()
        {
            _view.AddToSelection(_view.FindNodeView(_g.Stage.Id));

            _view.DeleteSelection();

            Assert.That(_g.Asset.Nodes.Select(n => n.Id), Is.EquivalentTo(new[] { _g.Entry.Id, _g.Result.Id }));
            Assert.That(_g.Asset.Edges, Is.Empty);
            Assert.That(_g.Asset.Groups, Is.Empty);
            Assert.That(_g.Asset.StickyNotes, Is.Empty);
        }

        private List<string> VisibleNodeIds() => VisibleIds(_view);

        private static List<string> VisibleIds(NodeGraphView view) =>
            view.nodes.ToList().OfType<NodeView>().Select(v => v.NodeId).ToList();
    }
}
