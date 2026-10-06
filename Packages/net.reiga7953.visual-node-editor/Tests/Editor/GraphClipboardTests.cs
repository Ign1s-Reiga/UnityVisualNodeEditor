using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Clipboard;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphClipboardTests
    {
        private static readonly Vector2 Offset = new Vector2(30f, 30f);

        private NodeGraphAsset _graph;
        private EntryNode _entry;
        private EventNode _event;
        private StateNode _state;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _entry = new EntryNode { Position = new Vector2(0f, 0f) };
            _event = new EventNode { EventName = "OnBossDefeated", Title = "Boss", Position = new Vector2(200f, 0f) };
            _state = new StateNode { Position = new Vector2(400f, 0f) };
            _graph.AddNode(_entry);
            _graph.AddNode(_event);
            _graph.AddNode(_state);
            _graph.AddEdge(new EdgeData(_entry.Id, "out", _event.Id, "in"));
            _graph.AddEdge(new EdgeData(_event.Id, "out", _state.Id, "in"));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Paste_CreatesCopiesWithNewIdsAndOffsetPositions()
        {
            var data = GraphClipboard.Serialize(_graph, new[] { _event.Id }, null, null);

            var content = GraphClipboard.Deserialize(data, Offset);

            var copy = content.Nodes.Single();
            Assert.That(copy, Is.TypeOf<EventNode>(), "node type must survive SerializeReference round trip");
            Assert.That(copy, Is.Not.SameAs(_event));
            Assert.That(copy.Id, Is.Not.EqualTo(_event.Id));
            Assert.That(((EventNode)copy).EventName, Is.EqualTo("OnBossDefeated"));
            Assert.That(copy.Title, Is.EqualTo("Boss"));
            Assert.That(copy.Position, Is.EqualTo(_event.Position + Offset));
        }

        [Test]
        public void Paste_KeepsOnlyEdgesBetweenCopiedNodesAndRemapsThem()
        {
            var data = GraphClipboard.Serialize(_graph, new[] { _entry.Id, _event.Id }, null, null);

            var content = GraphClipboard.Deserialize(data, Offset);

            var edge = content.Edges.Single();
            var entryCopy = content.Nodes.OfType<EntryNode>().Single();
            var eventCopy = content.Nodes.OfType<EventNode>().Single();
            Assert.That(edge.FromNodeId, Is.EqualTo(entryCopy.Id));
            Assert.That(edge.ToNodeId, Is.EqualTo(eventCopy.Id));
            Assert.That(edge.FromPort, Is.EqualTo("out"));
            Assert.That(edge.ToPort, Is.EqualTo("in"));
        }

        [Test]
        public void CopyingGroup_IncludesItsNodesAndRemapsMembers()
        {
            var group = new GroupData { Title = "Boss Fight", Position = new Vector2(10f, 10f) };
            group.AddNode(_event.Id);
            group.AddNode(_state.Id);
            _graph.AddGroup(group);

            var data = GraphClipboard.Serialize(_graph, null, new[] { group.Id }, null);
            var content = GraphClipboard.Deserialize(data, Offset);

            var groupCopy = content.Groups.Single();
            Assert.That(groupCopy.Id, Is.Not.EqualTo(group.Id));
            Assert.That(groupCopy.Title, Is.EqualTo("Boss Fight"));
            Assert.That(groupCopy.Position, Is.EqualTo(group.Position + Offset));
            Assert.That(content.Nodes, Has.Count.EqualTo(2));
            Assert.That(groupCopy.NodeIds, Is.EquivalentTo(content.Nodes.Select(n => n.Id)));
            Assert.That(content.Edges, Has.Count.EqualTo(1), "edge between the two grouped nodes");
        }

        [Test]
        public void StickyNotes_AreCopiedWithNewIdsAndOffset()
        {
            var note = new StickyNoteData { Title = "TODO", Contents = "check", Rect = new Rect(5f, 5f, 120f, 80f) };
            _graph.AddStickyNote(note);

            var data = GraphClipboard.Serialize(_graph, null, null, new[] { note.Id });
            var copy = GraphClipboard.Deserialize(data, Offset).StickyNotes.Single();

            Assert.That(copy.Id, Is.Not.EqualTo(note.Id));
            Assert.That(copy.Contents, Is.EqualTo("check"));
            Assert.That(copy.Rect, Is.EqualTo(new Rect(35f, 35f, 120f, 80f)));
        }

        [Test]
        public void PastingTwice_GivesDistinctIds()
        {
            var data = GraphClipboard.Serialize(_graph, new[] { _state.Id }, null, null);

            var first = GraphClipboard.Deserialize(data, Offset).Nodes.Single();
            var second = GraphClipboard.Deserialize(data, Offset * 2).Nodes.Single();

            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(second.Position, Is.EqualTo(_state.Position + Offset * 2));
        }

        [Test]
        public void Serialize_DoesNotChangeTheSourceGraph()
        {
            var ids = _graph.Nodes.Select(n => n.Id).ToList();

            GraphClipboard.Serialize(_graph, ids, null, null);

            Assert.That(_graph.Nodes.Select(n => n.Id), Is.EqualTo(ids));
            Assert.That(_graph.Edges, Has.Count.EqualTo(2));
        }

        [Test]
        public void NothingSelected_SerializesToEmpty()
        {
            Assert.That(GraphClipboard.Serialize(_graph, new string[0], null, null), Is.Empty);
            Assert.That(GraphClipboard.Serialize(_graph, new[] { "missing" }, null, null), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("hello")]
        [TestCase("{\"MonoBehaviour\":{}}")]
        public void ForeignText_CannotBePasted(string data)
        {
            Assert.That(GraphClipboard.CanPaste(data), Is.False);
            Assert.That(GraphClipboard.Deserialize(data, Offset), Is.Null);
        }
    }
}
