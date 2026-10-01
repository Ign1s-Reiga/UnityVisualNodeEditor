using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Search;
using UnityEditor.Experimental.GraphView;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeSearchTreeTests
    {
        [Test]
        public void Catalog_ContainsBuiltInNodesInPathOrder()
        {
            var items = NodeMenuCatalog.GetItems();
            var paths = items.Select(i => i.Path).ToList();

            Assert.That(paths, Is.SupersetOf(new[] { "Flow/Entry", "Flow/Event", "Flow/Scene", "Flow/State", "Misc/Note" }));
            Assert.That(paths, Is.Ordered.Using<string>(System.StringComparer.Ordinal));
            Assert.That(items.Single(i => i.Path == "Flow/Scene").NodeType, Is.EqualTo(typeof(SceneNode)));
        }

        [Test]
        public void Catalog_ExcludesAbstractTypes()
        {
            Assert.That(NodeMenuCatalog.IsCreatable(typeof(NodeData)), Is.False);
            Assert.That(NodeMenuCatalog.IsCreatable(typeof(SceneNode)), Is.True);
            Assert.That(NodeMenuCatalog.IsCreatable(typeof(string)), Is.False);
        }

        [Test]
        public void BuildSearchTree_NestsGroupsByPath()
        {
            var items = new[]
            {
                new NodeMenuItem("Flow/Entry", typeof(EntryNode)),
                new NodeMenuItem("Flow/Scene", typeof(SceneNode)),
                new NodeMenuItem("Flow/Sub/State", typeof(StateNode)),
                new NodeMenuItem("Misc/Note", typeof(NoteNode)),
                new NodeMenuItem("Top", typeof(EventNode)),
            };

            var tree = NodeSearchWindow.BuildSearchTree(items, null);

            var actual = tree.Select(e => (e is SearchTreeGroupEntry ? "G:" : "") + e.name + "@" + e.level).ToArray();
            Assert.That(actual, Is.EqualTo(new[]
            {
                "G:Create Node@0",
                "G:Flow@1",
                "Entry@2",
                "Scene@2",
                "G:Sub@2",
                "State@3",
                "G:Misc@1",
                "Note@2",
                "Top@1",
            }));
            Assert.That(tree.Last().userData, Is.EqualTo(typeof(EventNode)));
        }
    }
}
