using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ボタンからグラフへイベントを送る <see cref="GraphEventButton"/> と、そのインスペクタの注意。</summary>
    public sealed class GraphEventButtonTests
    {
        private NodeGraphAsset _graph;
        private StateNode _title;
        private StateNode _play;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            // Entry → Title ─StartGame→ Play
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Flow";
            var entry = Add(new EntryNode());
            _title = Add(new StateNode { Title = "Title" });
            _play = Add(new StateNode { Title = "Play" });
            var start = Add(new EventNode { EventName = "StartGame" });
            Connect(entry, _title);
            Connect(_title, start);
            Connect(start, _play);
            _gameObject = new GameObject("Start Button");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in GraphRunner.Running.ToList())
            {
                runner.Stop();
            }

            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_graph);
        }

        // ---- イベント名の一覧 ----

        [Test]
        public void EventNames_InGraphOrderWithoutDuplicatesOrBlanks()
        {
            Add(new EventNode { EventName = "Pause" });
            Add(new EventNode { EventName = "StartGame" });
            Add(new EventNode { EventName = "" });
            Add(new EventNode { EventName = "startgame" });

            Assert.That(GraphEventNames.Collect(_graph), Is.EqualTo(new[] { "StartGame", "Pause", "startgame" }),
                "case differences are different names (they are reported as near-duplicates elsewhere)");
            Assert.That(GraphEventNames.Collect(null), Is.Empty);
        }

        // ---- 送る ----

        [Test]
        public void Raise_MovesTheRunnerOfThatGraph()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            var button = CreateButton(GraphEventButtonAction.Raise, "StartGame");

            Assert.That(button.TrySend(), Is.True);

            Assert.That(runner.Current, Is.SameAs(_play));
        }

        [Test]
        public void Advance_MovesWithoutAnEventName()
        {
            _graph.AddEdge(new EdgeData(_title.Id, "out", _play.Id, "in"));
            var runner = new GraphRunner(_graph);
            runner.Start();
            var button = CreateButton(GraphEventButtonAction.Advance, string.Empty);

            Assert.That(button.TrySend(), Is.True);

            Assert.That(runner.Current, Is.SameAs(_play));
        }

        [Test]
        public void UnknownEventOrNoRunner_DoesNothing()
        {
            var button = CreateButton(GraphEventButtonAction.Raise, "StartGame");
            Assert.That(button.TrySend(), Is.False, "nothing is running yet");

            var runner = new GraphRunner(_graph);
            runner.Start();
            button.EventName = "Missing";

            Assert.That(button.TrySend(), Is.False);
            Assert.That(runner.Current, Is.SameAs(_title));
        }

        [Test]
        public void FindRunner_PicksTheRunnerOfTheGraph_OrAnyWhenNoGraphIsSet()
        {
            var other = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                other.AddNode(new EntryNode());
                var otherRunner = new GraphRunner(other);
                otherRunner.Start();
                var runner = new GraphRunner(_graph);
                runner.Start();

                Assert.That(GraphEventButton.FindRunner(_graph), Is.SameAs(runner));
                Assert.That(GraphEventButton.FindRunner(other), Is.SameAs(otherRunner));
                Assert.That(GraphEventButton.FindRunner(null), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        // ---- ボタンのクリックに繋ぐ ----

        [Test]
        public void Click_SendsTheEvent()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            var source = new ClickSourceTestComponent();
            var button = CreateButton(GraphEventButtonAction.Raise, "StartGame");

            button.HookClick(GraphEventButton.FindClickEventOn(source));
            source.onClick.Invoke();

            Assert.That(runner.Current, Is.SameAs(_play));
        }

        [Test]
        public void ClickHook_CanBeTurnedOffAndUnhooked()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            var source = new ClickSourceTestComponent();
            var button = CreateButton(GraphEventButtonAction.Raise, "StartGame");

            button.SendOnClick = false;
            button.HookClick(source.onClick);
            source.onClick.Invoke();
            Assert.That(runner.Current, Is.SameAs(_title), "not hooked while Send On Click is off");

            button.SendOnClick = true;
            button.HookClick(source.onClick);
            button.UnhookClick();
            source.onClick.Invoke();
            Assert.That(runner.Current, Is.SameAs(_title), "unhooked when disabled");
        }

        [Test]
        public void ClickThatAlreadyCallsSend_IsNotHookedAgain()
        {
            var button = CreateButton(GraphEventButtonAction.Advance, string.Empty);
            var wiredByHand = new UnityEvent();
            UnityEventTools.AddVoidPersistentListener(wiredByHand, button.Send);
            var plain = new UnityEvent();

            Assert.That(GraphEventButton.CallsSend(wiredByHand, button), Is.True);
            Assert.That(GraphEventButton.CallsSend(plain, button), Is.False);

            button.HookClick(wiredByHand);
            Assert.That(button.IsClickHooked, Is.False, "OnClick already sends; hooking again would send twice per click");

            button.HookClick(plain);
            Assert.That(button.IsClickHooked, Is.True);
        }

        [Test]
        public void ClickWithSendSwitchedOff_IsStillHooked()
        {
            var button = CreateButton(GraphEventButtonAction.Advance, string.Empty);
            var switchedOff = new UnityEvent();
            UnityEventTools.AddVoidPersistentListener(switchedOff, button.Send);
            switchedOff.SetPersistentListenerState(0, UnityEventCallState.Off);

            Assert.That(GraphEventButton.CallsSend(switchedOff, button), Is.False, "an Off entry never fires");

            button.HookClick(switchedOff);
            Assert.That(button.IsClickHooked, Is.True, "otherwise the click would send nothing");
        }

        [Test]
        public void FindClickEvent_NeedsAPublicOnClickEvent()
        {
            var source = new ClickSourceTestComponent();

            Assert.That(GraphEventButton.FindClickEventOn(source), Is.SameAs(source.onClick));
            Assert.That(GraphEventButton.FindClickEventOn(new object()), Is.Null);
            Assert.That(GraphEventButton.FindClickEventOn(null), Is.Null);
            Assert.That(GraphEventButton.FindClickEvent(_gameObject), Is.Null, "a bare GameObject has no button");
        }

        // ---- インスペクタの注意 ----

        [Test]
        public void Hints_GuideToTheNextStep()
        {
            var events = new[] { "StartGame", "Pause" };

            Assert.That(GraphEventButtonHints.Get(events, "StartGame", GraphEventButtonAction.Raise, true, true), Is.Empty);
            Assert.That(GraphEventButtonHints.Get(null, "StartGame", GraphEventButtonAction.Raise, true, true).Single(),
                Does.Contain("Assign the graph"));
            Assert.That(GraphEventButtonHints.Get(events, "", GraphEventButtonAction.Raise, true, true).Single(),
                Does.Contain("Choose the event"));
            Assert.That(GraphEventButtonHints.Get(events, "Missing", GraphEventButtonAction.Raise, true, true).Single(),
                Does.Contain("not an event in this graph"));
            Assert.That(GraphEventButtonHints.Get(new string[0], "", GraphEventButtonAction.Raise, true, true),
                Has.Some.Matches<string>(hint => hint.Contains("no Event nodes yet")));
            Assert.That(GraphEventButtonHints.Get(events, "StartGame", GraphEventButtonAction.Raise, true, false).Single(),
                Does.Contain("No button on this GameObject"));
            Assert.That(GraphEventButtonHints.Get(null, "", GraphEventButtonAction.Advance, false, false), Is.Empty,
                "Advance needs no event name");
            Assert.That(GraphEventButtonHints.Get(events, "StartGame", GraphEventButtonAction.Raise, true, true, true).Single(),
                Does.Contain("already calls Send()"));
        }

        [Test]
        public void SerializedFieldNamesUsedByTheInspectorExist()
        {
            var button = _gameObject.AddComponent<GraphEventButton>();
            var serializedObject = new SerializedObject(button);

            Assert.That(serializedObject.FindProperty(GraphEventButtonEditor.GraphPropertyName), Is.Not.Null);
            Assert.That(serializedObject.FindProperty(GraphEventButtonEditor.ActionPropertyName), Is.Not.Null);
            Assert.That(serializedObject.FindProperty(GraphEventButtonEditor.EventNamePropertyName), Is.Not.Null);
            Assert.That(serializedObject.FindProperty(GraphEventButtonEditor.SendOnClickPropertyName), Is.Not.Null);
        }

        private GraphEventButton CreateButton(GraphEventButtonAction action, string eventName)
        {
            var button = _gameObject.AddComponent<GraphEventButton>();
            button.Graph = _graph;
            button.Action = action;
            button.EventName = eventName;
            return button;
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
