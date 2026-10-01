using System;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeViewFactoryTests
    {
        [TestCase(typeof(EntryNode), typeof(EntryNodeView))]
        [TestCase(typeof(SceneNode), typeof(SceneNodeView))]
        [TestCase(typeof(StateNode), typeof(StateNodeView))]
        [TestCase(typeof(EventNode), typeof(EventNodeView))]
        [TestCase(typeof(NoteNode), typeof(NodeView))]
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
