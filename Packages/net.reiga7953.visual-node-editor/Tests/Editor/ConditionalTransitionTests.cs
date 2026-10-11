using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>条件付きの Event と自動遷移で Runner がどう進むか。</summary>
    public sealed class ConditionalTransitionTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private GraphParameter _affection;
        private GraphParameter _met;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "ConditionGraph";
            _affection = new GraphParameter("Affection", GraphParameterType.Int);
            _met = new GraphParameter("Met", GraphParameterType.Bool);
            _graph.AddParameter(_affection);
            _graph.AddParameter(_met);
        }

        [TearDown]
        public void TearDown()
        {
            _runner?.Stop();
            Object.DestroyImmediate(_graph);
        }

        // ---- Raise ----

        [Test]
        public void Raise_PicksTheFirstEventWhoseConditionsHold()
        {
            // Chapter → Done (Affection >= 5) → Alice / Done → Normal（後ろの条件無しが「それ以外」）
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            var normal = AddState("Normal");
            Connect(chapter, Event("Done", AtLeast(5)), alice);
            Connect(chapter, Event("Done"), normal);
            _runner = Run();

            _runner.SetInt("Affection", 5);
            Assert.That(_runner.Raise("Done"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(alice));
        }

        [Test]
        public void Raise_FallsThroughToTheNextEventWithTheSameName()
        {
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            var normal = AddState("Normal");
            Connect(chapter, Event("Done", AtLeast(5)), alice);
            Connect(chapter, Event("Done"), normal);
            _runner = Run();

            _runner.SetInt("Affection", 2);
            Assert.That(_runner.Raise("Done"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(normal));
        }

        [Test]
        public void Raise_DoesNothingWhenNoConditionHolds()
        {
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            var done = Event("Done", AtLeast(5));
            Connect(chapter, done, alice);
            _runner = Run();

            Assert.That(_runner.FindRaiseTarget("Done"), Is.Null);
            Assert.That(_runner.Raise("Done"), Is.False);
            Assert.That(_runner.Current, Is.SameAs(chapter));

            _runner.SetInt("Affection", 9);
            Assert.That(_runner.FindRaiseTarget("Done"), Is.SameAs(done));
        }

        [Test]
        public void AllConditionsMustHold()
        {
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            Connect(chapter, Event("Done", AtLeast(5), IsTrue(_met)), alice);
            _runner = Run();

            _runner.SetInt("Affection", 7);
            Assert.That(_runner.Raise("Done"), Is.False, "Met is still false");
            _runner.SetBool("Met", true);
            Assert.That(_runner.Raise("Done"), Is.True);
        }

        [Test]
        public void ConditionsOnAMissingParameter_AreNeverMet()
        {
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            var done = Event("Done", new TransitionCondition("deleted-parameter", ConditionOperator.IsTrue));
            Connect(chapter, done, alice);
            _runner = Run();

            Assert.That(_runner.AreConditionsMet(done), Is.False);
            Assert.That(_runner.Raise("Done"), Is.False);
        }

        [Test]
        public void ConditionsFollowARenamedParameter()
        {
            var chapter = StartAt("Chapter");
            var alice = AddState("Alice");
            Connect(chapter, Event("Done", AtLeast(5)), alice);
            _affection.Name = "Love";
            _runner = Run();

            _runner.SetInt("Love", 5);
            Assert.That(_runner.Raise("Done"), Is.True);
        }

        // ---- 自動遷移 ----

        [Test]
        public void Automatic_MovesRightAfterEnteringWhenItHolds()
        {
            // Story（シーン）に入ったらすぐ中へ進む（条件の無い自動の Event）
            var story = StartAt("Story");
            var chapter = AddState("Chapter");
            Connect(story, Automatic(), chapter);

            _runner = Run();

            Assert.That(_runner.Current, Is.SameAs(chapter));
        }

        [Test]
        public void Automatic_MovesWhenAParameterChanges()
        {
            var play = StartAt("Play");
            var gameOver = AddState("GameOver");
            Connect(play, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue)), gameOver);
            _runner = Run();
            Assert.That(_runner.Current, Is.SameAs(play));

            _runner.SetBool("Met", true);

            Assert.That(_runner.Current, Is.SameAs(gameOver));
        }

        [Test]
        public void Automatic_IsCheckedAfterTheTransitionThatChangedTheValue()
        {
            // 入った通知の中で値を変えても、その遷移が終わってから調べる（遷移の途中で別の遷移を始めない）
            var play = StartAt("Play");
            var gameOver = AddState("GameOver");
            Connect(play, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue)), gameOver);
            var entered = new List<string>();
            _runner = new GraphRunner(_graph);
            _runner.NodeEntered += node =>
            {
                entered.Add(node.Title);
                if (node == play)
                {
                    _runner.SetBool("Met", true);
                }
            };

            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(gameOver));
            Assert.That(entered, Is.EqualTo(new[] { "Entry", "Play", "Automatic", "GameOver" }));
        }

        [Test]
        public void Automatic_IsNotRaisedByName()
        {
            var play = StartAt("Play");
            var next = AddState("Next");
            Connect(play, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue), "Finish"), next);
            _runner = Run();

            Assert.That(_runner.Raise("Finish"), Is.False);
            Assert.That(_runner.Current, Is.SameAs(play));
        }

        [Test]
        public void NamedAutomatic_StillNotifiesItsName()
        {
            var play = StartAt("Play");
            var next = AddState("Next");
            Connect(play, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue), "Finish"), next);
            var notified = 0;
            _runner = Run();
            _runner.On("Finish", () => notified++);

            _runner.SetBool("Met", true);

            Assert.That(notified, Is.EqualTo(1));
        }

        [Test]
        public void Automatic_FollowsEdgeOrder()
        {
            var play = StartAt("Play");
            var first = AddState("First");
            var second = AddState("Second");
            Connect(play, Automatic(AtLeast(1)), first);
            Connect(play, Automatic(AtLeast(1)), second);
            _runner = Run();

            _runner.SetInt("Affection", 3);

            Assert.That(_runner.Current, Is.SameAs(first));
        }

        [Test]
        public void Automatic_ChainsAcrossNodes()
        {
            var a = StartAt("A");
            var b = AddState("B");
            var c = AddState("C");
            Connect(a, Automatic(), b);
            Connect(b, Automatic(), c);

            _runner = Run();

            Assert.That(_runner.Current, Is.SameAs(c));
        }

        [Test]
        public void Automatic_LoopStopsInsteadOfHanging()
        {
            var a = StartAt("A");
            var b = AddState("B");
            Connect(a, Automatic(), b);
            Connect(b, Automatic(), a);

            _runner = Run();

            Assert.That(_runner.IsRunning, Is.True, "stays at some node after the limit");
            Assert.That(_runner.Current == a || _runner.Current == b, Is.True);
        }

        [Test]
        public void Automatic_WaitsAtEntryToo()
        {
            // Entry から Event にしか繋がっていなければ Entry で待つ。そこでも自動の Event を調べる
            var entry = Add(new EntryNode());
            var title = AddState("Title");
            Connect(entry, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue)), title);
            _runner = Run();
            Assert.That(_runner.Current, Is.SameAs(entry));

            _runner.SetBool("Met", true);

            Assert.That(_runner.Current, Is.SameAs(title));
        }

        [Test]
        public void ConditionsOfAChainedEvent_AreIgnored()
        {
            // 待機ノードから直接繋がった Event だけを見る（Event → Event の先の条件は使わない）
            var play = StartAt("Play");
            var next = AddState("Next");
            var first = Event("Go");
            var chained = Event("Then", AtLeast(99));
            Connect(play, first);
            Connect(first, chained);
            Connect(chained, next);
            _runner = Run();

            Assert.That(_runner.Raise("Go"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(next));
        }

        [Test]
        public void ParameterChanges_AfterStop_DoNothing()
        {
            var play = StartAt("Play");
            var gameOver = AddState("GameOver");
            Connect(play, Automatic(new TransitionCondition(_met.Id, ConditionOperator.IsTrue)), gameOver);
            _runner = Run();
            _runner.Stop();

            _runner.SetBool("Met", true);

            Assert.That(_runner.Current, Is.Null);
        }

        // ---- 組み立て ----

        private GraphRunner Run()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            return runner;
        }

        private StateNode StartAt(string title)
        {
            var entry = Add(new EntryNode());
            var state = AddState(title);
            Connect(entry, state);
            return state;
        }

        private StateNode AddState(string title) => Add(new StateNode { Title = title });

        private EventNode Event(string name, params TransitionCondition[] conditions)
        {
            var node = Add(new EventNode { EventName = name });
            foreach (var condition in conditions)
            {
                node.AddCondition(condition);
            }

            return node;
        }

        private EventNode Automatic(TransitionCondition condition = null, string name = null)
        {
            var node = Add(new EventNode { EventName = name, Trigger = EventTriggerMode.Automatic, Title = "Automatic" });
            node.AddCondition(condition);
            return node;
        }

        private TransitionCondition AtLeast(int value) =>
            new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = value };

        private static TransitionCondition IsTrue(GraphParameter parameter) => new TransitionCondition(parameter.Id, ConditionOperator.IsTrue);

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
