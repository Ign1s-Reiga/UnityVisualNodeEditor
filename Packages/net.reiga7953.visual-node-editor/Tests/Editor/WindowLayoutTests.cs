using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ウィンドウの UXML の並び（どこに何を置くか）。見た目は対象外。</summary>
    public sealed class WindowLayoutTests
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            var uxml = Resources.Load<VisualTreeAsset>("VisualNodeEditor/NodeGraphEditorWindow");
            Assert.That(uxml, Is.Not.Null, "the window UXML imports");
            _root = new VisualElement();
            uxml.CloneTree(_root);
        }

        [Test]
        public void Breadcrumbs_SitBelowTheGraph()
        {
            var graphArea = _root.Q("graph-area");
            var graph = _root.Q("graph-container");
            var levelBar = _root.Q("level-bar");

            Assert.That(graph.parent, Is.SameAs(graphArea));
            Assert.That(levelBar.parent, Is.SameAs(graphArea));
            Assert.That(graphArea.IndexOf(levelBar), Is.GreaterThan(graphArea.IndexOf(graph)));
        }

        [Test]
        public void NodeTree_IsTheLeftPane()
        {
            var body = _root.Q<TwoPaneSplitView>("body");

            Assert.That(body.fixedPaneIndex, Is.EqualTo(0), "the tree keeps its width when the window is resized");
            Assert.That(body.contentContainer.IndexOf(_root.Q("node-tree-pane")), Is.EqualTo(0));
            Assert.That(body.contentContainer.IndexOf(_root.Q("main")), Is.EqualTo(1));
        }
    }
}
