using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeCollapseTests
    {
        private NodeGraphAsset _graph;
        private StateNode _a;
        private StateNode _b;
        private NodeGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _a = new StateNode { Description = "first" };
            _b = new StateNode { Collapsed = true };
            _graph.AddNode(_a);
            _graph.AddNode(_b);
            _view = new NodeGraphView();
            _view.Populate(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void SavedCollapsedState_IsShownWhenOpened()
        {
            Assert.That(_view.FindNodeView(_a.Id).IsCollapsed, Is.False);
            Assert.That(_view.FindNodeView(_b.Id).IsCollapsed, Is.True);
            Assert.That(_view.FindNodeView(_b.Id).ClassListContains("vne-node--collapsed"), Is.True);
        }

        [Test]
        public void CollapseAll_WithoutSelection_AffectsEveryNodeAndIsSaved()
        {
            _view.SetCollapsedForSelectionOrAll(true);

            Assert.That(_a.Collapsed && _b.Collapsed, Is.True);
            Assert.That(_view.FindNodeView(_a.Id).IsCollapsed, Is.True);

            _view.SetCollapsedForSelectionOrAll(false);

            Assert.That(_a.Collapsed || _b.Collapsed, Is.False);
            Assert.That(_view.FindNodeView(_b.Id).ClassListContains("vne-node--collapsed"), Is.False);
        }

        [Test]
        public void CollapseAll_WithSelection_OnlyAffectsSelectedNodes()
        {
            _view.AddToSelection(_view.FindNodeView(_a.Id));

            _view.SetCollapsedForSelectionOrAll(true);

            Assert.That(_a.Collapsed, Is.True);
            Assert.That(_b.Collapsed, Is.True, "was already collapsed and is not touched");

            _view.SetCollapsedForSelectionOrAll(false);

            Assert.That(_a.Collapsed, Is.False);
            Assert.That(_b.Collapsed, Is.True);
        }
    }
}
