using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ファイルのセーブスロット（<see cref="GraphSaveSlots"/>）と、ボタンからのセーブ・ロード。</summary>
    public sealed class GraphSaveSlotsTests
    {
        private string _root;
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private StateNode _title;
        private StateNode _game;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "VneSaveSlots_" + Guid.NewGuid().ToString("N"));
            GraphSaveSlots.RootOverride = _root;
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Slot:Graph";
            _graph.AddParameter(new GraphParameter("Score", GraphParameterType.Int));
            var entry = new EntryNode();
            _title = new StateNode { Title = "Title" };
            _game = new StateNode { Title = "Game" };
            var start = new EventNode { EventName = "Start" };
            foreach (var node in new NodeData[] { entry, _title, start, _game })
            {
                _graph.AddNode(node);
            }

            _graph.AddEdge(new EdgeData(entry.Id, "out", _title.Id, "in"));
            _graph.AddEdge(new EdgeData(_title.Id, "out", start.Id, "in"));
            _graph.AddEdge(new EdgeData(start.Id, "out", _game.Id, "in"));
            _runner = new GraphRunner(_graph);
            _runner.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            GraphSaveSlots.RootOverride = null;
            Object.DestroyImmediate(_graph);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void SaveThenLoad_ResumesWhereItWas()
        {
            _runner.Raise("Start");
            _runner.SetInt("Score", 42);
            GraphSaveSlots.Save(_runner, "slot1");
            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.True);

            _runner.Stop();
            _runner.Start();
            Assert.That(_runner.Current, Is.SameAs(_title));

            Assert.That(GraphSaveSlots.Load(_runner, "slot1"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(_game));
            Assert.That(_runner.GetInt("Score"), Is.EqualTo(42));
        }

        [Test]
        public void Files_GoUnderTheGraphName()
        {
            var path = GraphSaveSlots.GetPath(_graph, "Quick Save");

            Assert.That(path, Does.StartWith(_root));
            Assert.That(Path.GetFileName(path), Is.EqualTo("Quick Save.json"));
            Assert.That(Path.GetFileName(Path.GetDirectoryName(path)), Is.EqualTo("Slot_Graph"), "':' cannot be in a folder name");
        }

        [Test]
        public void MissingSlot_LoadsNothing()
        {
            Assert.That(GraphSaveSlots.Exists(_graph, "empty"), Is.False);
            Assert.That(GraphSaveSlots.Read(_graph, "empty"), Is.Null);
            Assert.That(GraphSaveSlots.Load(_runner, "empty"), Is.False);
            Assert.That(GraphSaveSlots.Delete(_graph, "empty"), Is.False);
        }

        [Test]
        public void Delete_RemovesTheSlot()
        {
            GraphSaveSlots.Save(_runner, "slot1");

            Assert.That(GraphSaveSlots.Delete(_graph, "slot1"), Is.True);
            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.False);
        }

        [Test]
        public void SavingAgain_ReplacesTheSlot_AndLeavesNoTemporaryFile()
        {
            GraphSaveSlots.Save(_runner, "slot1");
            _runner.Raise("Start");
            _runner.SetInt("Score", 7);

            GraphSaveSlots.Save(_runner, "slot1");

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));
            var folder = Path.GetDirectoryName(GraphSaveSlots.GetPath(_graph, "slot1"));
            Assert.That(Directory.GetFiles(folder).Select(Path.GetFileName), Is.EqualTo(new[] { "slot1.json" }));
        }

        [TestCase("slot1", true)]
        [TestCase("Chapter 2-b_x", true)]
        [TestCase("", false)]
        [TestCase(" slot", false)]
        [TestCase("../escape", false)]
        [TestCase("a/b", false)]
        [TestCase("CON", false)]
        [TestCase("nul", false)]
        [TestCase("Com1", false)]
        [TestCase("LPT9", false)]
        [TestCase("Console", true)]
        [TestCase("COM10", true)]
        public void SlotNames_AreFileNameSafe(string slot, bool valid)
        {
            Assert.That(GraphSaveSlots.IsValidSlotName(slot), Is.EqualTo(valid));
            if (!valid)
            {
                Assert.Throws<ArgumentException>(() => GraphSaveSlots.GetPath(_graph, slot));
            }
        }

        // ---- ボタン ----

        [Test]
        public void Button_SavesAndLoads()
        {
            var gameObject = new GameObject("Save Button");
            try
            {
                var button = gameObject.AddComponent<GraphEventButton>();
                button.Graph = _graph;
                button.Action = GraphEventButtonAction.Save;
                button.Slot = "slot2";
                _runner.Raise("Start");

                Assert.That(button.TrySend(), Is.True);
                Assert.That(GraphSaveSlots.Exists(_graph, "slot2"), Is.True);

                _runner.Stop();
                _runner.Start();
                button.Action = GraphEventButtonAction.Load;
                Assert.That(button.TrySend(), Is.True);
                Assert.That(_runner.Current, Is.SameAs(_game));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ButtonHints_AskForAUsableSlotName()
        {
            var hints = GraphEventButtonHints.Get(null, string.Empty, GraphEventButtonAction.Load, false, false, slot: "a/b");

            Assert.That(hints, Has.Some.Contains("Name the slot"));
            Assert.That(GraphEventButtonHints.Get(null, string.Empty, GraphEventButtonAction.Save, false, false, slot: "slot1"), Is.Empty);
        }

        [Test]
        public void UnusableSlotNames_AreExplained_IncludingReservedNames()
        {
            // 文字は使えても Windows が予約している名前は断るので、そのことも伝える
            var error = Assert.Throws<ArgumentException>(() => GraphSaveSlots.GetPath(_graph, "CON"));
            Assert.That(error.Message, Does.Contain("CON, NUL or COM1"));

            var hints = GraphEventButtonHints.Get(null, string.Empty, GraphEventButtonAction.Save, false, false, slot: "nul");
            Assert.That(hints, Has.Some.Contains("Windows reserves"));
        }
    }
}
