using System;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeViewFactoryTests
    {
        [TestCase(typeof(EntryNode), typeof(EntryNodeView))]
        [TestCase(typeof(SceneNode), typeof(SceneNodeView))]
        [TestCase(typeof(StateNode), typeof(StateNodeView))]
        [TestCase(typeof(EventNode), typeof(EventNodeView))]
        [TestCase(typeof(NoteNode), typeof(NoteNodeView))]
        [TestCase(typeof(UnregisteredNode), typeof(NodeView))]
        public void ResolveViewType_ReturnsRegisteredViewOrFallback(Type nodeType, Type expectedViewType)
        {
            Assert.That(NodeViewFactory.ResolveViewType(nodeType), Is.EqualTo(expectedViewType));
        }

        [Test]
        public void Create_BindsData()
        {
            var data = new SceneNode { Title = "Title Scene" };

            var view = NodeViewFactory.Create(data);

            Assert.That(view, Is.TypeOf<SceneNodeView>());
            Assert.That(view.Data, Is.SameAs(data));
            Assert.That(view.title, Is.EqualTo("Title Scene"));
            Assert.That(view.viewDataKey, Is.EqualTo(data.Id));
        }

        [Test]
        public void Create_EntryHasOnlyOutputPort()
        {
            var view = NodeViewFactory.Create(new EntryNode());

            Assert.That(view.FindPort(NodeView.OutputPortName, Direction.Output), Is.Not.Null);
            Assert.That(view.FindPort(NodeView.InputPortName, Direction.Input), Is.Null);
        }

        [Test]
        public void Create_SceneHasInputAndOutputPorts()
        {
            var view = NodeViewFactory.Create(new SceneNode());

            Assert.That(view.FindPort(NodeView.InputPortName, Direction.Input), Is.Not.Null);
            Assert.That(view.FindPort(NodeView.OutputPortName, Direction.Output), Is.Not.Null);
        }

        [Test]
        public void Create_NoteHasNoPorts()
        {
            var view = NodeViewFactory.Create(new NoteNode());

            Assert.That(view.FindPort(NodeView.InputPortName, Direction.Input), Is.Null);
            Assert.That(view.FindPort(NodeView.OutputPortName, Direction.Output), Is.Null);
        }

        [TestCase(0, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(0, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(1, 2, false)]
        public void ShouldHidePortLabels_OnlyWithAtMostOnePortEachSide(int inputs, int outputs, bool expected)
        {
            Assert.That(NodeView.ShouldHidePortLabels(inputs, outputs), Is.EqualTo(expected));
        }

        [Test]
        public void Create_BuiltInNodesHidePortLabelsAndHaveCategoryClass()
        {
            var entry = NodeViewFactory.Create(new EntryNode());
            var evt = NodeViewFactory.Create(new EventNode());

            Assert.That(entry.ClassListContains("vne-node--hide-port-labels"), Is.True);
            Assert.That(entry.ClassListContains("vne-node--flow"), Is.True);
            Assert.That(evt.ClassListContains("vne-node--event"), Is.True);
        }

        [Test]
        public void Create_EventReadsAsATransition()
        {
            // タイトルを付けていなければイベント名がタイトル（2 行目には出さない）。札の形の USS クラスが付く
            var view = NodeViewFactory.Create(new EventNode { EventName = "OnBossDefeated" });

            Assert.That(view.title, Is.EqualTo("OnBossDefeated"));
            Assert.That(view.Summary, Is.Empty);
            Assert.That(view.ClassListContains(EventNodeView.TransitionClassName), Is.True);
            Assert.That(NodeViewFactory.Create(new EventNode()).title, Is.EqualTo("Event"), "no event name yet");
        }

        [Test]
        public void Create_TitledEventShowsEventNameAsSummary()
        {
            var view = NodeViewFactory.Create(new EventNode { Title = "Boss down", EventName = "OnBossDefeated" });

            Assert.That(view.title, Is.EqualTo("Boss down"));
            Assert.That(view.Summary, Is.EqualTo("OnBossDefeated"));
        }

        [Test]
        public void NodeLabel_FollowsTheTitleRules()
        {
            Assert.That(NodeDisplay.GetNodeLabel(new EventNode { EventName = "Finish" }), Is.EqualTo("Finish"));
            Assert.That(NodeDisplay.GetNodeLabel(new EventNode { EventName = "Finish ", Title = "" }), Is.EqualTo("Finish "),
                "the exact name Raise compares, so a stray space stays visible");
            Assert.That(NodeDisplay.GetNodeLabel(new EventNode { EventName = "  " }), Is.EqualTo("Event"));
            Assert.That(NodeDisplay.GetNodeLabel(new EventNode { Title = "Boss down", EventName = "Finish" }), Is.EqualTo("Boss down"));
            Assert.That(NodeDisplay.GetNodeLabel(new StateNode()), Is.EqualTo("State"));
        }

        [Test]
        public void Create_EmptySummaryIsNotRendered()
        {
            var view = NodeViewFactory.Create(new EntryNode());

            Assert.That(view.Summary, Is.Empty);
            Assert.That(view.Q(className: "vne-node__summary"), Is.Null);
        }

        [Test]
        public void Rebind_UpdatesTitleAndSummary()
        {
            var data = new NoteNode();
            var view = NodeViewFactory.Create(data);
            Assert.That(view.title, Is.EqualTo("Note"));

            data.Title = "Memo";
            data.Text = "\n  first line \nsecond line";
            view.Rebind(data);

            Assert.That(view.title, Is.EqualTo("Memo"));
            Assert.That(view.Summary, Is.EqualTo("first line"));
        }

        [Test]
        public void Create_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => NodeViewFactory.Create(null));
        }

        /// <summary>View が登録されていないノード型（メニューにも出さない）。</summary>
        [Serializable]
        private sealed class UnregisteredNode : NodeData
        {
            protected override string DefaultTitle => "Unregistered";
        }
    }
}
