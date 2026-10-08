using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class ContainerModelTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _rootEntry;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _rootEntry = Add(new EntryNode());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 出口の作成と編集 ----

        [Test]
        public void NewContainer_GetsDefaultExits()
        {
            Assert.That(new ContainerNode().Exits.Select(e => e.Name), Is.EqualTo(new[] { "Next" }));
            Assert.That(new StageTestContainer().Exits.Select(e => e.Name), Is.EqualTo(new[] { "Clear", "GameOver" }));
        }

        [Test]
        public void TestOnlyContainer_IsNotInTheCreateNodeMenu()
        {
            var types = NodeMenuCatalog.GetItems().Select(i => i.NodeType).ToList();
            Assert.That(types, Has.No.Member(typeof(StageTestContainer)));
            Assert.That(types, Has.Member(typeof(ContainerNode)));
            Assert.That(types, Has.Member(typeof(ContainerExitNode)));
            Assert.That(types, Has.No.Member(typeof(ContainerEntryNode)), "created with its container, hidden from the menu");
        }

        [Test]
        public void TryGetExit_FindsByNameOnly()
        {
            var stage = new StageTestContainer();

            Assert.That(stage.TryGetExit("Clear", out var clear), Is.True);
            Assert.That(clear.Name, Is.EqualTo("Clear"));
            Assert.That(stage.TryGetExit("clear", out _), Is.False, "names are case-sensitive");
            Assert.That(stage.TryGetExit("Unknown", out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(stage.TryGetExit(null, out _), Is.False);
        }

        [Test]
        public void RenamingAnExit_KeepsEdgesAndExitNodeReferences()
        {
            var (stage, _, exitNode) = AddContainerWithEntryAndExit(new StageTestContainer(), "Clear");
            var result = Add(new StateNode());
            var clear = Exit(stage, "Clear");
            Connect(stage, clear.Id, result);

            stage.RenameExit(clear.Id, "Victory");

            Assert.That(stage.TryGetExit("Victory", out var renamed), Is.True);
            Assert.That(renamed.Id, Is.EqualTo(clear.Id), "the ID is stable");
            Assert.That(_graph.Edges.Single().FromPort, Is.EqualTo(clear.Id));
            Assert.That(exitNode.ExitId, Is.EqualTo(clear.Id));
            Assert.That(Validate(), Is.Empty);
        }

        [Test]
        public void ReorderingExits_KeepsEdges()
        {
            var (stage, _, _) = AddContainerWithEntryAndExit(new StageTestContainer(), "Clear");
            var gameOverNode = AddChild(stage, new ContainerExitNode { ExitId = Exit(stage, "GameOver").Id });
            var win = Add(new StateNode());
            var lose = Add(new StateNode());
            Connect(stage, Exit(stage, "Clear").Id, win);
            Connect(stage, Exit(stage, "GameOver").Id, lose);

            stage.MoveExit(Exit(stage, "GameOver").Id, 0);

            Assert.That(stage.Exits.Select(e => e.Name), Is.EqualTo(new[] { "GameOver", "Clear" }));
            Assert.That(_graph.Edges.Single(e => e.ToNodeId == win.Id).FromPort, Is.EqualTo(Exit(stage, "Clear").Id));
            Assert.That(_graph.Edges.Single(e => e.ToNodeId == lose.Id).FromPort, Is.EqualTo(Exit(stage, "GameOver").Id));
            Assert.That(gameOverNode.ExitId, Is.EqualTo(Exit(stage, "GameOver").Id));
            Assert.That(Validate(), Is.Empty);
        }

        [Test]
        public void RemovingAnExit_RemovesItsEdgesAndFlagsExitNodes()
        {
            var (stage, _, exitNode) = AddContainerWithEntryAndExit(new StageTestContainer(), "Clear");
            var win = Add(new StateNode());
            var lose = Add(new StateNode());
            var clearId = Exit(stage, "Clear").Id;
            Connect(stage, clearId, win);
            Connect(stage, Exit(stage, "GameOver").Id, lose);

            var removed = _graph.RemoveContainerExit(stage, clearId);

            Assert.That(removed.Select(e => e.ToNodeId), Is.EqualTo(new[] { win.Id }));
            Assert.That(_graph.Edges.Select(e => e.ToNodeId), Is.EqualTo(new[] { lose.Id }), "other exits keep their edges");
            Assert.That(_graph.Nodes, Has.Member(exitNode), "Exit nodes are kept");
            var issue = Validate().Single();
            Assert.That(issue.NodeId, Is.EqualTo(exitNode.Id), "invariant 6: dangling Exit node");
            Assert.That(_graph.RemoveContainerExit(stage, clearId), Is.Empty, "already removed");
        }

        // ---- 削除 ----

        [Test]
        public void DeletingAContainer_RemovesNestedDescendantsAndTheirEdgesGroupsAndStickyNotes()
        {
            var (outer, outerEntry, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var (inner, innerEntry, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next", outer);
            var deep = AddChild(inner, new StateNode());
            Connect(innerEntry, "out", deep);
            Connect(outerEntry, "out", inner);
            var outside = Add(new StateNode());
            Connect(_rootEntry, "out", outer);
            Connect(outer, Exit(outer, "Next").Id, outside);
            var innerGroup = new GroupData { ParentId = inner.Id };
            innerGroup.AddNode(deep.Id);
            _graph.AddGroup(innerGroup);
            _graph.AddStickyNote(new StickyNoteData { ParentId = outer.Id });
            var rootNote = new StickyNoteData();
            _graph.AddStickyNote(rootNote);

            Assert.That(_graph.RemoveNode(outer), Is.True);

            Assert.That(_graph.Nodes, Is.EquivalentTo(new NodeData[] { _rootEntry, outside }));
            Assert.That(_graph.Edges, Is.Empty);
            Assert.That(_graph.Groups, Is.Empty);
            Assert.That(_graph.StickyNotes, Is.EqualTo(new[] { rootNote }));
        }

        // ---- 検証（条件 1〜9）----

        [Test]
        public void ValidContainerGraph_HasNoIssues()
        {
            var (stage, entry, exitNode) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var play = AddChild(stage, new StateNode());
            Connect(_rootEntry, "out", stage);
            Connect(entry, "out", play);
            Connect(play, "out", exitNode);

            Assert.That(Validate(), Is.Empty);
        }

        [Test]
        public void Invariant1_EdgeAcrossLevels()
        {
            var (stage, _, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var inside = AddChild(stage, new StateNode());
            Connect(_rootEntry, "out", inside);

            Assert.That(Messages(), Has.Some.Contains("different levels"));
        }

        [Test]
        public void Invariant2_ContainerWithoutOrWithTwoEntries()
        {
            var empty = Add(new ContainerNode { Title = "Empty" });
            var (twice, _, _) = AddContainerWithEntryAndExit(new ContainerNode { Title = "Twice" }, "Next");
            var extra = AddChild(twice, new ContainerEntryNode());

            var issues = Validate();

            Assert.That(issues.Any(i => i.NodeId == empty.Id && i.Message.Contains("no Entry")), Is.True);
            Assert.That(issues.Any(i => i.NodeId == extra.Id && i.Message.Contains("more than one Entry")), Is.True);
        }

        [Test]
        public void Invariant3_EntryAndExitOnlyInsideContainersAndRootEntryOnlyAtRoot()
        {
            var strayEntry = Add(new ContainerEntryNode());
            var strayExit = Add(new ContainerExitNode());
            var (stage, _, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var nestedRootEntry = AddChild(stage, new EntryNode());

            var issues = Validate();

            Assert.That(issues.Any(i => i.NodeId == strayEntry.Id && i.Message.Contains("only be placed inside a container")), Is.True);
            Assert.That(issues.Any(i => i.NodeId == strayExit.Id && i.Message.Contains("only be placed inside a container")), Is.True);
            Assert.That(issues.Any(i => i.NodeId == nestedRootEntry.Id && i.Message.Contains("root level")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("only one Entry")), Is.False, "nested EntryNode does not count as a second root Entry");
        }

        [Test]
        public void Invariant4_MissingOrNonContainerParentAndParentLoops()
        {
            var orphan = Add(new StateNode { ParentId = "missing" });
            var state = Add(new StateNode());
            var underState = Add(new StateNode { ParentId = state.Id });
            var a = Add(new ContainerNode { Title = "A" });
            var b = Add(new ContainerNode { Title = "B", ParentId = a.Id });
            a.ParentId = b.Id;

            var issues = Validate();

            Assert.That(issues.Any(i => i.NodeId == orphan.Id && i.Message.Contains("does not exist")), Is.True);
            Assert.That(issues.Any(i => i.NodeId == underState.Id && i.Message.Contains("does not exist")), Is.True);
            Assert.That(issues.Count(i => i.Message.Contains("forms a loop")), Is.EqualTo(2));
        }

        [Test]
        public void Invariant5_EmptyOrDuplicateExitNamesAndDuplicateIds()
        {
            var (stage, _, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            stage.AddExit("Next");
            stage.AddExit("  ");
            stage.AddExit("Next");

            var messages = Messages();
            Assert.That(messages.Count(m => m.Contains("more than one exit named 'Next'")), Is.EqualTo(1), "reported once per name");
            Assert.That(messages, Has.Some.Contains("empty name"));

            // 出口の ID ごと複製された状態（生のリストで「+」を押したのと同じ）
            var duplicateIds = Add(new ContainerNode());
            AddChild(duplicateIds, new ContainerEntryNode());
            duplicateIds.AddExit("Other");
            ReplaceSecondExit(duplicateIds, duplicateIds.Exits[0]);

            Assert.That(Messages(), Has.Some.Contains("two exits with the same ID"));
        }

        [Test]
        public void Invariant6_ExitNodeReferringToUnknownExit()
        {
            var (stage, _, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var wrong = AddChild(stage, new ContainerExitNode { ExitId = "not-an-exit" });

            Assert.That(Validate().Any(i => i.NodeId == wrong.Id && i.Message.Contains("does not have")), Is.True);
        }

        [Test]
        public void Invariant7_EdgeFromUnknownContainerPort()
        {
            var (stage, _, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var after = Add(new StateNode());
            Connect(stage, "no-such-exit", after);

            Assert.That(Messages(), Has.Some.Contains("exit that no longer exists"));
        }

        [Test]
        public void Invariant8_LeftoverDescendantsAreReportedAsMissingParent()
        {
            // RemoveNode を通さずにコンテナだけが消えた（手で編集されたなど）場合
            var (stage, entry, _) = AddContainerWithEntryAndExit(new ContainerNode(), "Next");
            var nodes = (List<NodeData>)typeof(NodeGraphAsset)
                .GetField("_nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_graph);
            nodes.Remove(stage);

            Assert.That(Validate().Any(i => i.NodeId == entry.Id && i.Message.Contains("does not exist")), Is.True);
        }

        [Test]
        public void Invariant9_DeletedOrDuplicatedEntryIsDetected()
        {
            var (stage, entry, _) = AddContainerWithEntryAndExit(new ContainerNode { Title = "Stage" }, "Next");

            _graph.RemoveNode(entry);
            Assert.That(Messages(), Has.Some.Contains("has no Entry"));

            AddChild(stage, new ContainerEntryNode());
            AddChild(stage, new ContainerEntryNode());
            Assert.That(Messages(), Has.Some.Contains("more than one Entry"));
        }

        // ---- ヘルパー ----

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private T AddChild<T>(NodeData parent, T node) where T : NodeData
        {
            node.ParentId = parent.Id;
            return Add(node);
        }

        private (TContainer container, ContainerEntryNode entry, ContainerExitNode exit) AddContainerWithEntryAndExit<TContainer>(
            TContainer container, string exitName, NodeData parent = null)
            where TContainer : ContainerNode
        {
            if (parent != null)
            {
                container.ParentId = parent.Id;
            }

            Add(container);
            var entry = AddChild(container, new ContainerEntryNode());
            var exit = AddChild(container, new ContainerExitNode { ExitId = Exit(container, exitName).Id });
            return (container, entry, exit);
        }

        private static ContainerExit Exit(ContainerNode container, string name) =>
            container.TryGetExit(name, out var exit) ? exit : throw new ArgumentException(name);

        private void Connect(NodeData from, string fromPort, NodeData to) =>
            _graph.AddEdge(new EdgeData(from.Id, fromPort, to.Id, "in"));

        private List<GraphIssue> Validate() => GraphValidator.Validate(_graph);

        private List<string> Messages() => Validate().Select(i => i.Message).ToList();

        private static void ReplaceSecondExit(ContainerNode container, ContainerExit exit)
        {
            var exits = (List<ContainerExit>)typeof(ContainerNode)
                .GetField("_exits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(container);
            exits[1] = exit;
        }
    }
}
