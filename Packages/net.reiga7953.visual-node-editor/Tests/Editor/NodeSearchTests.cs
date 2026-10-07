using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeSearchTests
    {
        [TestCase("boss", "Boss Fight", "State", true)]
        [TestCase("  FIGHT ", "Boss Fight", "State", true)]
        [TestCase("event", "OnBossDefeated", "Event", true)]
        [TestCase("scene", "Boss Fight", "State", false)]
        [TestCase("", "Boss Fight", "State", false)]
        [TestCase("   ", "Boss Fight", "State", false)]
        [TestCase(null, "Boss Fight", "State", false)]
        public void Matches_TitleOrTypeIgnoringCase(string query, string title, string typeName, bool expected)
        {
            Assert.That(NodeSearch.Matches(query, title, typeName), Is.EqualTo(expected));
        }

        [TestCase(-1, 3, 0)]
        [TestCase(0, 3, 1)]
        [TestCase(2, 3, 0)]
        [TestCase(5, 3, 0)]
        [TestCase(-1, 0, -1)]
        public void NextIndex_CyclesThroughMatches(int current, int count, int expected)
        {
            Assert.That(NodeSearch.NextIndex(current, count), Is.EqualTo(expected));
        }

        [TestCase("", 3, -1, "")]
        [TestCase("x", 0, -1, "No matches")]
        [TestCase("x", 1, -1, "1 match")]
        [TestCase("x", 4, -1, "4 matches")]
        [TestCase("x", 4, 1, "2 / 4")]
        public void CountText(string query, int count, int index, string expected)
        {
            Assert.That(NodeSearch.GetCountText(query, count, index), Is.EqualTo(expected));
        }

        [Test]
        public void GraphView_HighlightsMatchesInAssetOrderAndKeepsThemAfterRebuild()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                var title = new SceneNode { Title = "Title Screen" };
                var boss = new StateNode { Title = "Boss" };
                var credits = new SceneNode { Title = "Credits" };
                graph.AddNode(title);
                graph.AddNode(boss);
                graph.AddNode(credits);
                var view = new NodeGraphView();
                view.Populate(graph);

                var matches = view.ApplySearch("scene");

                Assert.That(matches, Is.EqualTo(new[] { title.Id, credits.Id }), "type name matches, in asset order");
                Assert.That(view.FindNodeView(boss.Id).ClassListContains("vne-node--search-dimmed"), Is.True);
                Assert.That(view.FindNodeView(credits.Id).ClassListContains("vne-node--search-match"), Is.True);

                view.Populate(graph);
                Assert.That(view.FindNodeView(boss.Id).ClassListContains("vne-node--search-dimmed"), Is.True,
                    "search result survives a rebuild");

                view.ApplySearch(string.Empty);
                Assert.That(view.FindNodeView(boss.Id).ClassListContains("vne-node--search-dimmed"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }
    }
}
