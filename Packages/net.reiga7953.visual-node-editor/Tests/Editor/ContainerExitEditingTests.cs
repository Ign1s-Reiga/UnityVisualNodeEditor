using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>コンテナの出口の編集（インスペクタの一覧・Exit ノードのドロップダウン）と、その結果のポート・エッジ・表示。</summary>
    public sealed class ContainerExitEditingTests
    {
        private ContainerTestGraph _g;
        private string _clearId;
        private string _gameOverId;

        [SetUp]
        public void SetUp()
        {
            _g = new ContainerTestGraph();
            _clearId = ContainerTestGraph.ExitId(_g.Stage, "Clear");
            _gameOverId = ContainerTestGraph.ExitId(_g.Stage, "GameOver");
        }

        [TearDown]
        public void TearDown() => _g.Dispose();

        // ---- 編集の操作 ----

        [Test]
        public void Add_GivesANameNotUsedInTheContainer()
        {
            Assert.That(ContainerExitEditing.Add(_g.Asset, _g.Stage).Name, Is.EqualTo("Exit"));
            Assert.That(ContainerExitEditing.Add(_g.Asset, _g.Stage).Name, Is.EqualTo("Exit 2"));
            Assert.That(ContainerExitEditing.Add(_g.Asset, _g.Stage, "Clear").Name, Is.EqualTo("Clear 2"));
            Assert.That(ContainerExitEditing.Add(_g.Asset, _g.Stage, " Boss ").Name, Is.EqualTo("Boss"));
            Assert.That(_g.Stage.Exits, Has.Count.EqualTo(6));
        }

        [Test]
        public void Rename_RejectsEmptyAndDuplicateNames()
        {
            Assert.That(ContainerExitEditing.Rename(_g.Asset, _g.Stage, _clearId, "  "), Is.False);
            Assert.That(ContainerExitEditing.Rename(_g.Asset, _g.Stage, _clearId, "GameOver"), Is.False);
            Assert.That(ContainerExitEditing.Rename(_g.Asset, _g.Stage, _clearId, "Clear"), Is.True, "same name is fine");
            Assert.That(_g.Stage.FindExit(_clearId).Name, Is.EqualTo("Clear"));
        }

        [Test]
        public void Rename_KeepsTheEdgeAndUpdatesPortLabelAndExitNodeTitle()
        {
            Assert.That(ContainerExitEditing.Rename(_g.Asset, _g.Stage, _clearId, " Victory "), Is.True);

            Assert.That(_g.Stage.FindExit(_clearId).Name, Is.EqualTo("Victory"));
            Assert.That(_g.Asset.Edges.Any(e => e.FromNodeId == _g.Stage.Id && e.FromPort == _clearId && e.ToNodeId == _g.Result.Id), Is.True);
            var containerView = NodeViewFactory.Create(_g.Stage, _g.Asset);
            Assert.That(containerView.FindPort(_clearId, Direction.Output).portName, Is.EqualTo("Victory"));
            Assert.That(NodeViewFactory.Create(_g.ClearExit, _g.Asset).title, Is.EqualTo("Victory"));
        }

        [Test]
        public void Move_ChangesThePortOrder()
        {
            Assert.That(ContainerExitEditing.Move(_g.Asset, _g.Stage, _gameOverId, 0), Is.True);

            var ports = NodeViewFactory.Create(_g.Stage, _g.Asset).outputContainer.Query<Port>().ToList();
            Assert.That(ports.Select(NodeView.GetPortId), Is.EqualTo(new[] { _gameOverId, _clearId }));
            Assert.That(_g.Asset.Edges.Count(e => e.FromPort == _clearId), Is.EqualTo(1), "edges follow the exit, not the position");
        }

        [Test]
        public void Remove_WithConnections_AsksFirst_AndCancellingKeepsEverything()
        {
            string asked = null;

            var removed = ContainerExitEditing.Remove(_g.Asset, _g.Stage, _clearId, message =>
            {
                asked = message;
                return false;
            });

            Assert.That(removed, Is.False);
            Assert.That(asked, Does.Contain("Clear → Result"));
            Assert.That(asked, Does.Contain("1 Exit node"));
            Assert.That(_g.Stage.FindExit(_clearId), Is.Not.Null);
            Assert.That(_g.Asset.Edges.Count(e => e.FromPort == _clearId), Is.EqualTo(1));
        }

        [Test]
        public void Remove_Confirmed_RemovesTheExitAndItsEdges_ButKeepsTheExitNodes()
        {
            Assert.That(ContainerExitEditing.Remove(_g.Asset, _g.Stage, _clearId, _ => true), Is.True);

            Assert.That(_g.Stage.FindExit(_clearId), Is.Null);
            Assert.That(_g.Asset.Edges.Any(e => e.FromPort == _clearId), Is.False);
            Assert.That(_g.Asset.FindNode(_g.ClearExit.Id), Is.Not.Null);
            Assert.That(GraphValidator.Validate(_g.Asset).Where(i => i.Severity == GraphIssueSeverity.Error).Select(i => i.NodeId),
                Has.Member(_g.ClearExit.Id), "the Exit node now points to a missing exit");
        }

        [Test]
        public void Remove_WithoutAnyImpact_DoesNotAsk()
        {
            var asked = false;

            Assert.That(ContainerExitEditing.Remove(_g.Asset, _g.Stage, _gameOverId, _ => asked = true), Is.True);

            Assert.That(asked, Is.False);
            Assert.That(_g.Stage.Exits.Select(e => e.Id), Is.EqualTo(new[] { _clearId }));
        }

        [Test]
        public void Remove_CanBeUndone()
        {
            Undo.IncrementCurrentGroup();
            ContainerExitEditing.Remove(_g.Asset, _g.Stage, _clearId, _ => true);

            Undo.PerformUndo();

            var stage = (ContainerNode)_g.Asset.FindNode(_g.Stage.Id);
            Assert.That(stage.FindExit(_clearId)?.Name, Is.EqualTo("Clear"));
            Assert.That(_g.Asset.Edges.Count(e => e.FromNodeId == stage.Id && e.FromPort == _clearId), Is.EqualTo(1));
        }

        // ---- コンテナのインスペクタの一覧 ----

        [Test]
        public void ExitList_RaisesChangedForEachEdit()
        {
            var list = new ContainerExitListView(_g.Asset, _g.Stage);
            var changes = 0;
            list.Changed += () => changes++;

            list.AddExit();
            list.RenameExit(_clearId, "Victory");
            list.MoveExit(_gameOverId, 0);
            list.RemoveExit(_gameOverId);

            Assert.That(changes, Is.EqualTo(4));
            Assert.That(_g.Stage.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Victory", "Exit" }));
        }

        [Test]
        public void ExitList_RejectedName_ShowsWhyAndChangesNothing()
        {
            var list = new ContainerExitListView(_g.Asset, _g.Stage);
            var changes = 0;
            list.Changed += () => changes++;

            Assert.That(list.RenameExit(_clearId, "GameOver"), Is.False);

            Assert.That(list.Message, Does.Contain("GameOver"));
            Assert.That(changes, Is.Zero);
            Assert.That(_g.Stage.FindExit(_clearId).Name, Is.EqualTo("Clear"));
        }

        [Test]
        public void ExitList_RemoveUsesItsConfirmation()
        {
            var list = new ContainerExitListView(_g.Asset, _g.Stage) { Confirm = _ => false };

            Assert.That(list.RemoveExit(_clearId), Is.False);
            Assert.That(_g.Stage.FindExit(_clearId), Is.Not.Null);

            list.Confirm = _ => true;
            Assert.That(list.RemoveExit(_clearId), Is.True);
            Assert.That(_g.Stage.FindExit(_clearId), Is.Null);
        }

        // ---- Exit ノードのドロップダウン ----

        [Test]
        public void ExitPicker_ListsTheParentsExitsAndNewExit()
        {
            var picker = new ContainerExitPicker(_g.Asset, _g.ClearExit);

            Assert.That(picker.Choices, Is.EqualTo(new[] { "Clear", "GameOver", ContainerExitPicker.NewExitChoice }));
            Assert.That(picker.Q<DropdownField>().value, Is.EqualTo("Clear"));
            Assert.That(picker.Q<TextField>().value, Is.EqualTo("Clear"));
        }

        [Test]
        public void ExitPicker_PickingAnotherExit_RetargetsTheExitNode()
        {
            var picker = new ContainerExitPicker(_g.Asset, _g.ClearExit);
            var changes = 0;
            picker.Changed += () => changes++;

            Assert.That(picker.Pick(1), Is.True);
            Assert.That(picker.Pick(1), Is.False, "already that exit");

            Assert.That(_g.ClearExit.ExitId, Is.EqualTo(_gameOverId));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ExitPicker_NewExit_AddsAnExitToTheParentAndSelectsIt()
        {
            var picker = new ContainerExitPicker(_g.Asset, _g.ClearExit);

            Assert.That(picker.Pick(2), Is.True);

            Assert.That(_g.Stage.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Clear", "GameOver", "Exit" }));
            Assert.That(_g.ClearExit.ExitId, Is.EqualTo(_g.Stage.Exits[2].Id));
            Assert.That(NodeViewFactory.Create(_g.Stage, _g.Asset).outputContainer.Query<Port>().ToList(), Has.Count.EqualTo(3),
                "the container gets a port for the new exit");
        }

        [Test]
        public void ExitPicker_RenamesTheSelectedExit()
        {
            var picker = new ContainerExitPicker(_g.Asset, _g.ClearExit);

            Assert.That(picker.RenameSelectedExit("GameOver"), Is.False);
            Assert.That(picker.Message, Is.Not.Empty);
            Assert.That(picker.RenameSelectedExit("Win"), Is.True);

            Assert.That(_g.Stage.FindExit(_clearId).Name, Is.EqualTo("Win"));
        }

        [Test]
        public void ExitPicker_WithAMissingExit_SaysSo()
        {
            _g.ClearExit.ExitId = "removed";

            var picker = new ContainerExitPicker(_g.Asset, _g.ClearExit);

            Assert.That(picker.Q<DropdownField>().value, Is.EqualTo(ContainerExitPicker.MissingExitChoice));
            Assert.That(picker.Q<TextField>().enabledSelf, Is.False);
        }

        // ---- インスペクタへの組み込み ----

        [Test]
        public void Inspector_ShowsTheExitListForContainersAndThePickerForExitNodes()
        {
            var inspector = new NodeInspectorView();

            inspector.Show(_g.Asset, _g.Stage.Id);
            Assert.That(inspector.Q<ContainerExitListView>(), Is.Not.Null);
            Assert.That(inspector.Q<ContainerExitPicker>(), Is.Null);

            inspector.Show(_g.Asset, _g.ClearExit.Id);
            Assert.That(inspector.Q<ContainerExitPicker>(), Is.Not.Null);
            Assert.That(inspector.Q<ContainerExitListView>(), Is.Null);

            inspector.Show(_g.Asset, _g.Play.Id);
            Assert.That(inspector.Q<ContainerExitListView>(), Is.Null);
            Assert.That(inspector.Q<ContainerExitPicker>(), Is.Null);
        }

        [Test]
        public void Inspector_ReportsExitEditsAsStructureChanges()
        {
            var inspector = new NodeInspectorView();
            var changed = new List<string>();
            inspector.StructureChanged += changed.Add;
            inspector.Show(_g.Asset, _g.Stage.Id);

            inspector.Q<ContainerExitListView>().AddExit();

            Assert.That(changed, Is.EqualTo(new[] { _g.Stage.Id }));
        }
    }
}
