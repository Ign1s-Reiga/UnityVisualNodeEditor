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

        [TestCase(typeof(PlatformNotSupportedException))]
        [TestCase(typeof(IOException))]
        public void SavingAgain_WhereFileReplaceFails_ReplacesThroughABackup(Type failure)
        {
            // File.Replace に対応しないプラットフォーム・ドライブ。古いセーブを .bak に退けてから移す
            GraphSaveSlots.Save(_runner, "slot1");
            _runner.Raise("Start");

            WithReplaceFile((_, _) => throw (Exception)Activator.CreateInstance(failure), () => GraphSaveSlots.Save(_runner, "slot1"));

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));
            Assert.That(FilesInSlotFolder(), Is.EqualTo(new[] { "slot1.json" }));
        }

        [Test]
        public void SavingAgain_WhileTheSlotIsLocked_FailsAndKeepsTheOldSave()
        {
            // ほかのプロセス（ウイルス対策ソフトなど）が開いている間は、置き換えも .bak に退けるのもできない。古いセーブを残して失敗にし、一時ファイルも残さない
            RequireFileLocks();
            GraphSaveSlots.Save(_runner, "slot1");
            _runner.Raise("Start");

            using (File.Open(GraphSaveSlots.GetPath(_graph, "slot1"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(() => GraphSaveSlots.Save(_runner, "slot1"),
                    Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
            }

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_title.Id));
            Assert.That(FilesInSlotFolder(), Is.EqualTo(new[] { "slot1.json" }), "no slot1.json.tmp is left behind");
        }

        [Test]
        public void ASaveInterruptedHalfway_IsFoundInTheBackup_AndTheNextSaveCleansUp()
        {
            // .bak に退けた後、新しいセーブを移す前に落ちた
            GraphSaveSlots.Save(_runner, "slot1");
            var path = GraphSaveSlots.GetPath(_graph, "slot1");
            File.Move(path, path + ".bak");

            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.True);
            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_title.Id));

            _runner.Raise("Start");
            GraphSaveSlots.Save(_runner, "slot1");
            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));
            Assert.That(FilesInSlotFolder(), Is.EqualTo(new[] { "slot1.json" }));
        }

        [Test]
        public void Delete_AlsoRemovesALeftoverBackup()
        {
            GraphSaveSlots.Save(_runner, "slot1");
            var path = GraphSaveSlots.GetPath(_graph, "slot1");
            File.Copy(path, path + ".bak");

            Assert.That(GraphSaveSlots.Delete(_graph, "slot1"), Is.True);

            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.False);
            Assert.That(FilesInSlotFolder(), Is.Empty);
        }

        [Test]
        public void SavingAgain_WhenReplacingFailsHalfway_StillFillsTheSlot()
        {
            // Windows の ReplaceFile は、古いファイルを別の名前に退けた後で失敗することがある
            GraphSaveSlots.Save(_runner, "slot1");
            _runner.Raise("Start");

            WithReplaceFile((_, destination) =>
            {
                File.Delete(destination);
                throw new IOException("Unable to move the replacement file.");
            }, () => GraphSaveSlots.Save(_runner, "slot1"));

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));
        }

        [Test]
        public void SavingAgain_WhenTheOldSaveIsGoneAndMovingFails_KeepsTheNewSaveReadable()
        {
            // 古いセーブが退けられた後で、新しいセーブを移すのにも失敗した（ここでは、スロットの名前のフォルダがあって移せない）。
            // 一時ファイルが新しいセーブの唯一の写しなので、消さずに残し、スロットとして読む。消すときは一緒に消す
            GraphSaveSlots.Save(_runner, "slot1");
            _runner.Raise("Start");
            var path = GraphSaveSlots.GetPath(_graph, "slot1");

            WithReplaceFile((_, destination) =>
            {
                File.Delete(destination);
                Directory.CreateDirectory(destination);
                throw new IOException("Unable to move the replacement file.");
            }, () => Assert.Throws<IOException>(() => GraphSaveSlots.Save(_runner, "slot1")));

            Assert.That(File.Exists(path + ".tmp"), Is.True);
            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.True);
            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));

            Assert.That(GraphSaveSlots.Delete(_graph, "slot1"), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.False);
        }

        [Test]
        public void ATemporaryFileCutOffMidWrite_IsNotASave_AndDeleteRemovesIt()
        {
            // 書いている途中で落ちた一時ファイルは読めないので、スロットとしては扱わない。それでも Delete は消す
            var path = GraphSaveSlots.GetPath(_graph, "slot1");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", "{\"_version\":1,\"_nod");

            Assert.That(GraphSaveSlots.Exists(_graph, "slot1"), Is.False);
            Assert.That(GraphSaveSlots.Read(_graph, "slot1"), Is.Null);

            Assert.That(GraphSaveSlots.Delete(_graph, "slot1"), Is.False, "there was no save");
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void ASaveKeptInTheTemporaryFile_IsMovedIntoTheSlotBeforeTheNextSave()
        {
            // 前の保存の失敗で、セーブの唯一の写しが .tmp に残っている（スロットのファイルは無い）
            _runner.Raise("Start");
            GraphSaveSlots.Save(_runner, "slot1");
            var path = GraphSaveSlots.GetPath(_graph, "slot1");
            File.Move(path, path + ".tmp");

            // 次の保存が失敗しても、その写しは消えない（上書きする前にスロットへ移す）
            _runner.SetInt("Score", 9);
            WithReplaceFile((_, _) => throw new UnauthorizedAccessException("Access denied."), () =>
                Assert.Throws<UnauthorizedAccessException>(() => GraphSaveSlots.Save(_runner, "slot1")));

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").Parameters[0].Value, Is.EqualTo(0), "the kept save, not the failed one");
            Assert.That(FilesInSlotFolder(), Is.EqualTo(new[] { "slot1.json" }));
        }

        [Test]
        public void ANewerTemporaryFile_WinsOverAnOlderBackup()
        {
            // 古いセーブを .bak に退けた後、新しいセーブを移すのにも、古いセーブを戻すのにも失敗した。新しいほう（.tmp）を読む
            GraphSaveSlots.Save(_runner, "slot1");
            var path = GraphSaveSlots.GetPath(_graph, "slot1");
            File.Move(path, path + ".bak");
            _runner.Raise("Start");
            GraphSaveSlots.Save(_runner, "slot1");
            File.Move(path, path + ".tmp");

            Assert.That(GraphSaveSlots.Read(_graph, "slot1").NodeId, Is.EqualTo(_game.Id));
        }

        private static void WithReplaceFile(Action<string, string> replace, Action action)
        {
            var original = GraphSaveSlots.ReplaceFile;
            GraphSaveSlots.ReplaceFile = replace;
            try
            {
                action();
            }
            finally
            {
                GraphSaveSlots.ReplaceFile = original;
            }
        }

        // FileShare.None でほかのプロセスからの置き換え・移動を止められるのは Windows だけ
        private static void RequireFileLocks()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("Only Windows enforces FileShare.None against renames.");
            }
        }

        private string[] FilesInSlotFolder() =>
            Directory.GetFiles(Path.GetDirectoryName(GraphSaveSlots.GetPath(_graph, "slot1"))).Select(Path.GetFileName).ToArray();

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
        public void Button_WarnsWhenItCannotSave()
        {
            // クリックの処理で例外にせず、警告を出して false（警告は Unity のログに出る）
            RequireFileLocks();
            GraphSaveSlots.Save(_runner, "slot1");
            var gameObject = new GameObject("Save Button");
            try
            {
                var button = gameObject.AddComponent<GraphEventButton>();
                button.Graph = _graph;
                button.Action = GraphEventButtonAction.Save;
                button.Slot = "slot1";

                using (File.Open(GraphSaveSlots.GetPath(_graph, "slot1"), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.That(button.TrySend(), Is.False);
                }
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
