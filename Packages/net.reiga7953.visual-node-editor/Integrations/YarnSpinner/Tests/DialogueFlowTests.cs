using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>グラフの Runner と会話の合わせ方（<see cref="DialogueFlow"/>）。</summary>
    public sealed class DialogueFlowTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private FakeDialoguePlayer _player;
        private DialogueFlow _flow;
        private List<string> _warnings;
        private GraphParameter _affection;

        private StateNode _title;
        private DialogueNode _chapter;
        private DialogueNode _good;
        private DialogueNode _normal;
        private StateNode _menu;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Adv";
            _affection = new GraphParameter("Affection", GraphParameterType.Int);
            _graph.AddParameter(_affection);

            // Entry → Title ─(Start)→ Chapter ─(Done, Affection >= 3)→ Good
            //                                  └(Done)→ Normal ─→ Menu
            //                         Chapter ─(Skip)→ Menu
            var entry = Add(new EntryNode());
            _title = Add(new StateNode { Title = "Title" });
            _chapter = Add(new DialogueNode { YarnNode = "Chapter1" });
            _good = Add(new DialogueNode { YarnNode = "GoodEnd" });
            _normal = Add(new DialogueNode { YarnNode = "NormalEnd" });
            _menu = Add(new StateNode { Title = "Menu" });
            Connect(entry, _title);
            Connect(_title, Event("Start"), _chapter);
            var toGood = Event("Done");
            toGood.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 3 });
            Connect(_chapter, toGood, _good);
            Connect(_chapter, Event("Done"), _normal);
            Connect(_normal, _menu);
            Connect(_chapter, Event("Skip"), _menu);

            _player = new FakeDialoguePlayer();
            _warnings = new List<string>();
            _flow = new DialogueFlow(_player) { Warn = _warnings.Add };
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Detach();
            _runner?.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void EnteringADialogueNode_StartsItsYarnNodeOnTheNextTick()
        {
            Begin();

            _runner.Raise("Start");
            Assert.That(_player.Started, Is.Empty, "not inside the graph's notification");

            _flow.Tick();
            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1" }));
        }

        [Test]
        public void Attach_StartsTheDialogueTheRunnerIsAlreadyIn()
        {
            // シーンの読み込みが終わる前に Dialogue ノードへ進んでいた（Bridge が後から有効になった）
            _runner = new GraphRunner(_graph);
            _runner.Start();
            _runner.Raise("Start");

            _flow.Attach(_runner);
            _flow.Tick();

            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1" }));
        }

        [Test]
        public void Completion_RaisesTheCompleteEvent_SoConditionsPickTheRoute()
        {
            AtChapter();
            _runner.SetInt("Affection", 5);

            _flow.OnDialogueCompleted();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(_good));
            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1", "GoodEnd" }), "the next dialogue starts in the same tick");
        }

        [Test]
        public void Completion_TakesTheOtherwiseRoute()
        {
            AtChapter();

            _flow.OnDialogueCompleted();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(_normal));
        }

        [Test]
        public void GraphNext_ChoosesTheEventInsteadOfTheCompleteEvent()
        {
            AtChapter();

            _flow.SetNext("Skip");
            _flow.OnDialogueCompleted();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(_menu));
        }

        [Test]
        public void Completion_AdvancesWhenThereIsNoCompleteEvent()
        {
            AtChapter();
            _flow.OnDialogueCompleted();
            _flow.Tick();
            Assert.That(_runner.Current, Is.SameAs(_normal));

            _flow.OnDialogueCompleted();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(_menu), "Normal → Menu is a plain edge");
        }

        [Test]
        public void LeavingTheNode_StopsTheDialogue_AndTheStopDoesNotMoveTheGraph()
        {
            AtChapter();

            _runner.Raise("Skip");
            _flow.Tick();
            Assert.That(_player.StopCount, Is.EqualTo(1));

            _flow.OnDialogueCompleted();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(_menu));
            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1" }));
        }

        [Test]
        public void MovingToAnotherDialogue_StopsThenStartsIt()
        {
            var again = Event("Again");
            Connect(_chapter, again, _chapter);
            AtChapter();

            // 同じノードへ入り直しても、会話を最初から流し直す
            _runner.Raise("Again");
            _flow.Tick();
            Assert.That(_player.StopCount, Is.EqualTo(1));
            Assert.That(_player.Started, Has.Count.EqualTo(1), "waits until the old dialogue has stopped");

            _flow.OnDialogueCompleted();
            _flow.Tick();
            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1", "Chapter1" }));
        }

        [Test]
        public void DeadEnd_WarnsOnce_AndDoesNotReplay()
        {
            var lonely = Add(new DialogueNode { YarnNode = "Epilogue" });
            Connect(_title, Event("Epilogue"), lonely);
            Begin();
            _runner.Raise("Epilogue");
            _flow.Tick();

            _flow.OnDialogueCompleted();
            _flow.Tick();
            _flow.Tick();

            Assert.That(_runner.Current, Is.SameAs(lonely));
            Assert.That(_warnings, Has.Count.EqualTo(1));
            Assert.That(_warnings[0], Does.Contain("nothing leads on"));
            Assert.That(_player.Started, Is.EqualTo(new[] { "Epilogue" }));
        }

        [Test]
        public void Detach_StopsTheDialogue()
        {
            AtChapter();

            _flow.Detach();
            _flow.Tick();

            Assert.That(_player.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_StopsTheDialogue_AndStartsAgainWithoutWaitingForTheStop()
        {
            // Bridge が無効になると、止まった知らせを聞かずに外れる。また有効になったら、その知らせを待たずに会話を始められる
            AtChapter();

            _flow.Shutdown();
            Assert.That(_player.StopCount, Is.EqualTo(1));

            _flow.Attach(_runner);
            _flow.Tick();
            Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1", "Chapter1" }));
        }

        [Test]
        public void GraphEvent_RaisesRightAway()
        {
            var shake = Event("Shake");
            Connect(_chapter, shake);
            AtChapter();
            var shaken = 0;
            _runner.On("Shake", () => shaken++);

            Assert.That(_flow.RaiseNow("Shake"), Is.True);
            Assert.That(shaken, Is.EqualTo(1));
            Assert.That(_runner.Current, Is.SameAs(_chapter), "a notification-only Event keeps the dialogue going");

            Assert.That(_flow.RaiseNow("Nothing"), Is.False);
            Assert.That(_warnings, Has.Some.Contains("Nothing"));
        }

        [Test]
        public void MissingYarnNode_IsWarnedAndSkipped()
        {
            _chapter.YarnNode = string.Empty;
            AtChapter(expectStart: false);

            Assert.That(_player.Started, Is.Empty);
            Assert.That(_warnings, Has.Some.Contains("no Yarn node"));
        }

        [Test]
        public void YarnNodeMissingFromTheProject_WarnsAndMovesOn()
        {
            // Yarn に無いノードは始められず、終わりの知らせも来ない。待たずに、終わったときと同じく次へ進める
            _player.Missing.Add("Chapter1");
            AtChapter(expectStart: false);

            Assert.That(_warnings, Has.Some.Contains("could not start the Yarn node 'Chapter1'"));
            Assert.That(_runner.Current, Is.SameAs(_normal));

            _flow.Tick();
            Assert.That(_player.Started, Is.EqualTo(new[] { "NormalEnd" }), "the next dialogue still plays");
        }

        [Test]
        public void AutoSave_SavesWhenEnteringADialogue_WithTheValuesFromBefore()
        {
            var root = Path.Combine(Path.GetTempPath(), "VneYarnAutoSave_" + Guid.NewGuid().ToString("N"));
            GraphSaveSlots.RootOverride = root;
            try
            {
                _flow.AutoSaveSlot = "auto";
                Begin();
                _runner.SetInt("Affection", 2);

                _runner.Raise("Start");

                var saved = GraphSaveSlots.Read(_graph, "auto");
                Assert.That(saved.NodeId, Is.EqualTo(_chapter.Id));
                Assert.That(saved.Parameters[0].Value, Is.EqualTo(2));
            }
            finally
            {
                GraphSaveSlots.RootOverride = null;
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        // ---- 組み立て ----

        // Runner はグラフを作った時点の索引で動くので、テストでノードを足し終えてから作る
        private void Begin()
        {
            _runner = new GraphRunner(_graph);
            _runner.Start();
            _flow.Attach(_runner);
        }

        private void AtChapter(bool expectStart = true)
        {
            Begin();
            _runner.Raise("Start");
            _flow.Tick();
            if (expectStart)
            {
                Assert.That(_player.Started, Is.EqualTo(new[] { "Chapter1" }));
            }
        }

        private EventNode Event(string name) => Add(new EventNode { EventName = name });

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private void Connect(NodeData from, EventNode via, NodeData to)
        {
            Connect(from, via);
            Connect(via, to);
        }
    }
}
