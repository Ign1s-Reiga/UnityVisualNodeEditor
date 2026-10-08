using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>コンテナ・コンテナの Entry / Exit の View と、階層ごとの Create Node メニュー。</summary>
    public sealed class ContainerViewTests
    {
        private ContainerTestGraph _g;

        [SetUp]
        public void SetUp() => _g = new ContainerTestGraph();

        [TearDown]
        public void TearDown() => _g.Dispose();

        [Test]
        public void ContainerView_HasAnInputAndOneOutputPerExitInOrder()
        {
            var view = NodeViewFactory.Create(_g.Stage, _g.Asset);

            Assert.That(view, Is.TypeOf<ContainerNodeView>(), "subclasses of ContainerNode use the container view");
            Assert.That(view.FindPort(ContainerNode.InputPortId, Direction.Input), Is.Not.Null);
            var outputs = view.outputContainer.Query<Port>().ToList();
            Assert.That(outputs.Select(NodeView.GetPortId), Is.EqualTo(_g.Stage.Exits.Select(e => e.Id)), "port id = exit id");
            Assert.That(outputs.Select(p => p.portName), Is.EqualTo(new[] { "Clear", "GameOver" }), "label = exit name");
        }

        [Test]
        public void ContainerView_ShowsExitNamesEvenWithASingleExit()
        {
            var view = NodeViewFactory.Create(_g.Inner, _g.Asset);

            Assert.That(view.PortLabelsHidden, Is.False);
            Assert.That(NodeViewFactory.Create(_g.Play, _g.Asset).PortLabelsHidden, Is.True, "ordinary nodes still hide in / out");
        }

        [Test]
        public void ContainerView_SummaryCountsTheContentsWithoutEntryAndExit()
        {
            Assert.That(NodeViewFactory.Create(_g.Stage, _g.Asset).Summary, Is.EqualTo("2 nodes"), "Play and Inner");
            Assert.That(NodeViewFactory.Create(_g.Inner, _g.Asset).Summary, Is.EqualTo("1 node"));
        }

        [TestCase(0, "Empty")]
        [TestCase(1, "1 node")]
        [TestCase(5, "5 nodes")]
        public void FormatChildCount(int count, string expected)
        {
            Assert.That(ContainerNodeView.FormatChildCount(count), Is.EqualTo(expected));
        }

        [Test]
        public void ContainerEntryView_CannotBeDeletedOrCopiedOnItsOwn()
        {
            var view = NodeViewFactory.Create(_g.StageEntry, _g.Asset);

            Assert.That(view, Is.TypeOf<ContainerEntryNodeView>());
            Assert.That(view.capabilities & Capabilities.Deletable, Is.EqualTo((Capabilities)0));
            Assert.That(view.capabilities & Capabilities.Copiable, Is.EqualTo((Capabilities)0));
            Assert.That(view.capabilities & Capabilities.Movable, Is.EqualTo(Capabilities.Movable), "can still be moved");
            Assert.That(view.inputContainer.Query<Port>().ToList(), Is.Empty);
            Assert.That(view.FindPort(NodeView.OutputPortName, Direction.Output), Is.Not.Null);
        }

        [Test]
        public void ContainerExitView_TitleFollowsTheExitName()
        {
            var view = NodeViewFactory.Create(_g.ClearExit, _g.Asset);
            Assert.That(view, Is.TypeOf<ContainerExitNodeView>());
            Assert.That(view.title, Is.EqualTo("Clear"));
            Assert.That(view.outputContainer.Query<Port>().ToList(), Is.Empty);

            _g.Stage.RenameExit(_g.ClearExit.ExitId, "Victory");
            view.Rebind(_g.ClearExit);

            Assert.That(view.title, Is.EqualTo("Victory"));
            Assert.That(view.Summary, Is.Empty);
        }

        [Test]
        public void ContainerExitView_WithAUserTitle_ShowsTheExitInTheSummary()
        {
            _g.ClearExit.Title = "Goal";

            var view = NodeViewFactory.Create(_g.ClearExit, _g.Asset);

            Assert.That(view.title, Is.EqualTo("Goal"));
            Assert.That(view.Summary, Is.EqualTo("Exit: Clear"));
        }

        [Test]
        public void ContainerExitView_WithAMissingExit_SaysSo()
        {
            _g.ClearExit.ExitId = "removed";

            var view = NodeViewFactory.Create(_g.ClearExit, _g.Asset);

            Assert.That(view.Summary, Is.EqualTo(ContainerExitNodeView.MissingExitSummary));
        }

        [Test]
        public void ContainerEntryAndExit_UseTheContainerCategory()
        {
            Assert.That(NodeViewFactory.Create(_g.StageEntry, _g.Asset).ClassListContains("vne-node--container"), Is.True);
            Assert.That(NodeViewFactory.Create(_g.ClearExit, _g.Asset).ClassListContains("vne-node--container"), Is.True);
            Assert.That(NodeViewFactory.Create(_g.Inner, _g.Asset).ClassListContains("vne-node--flow"), Is.True,
                "the container itself is a Flow node ([NodeMenu] is not inherited, so subclasses pick their own)");
        }

        [Test]
        public void Menu_RootEntryOnlyAtTheRoot_ContainerExitOnlyInside()
        {
            var atRoot = NodeMenuCatalog.GetItems(insideContainer: false).Select(i => i.NodeType).ToList();
            var inside = NodeMenuCatalog.GetItems(insideContainer: true).Select(i => i.NodeType).ToList();

            Assert.That(atRoot, Has.Member(typeof(EntryNode)));
            Assert.That(atRoot, Has.No.Member(typeof(ContainerExitNode)));
            Assert.That(inside, Has.No.Member(typeof(EntryNode)));
            Assert.That(inside, Has.Member(typeof(ContainerExitNode)));
            Assert.That(atRoot, Has.Member(typeof(ContainerNode)));
            Assert.That(inside, Has.Member(typeof(ContainerNode)), "containers can be nested");
            Assert.That(atRoot.Concat(inside), Has.No.Member(typeof(ContainerEntryNode)), "created with its container");
        }
    }
}
