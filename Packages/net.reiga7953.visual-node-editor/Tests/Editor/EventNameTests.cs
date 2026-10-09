using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>イベント名を打ち直させない仕組み（既存の名前から選ぶ・表記ゆれの警告・Raise の呼び出しのコピー）。</summary>
    public sealed class EventNameTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.AddNode(new EntryNode());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 表記ゆれ ----

        [Test]
        public void NearDuplicates_DifferOnlyInCaseOrSpaces()
        {
            var near = EventNameCheck.FindNearDuplicates(new[] { "StartGame", "Pause", "startgame", "StartGame", " Pause", "", null });

            Assert.That(near, Is.EqualTo(new[] { ("startgame", "StartGame"), (" Pause", "Pause") }),
                "the exact same name repeated is fine");
            Assert.That(EventNameCheck.Normalize("  Start Game "), Is.EqualTo("start game"));
            Assert.That(EventNameCheck.HasSurroundingSpaces("Retry "), Is.True);
            Assert.That(EventNameCheck.HasSurroundingSpaces("Re try"), Is.False);
            Assert.That(EventNameCheck.HasSurroundingSpaces(null), Is.False);
        }

        [Test]
        public void Validation_WarnsAboutNearDuplicatesAndSpaces()
        {
            var start = Add(new EventNode { EventName = "StartGame" });
            var typo = Add(new EventNode { EventName = "startGame" });
            var again = Add(new EventNode { EventName = "StartGame" });
            var spaced = Add(new EventNode { EventName = "Retry " });

            var issues = GraphValidator.Validate(_graph).Where(i => i.Severity == GraphIssueSeverity.Warning).ToList();

            Assert.That(issues.Where(i => i.NodeId == typo.Id).Select(i => i.Message), Has.Some.Matches<string>(m => m.Contains("differs from \"StartGame\"")));
            Assert.That(issues.Where(i => i.NodeId == spaced.Id).Select(i => i.Message), Has.Some.Matches<string>(m => m.Contains("spaces at the start or end")));
            Assert.That(issues.Any(i => i.NodeId == start.Id || i.NodeId == again.Id), Is.False);
        }

        // ---- Raise の呼び出しのコピー ----

        [TestCase("StartGame", "Raise(\"StartGame\")")]
        [TestCase("Say \"Hi\"", "Raise(\"Say \\\"Hi\\\"\")")]
        [TestCase("a\\b", "Raise(\"a\\\\b\")")]
        [TestCase(null, "Raise(\"\")")]
        public void RaiseCall_IsAValidCSharpCall(string eventName, string expected)
        {
            Assert.That(EventNameTools.FormatRaiseCall(eventName), Is.EqualTo(expected));
        }

        // ---- 既存の名前から選ぶ ----

        [Test]
        public void Inspector_OffersTheOtherEventNamesOfTheGraph()
        {
            Add(new EventNode { EventName = "StartGame" });
            Add(new EventNode { EventName = "Pause" });
            var node = Add(new EventNode { EventName = "" });
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, node.Id);
            var field = inspector.Q<EventNameField>();

            Assert.That(field, Is.Not.Null);
            Assert.That(field.Choices, Is.EqualTo(new[] { "StartGame", "Pause" }));

            field.Pick("Pause");

            Assert.That(((EventNode)_graph.FindNode(node.Id)).EventName, Is.EqualTo("Pause"));
            Assert.That(field.Choices, Is.EqualTo(new[] { "StartGame" }), "the current name is not offered");
        }

        [Test]
        public void EventNameFieldIsReachable()
        {
            Add(new EventNode());
            var serializedObject = new SerializedObject(_graph);
            var node = serializedObject.FindProperty(NodeInspectorView.NodesPropertyName).GetArrayElementAtIndex(1);

            Assert.That(node.FindPropertyRelative(EventNameField.EventNamePropertyName), Is.Not.Null);
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }
    }
}
