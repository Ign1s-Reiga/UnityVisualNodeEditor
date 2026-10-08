using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeGraphAssetInspectorTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var a = new StateNode();
            var b = new StateNode();
            _graph.AddNode(a);
            _graph.AddNode(b);
            _graph.AddEdge(new EdgeData(a.Id, "out", b.Id, "in"));
            _graph.AddStickyNote(new StickyNoteData());
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int) { IntValue = 3 });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Counts_SkipUnloadedNodes()
        {
            var counts = GraphSummary.GetCounts(_graph).ToDictionary(c => c.Label, c => c.Count);

            Assert.That(counts["Nodes"], Is.EqualTo(2));
            Assert.That(counts["Edges"], Is.EqualTo(1));
            Assert.That(counts["Groups"], Is.Zero);
            Assert.That(counts["Sticky Notes"], Is.EqualTo(1));
            Assert.That(counts["Parameters"], Is.EqualTo(1));
        }

        [Test]
        public void Parameters_AreDescribedWithTypeAndDefault()
        {
            Assert.That(GraphSummary.DescribeParameter(new GraphParameter("Lives", GraphParameterType.Int) { IntValue = 3 }),
                Is.EqualTo("Lives : Int = 3"));
            Assert.That(GraphSummary.DescribeParameter(new GraphParameter("Hard", GraphParameterType.Bool) { BoolValue = true }),
                Is.EqualTo("Hard : Bool = true"));
            Assert.That(GraphSummary.DescribeParameter(new GraphParameter("Speed", GraphParameterType.Float) { FloatValue = 1.25f }),
                Is.EqualTo("Speed : Float = 1.25"));
            Assert.That(GraphSummary.DescribeParameter(new GraphParameter("Player", GraphParameterType.String) { StringValue = "Ann" }),
                Is.EqualTo("Player : String = \"Ann\""));
            Assert.That(GraphSummary.DescribeParameter(new GraphParameter(" ", GraphParameterType.Int)),
                Does.StartWith("(no name)"));
        }

        [Test]
        public void Inspector_ShowsSummaryButNoEditableRawData()
        {
            var editor = UnityEditor.Editor.CreateEditor(_graph);
            try
            {
                Assert.That(editor, Is.TypeOf<NodeGraphAssetEditor>());
                var root = editor.CreateInspectorGUI();

                // 生のリストを編集できる要素が無いこと（「+」で ID ごと複製されるのを防ぐ）
                Assert.That(root.Query<PropertyField>().ToList(), Is.Empty);
                Assert.That(root.Query<ListView>().ToList(), Is.Empty);
                Assert.That(root.Q<Button>("open-button"), Is.Not.Null);

                var texts = root.Query<Label>().ToList().Select(l => l.text).ToList();
                Assert.That(texts, Does.Contain("Nodes: 2"));
                Assert.That(texts, Does.Contain("Lives : Int = 3"));
                Assert.That(root.Q<Label>("issues").text, Is.EqualTo("1 error"), "no Entry node");
                Assert.That(root.Q<Label>("issues").ClassListContains("vne-asset-inspector__issues--error"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }
    }
}
