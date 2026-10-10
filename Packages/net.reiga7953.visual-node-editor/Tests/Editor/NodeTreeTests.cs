using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>左のペインのノードツリー（階層の組み立てと、グラフとの行き来）。</summary>
    public sealed class NodeTreeTests
    {
        // ---- 階層 ----

        [Test]
        public void Build_NestsContainersAndOrdersEachLevel()
        {
            using var g = new ContainerTestGraph();

            var roots = NodeTree.Build(g.Asset);

            Assert.That(roots.Select(e => e.Label), Is.EqualTo(new[] { "Entry", "Stage", "Result" }));
            var stage = roots[1];
            Assert.That(stage.IsContainer, Is.True);
            Assert.That(stage.Children.Select(e => e.Label), Is.EqualTo(new[] { "Entry", "Play", "Inner", "Clear" }),
                "Entry first, then left to right and top to bottom, the Exit (named after its exit) last");
            var inner = stage.Children[2];
            Assert.That(inner.Children.Select(e => e.NodeId), Is.EqualTo(new[] { g.InnerEntry.Id, g.Boss.Id, g.InnerExit.Id }));
            Assert.That(inner.Children.Last().Label, Is.EqualTo("Next"));
        }

        [Test]
        public void Build_UsesTheSameNamesAsTheGraph()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.AddNode(new EntryNode());
                graph.AddNode(new EventNode { EventName = "StartGame", Position = new Vector2(100f, 0f) });
                graph.AddNode(new SceneNode { Scene = new SceneReference("aaaa", "Assets/Title.unity"), Position = new Vector2(200f, 0f) });

                var roots = NodeTree.Build(graph);

                Assert.That(roots.Select(e => e.Label), Is.EqualTo(new[] { "Entry", "StartGame", "Title" }));
                Assert.That(roots.Select(e => e.Category), Is.EqualTo(new[] { "flow", "event", "flow" }));
                Assert.That(roots[1].TypeName, Is.EqualTo("Event"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Build_LeavesOutNodesWithABrokenParent()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.AddNode(new EntryNode());
                graph.AddNode(new StateNode { Title = "Lost", ParentId = "missing-container" });

                Assert.That(NodeTree.Build(graph).Select(e => e.Label), Is.EqualTo(new[] { "Entry" }));
                Assert.That(NodeTree.Build(null), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        // ---- パネル ----

        [Test]
        public void Panel_ListsEveryNodeAndFollowsTheGraphSelection()
        {
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            string chosen = null;
            panel.NodeSelected += id => chosen = id;

            panel.Show(g.Asset);
            Assert.That(panel.NodeCount, Is.EqualTo(g.Asset.Nodes.Count));

            panel.Select(g.Boss.Id);
            Assert.That(panel.SelectedNodeId, Is.EqualTo(g.Boss.Id));
            Assert.That(chosen, Is.Null, "selecting from the graph does not move the graph again");

            panel.Select(null);
            Assert.That(panel.SelectedNodeId, Is.Null);
        }

        [Test]
        public void Panel_ClickMovesToTheNode_DoubleClickOpensContainers()
        {
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            string selected = null;
            string opened = null;
            panel.NodeSelected += id => selected = id;
            panel.NodeOpened += id => opened = id;
            panel.Show(g.Asset);

            panel.Choose(g.Boss.Id, open: false);
            Assert.That(selected, Is.EqualTo(g.Boss.Id));

            panel.Choose(g.Inner.Id, open: true);
            Assert.That(opened, Is.EqualTo(g.Inner.Id));

            selected = null;
            panel.Choose(g.Play.Id, open: true);
            Assert.That(selected, Is.EqualTo(g.Play.Id), "double-clicking a node that is not a container just moves to it");
        }

        [Test]
        public void Panel_HighlightsTheLevelAndTheRunningNode()
        {
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            panel.Show(g.Asset);

            panel.ShowLevel(g.Stage.Id);
            panel.ShowRunning(g.Boss.Id);

            Assert.That(panel.GetStateClasses(g.Stage.Id), Is.EquivalentTo(new[]
            {
                "vne-node-tree__item--container", "vne-node-tree__item--level", "vne-node-tree__item--running-path",
            }));
            Assert.That(panel.GetStateClasses(g.Inner.Id), Is.EquivalentTo(new[]
            {
                "vne-node-tree__item--container", "vne-node-tree__item--running-path",
            }));
            Assert.That(panel.GetStateClasses(g.Boss.Id), Is.EquivalentTo(new[] { "vne-node-tree__item--running" }));
            Assert.That(panel.GetStateClasses(g.Result.Id), Is.Empty);

            panel.ShowRunning(null);
            Assert.That(panel.GetStateClasses(g.Boss.Id), Is.Empty);
        }

        [Test]
        public void Panel_LeavesCollapsedContainersAlone_AndOpensNewOnes()
        {
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            panel.Show(g.Asset);
            panel.Select(g.Play.Id);
            panel.SetExpanded(g.Stage.Id, false);

            // 別のノードの改名で作り直しても、同じノードを選び直しても、閉じたコンテナは閉じたまま
            g.Result.Title = "Score";
            panel.Show(g.Asset);
            panel.Select(g.Play.Id);
            Assert.That(panel.IsExpanded(g.Stage.Id), Is.False);

            // 別のノードを選んだときは、見えるようにコンテナを開く
            panel.Select(g.Boss.Id);
            Assert.That(panel.IsExpanded(g.Stage.Id), Is.True);
            Assert.That(panel.IsExpanded(g.Inner.Id), Is.True);

            // 後から足したコンテナは開いた状態で出す
            var added = new ContainerNode { Title = "Bonus", Position = new Vector2(600f, 0f) };
            g.Asset.AddNode(added);
            g.Asset.AddNode(new StateNode { Title = "Extra", ParentId = added.Id });
            panel.Show(g.Asset);
            Assert.That(panel.IsExpanded(added.Id), Is.True);
        }

        [Test]
        public void Panel_KeepsTheSameItemsWhenNothingChanged()
        {
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            panel.Show(g.Asset);
            panel.Select(g.Play.Id);

            panel.Show(g.Asset);
            Assert.That(panel.SelectedNodeId, Is.EqualTo(g.Play.Id));

            g.Play.Title = "Playing";
            panel.Show(g.Asset);
            Assert.That(panel.SelectedNodeId, Is.EqualTo(g.Play.Id), "the selection survives a rebuild");
        }

        // ---- 閉じたコンテナの中の選択 ----

        [Test]
        public void Panel_ReselectsTheNodeWhenItsContainerOpens()
        {
            // 閉じたコンテナの中の選択中のノードは、作り直しで項目の選択が外れても、コンテナを開くと選ばれた状態に戻る
            using var g = new ContainerTestGraph();
            var panel = new NodeTreePanel();
            panel.Show(g.Asset);
            panel.Select(g.Play.Id);
            panel.SetExpanded(g.Stage.Id, false);
            g.Result.Title = "Score";
            panel.Show(g.Asset);
            Assert.That(panel.IsSelectionShown, Is.False, "hidden inside the collapsed container");

            panel.SetExpanded(g.Stage.Id, true);

            Assert.That(panel.IsSelectionShown, Is.True);
            Assert.That(panel.SelectedNodeId, Is.EqualTo(g.Play.Id));
        }
    }
}
