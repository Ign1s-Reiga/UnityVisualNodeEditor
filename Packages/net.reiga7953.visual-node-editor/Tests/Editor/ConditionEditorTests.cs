using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Debugging;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>エディタでの条件の編集・表示（インスペクタの一覧、ノードのサマリー、Now running のボタン）。</summary>
    public sealed class ConditionEditorTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _affection;
        private GraphParameter _met;
        private EventNode _event;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _affection = new GraphParameter("Affection", GraphParameterType.Int);
            _met = new GraphParameter("Met", GraphParameterType.Bool);
            _graph.AddParameter(_affection);
            _graph.AddParameter(_met);
            _event = new EventNode { EventName = "Done" };
            _graph.AddNode(_event);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 編集 ----

        [Test]
        public void Add_UsesTheFirstParameterAndItsFirstOperator()
        {
            var condition = ConditionEditing.Add(_graph, _event);

            Assert.That(condition.ParameterId, Is.EqualTo(_affection.Id));
            Assert.That(condition.Operator, Is.EqualTo(ConditionOperator.Equal));
            Assert.That(_event.Conditions, Has.Count.EqualTo(1));
        }

        [Test]
        public void Add_NeedsAParameter()
        {
            _graph.RemoveParameter(_affection);
            _graph.RemoveParameter(_met);

            Assert.That(ConditionEditing.Add(_graph, _event), Is.Null);
            Assert.That(_event.Conditions, Is.Empty);
        }

        [Test]
        public void SetParameter_PicksAnOperatorThatFitsTheNewType()
        {
            ConditionEditing.Add(_graph, _event);
            ConditionEditing.SetOperator(_graph, _event, 0, ConditionOperator.GreaterOrEqual);

            Assert.That(ConditionEditing.SetParameter(_graph, _event, 0, _met.Id), Is.True);

            Assert.That(_event.Conditions[0].ParameterId, Is.EqualTo(_met.Id));
            Assert.That(_event.Conditions[0].Operator, Is.EqualTo(ConditionOperator.IsTrue), ">= does not work for a Bool");
        }

        [Test]
        public void Remove_TakesTheConditionOut_AndCanBeUndone()
        {
            ConditionEditing.Add(_graph, _event);
            Undo.IncrementCurrentGroup();

            Assert.That(ConditionEditing.Remove(_graph, _event, 0), Is.True);
            Assert.That(_event.Conditions, Is.Empty);

            Undo.PerformUndo();
            Assert.That(((EventNode)_graph.FindNode(_event.Id)).Conditions, Has.Count.EqualTo(1));
        }

        [Test]
        public void ListView_ShowsARowPerCondition_AndAddsOne()
        {
            ConditionEditing.Add(_graph, _event);
            var property = new SerializedObject(_graph).FindProperty("_nodes").GetArrayElementAtIndex(0)
                .FindPropertyRelative(ConditionListView.ConditionsPropertyName);
            var changed = 0;
            var view = new ConditionListView(_graph, _event, property);
            view.Changed += () => changed++;

            Assert.That(view.Rows, Has.Count.EqualTo(1));
            Assert.That(view.AddCondition(), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(_event.Conditions, Has.Count.EqualTo(2));
        }

        [Test]
        public void ConditionsField_IsReachable()
        {
            var node = new SerializedObject(_graph).FindProperty("_nodes").GetArrayElementAtIndex(0);

            Assert.That(node.FindPropertyRelative(ConditionListView.ConditionsPropertyName), Is.Not.Null);
        }

        // ---- 表示 ----

        [Test]
        public void Summary_ShowsTheConditions()
        {
            _event.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 5 });
            _event.AddCondition(new TransitionCondition(_met.Id, ConditionOperator.IsTrue));

            Assert.That(NodeDisplay.GetEventSummary(_event, _graph), Is.EqualTo("if Affection >= 5 and Met"));

            _event.Title = "To Alice";
            Assert.That(NodeDisplay.GetEventSummary(_event, _graph), Is.EqualTo("Done if Affection >= 5 and Met"));
        }

        [Test]
        public void UnnamedAutomaticEvent_IsCalledAuto()
        {
            var automatic = new EventNode { Trigger = EventTriggerMode.Automatic };

            Assert.That(NodeDisplay.GetNodeLabel(automatic), Is.EqualTo(NodeDisplay.AutomaticEventLabel));
        }

        [Test]
        public void ButtonNames_LeaveOutAutomaticEvents()
        {
            _graph.AddNode(new EventNode { EventName = "Timeout", Trigger = EventTriggerMode.Automatic });

            Assert.That(GraphEventNames.CollectRaisable(_graph), Is.EqualTo(new[] { "Done" }));
            Assert.That(GraphEventNames.Collect(_graph), Is.EqualTo(new[] { "Done", "Timeout" }));
        }

        // ---- Now running ----

        [Test]
        public void RuntimeActions_LeaveOutAutomaticEvents_AndExplainUnmetConditions()
        {
            var entry = new EntryNode();
            var play = new StateNode { Title = "Play" };
            var alice = new StateNode { Title = "Alice" };
            var auto = new EventNode { Trigger = EventTriggerMode.Automatic };
            auto.AddCondition(new TransitionCondition(_met.Id, ConditionOperator.IsTrue));
            _event.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 5 });
            foreach (var node in new NodeData[] { entry, play, alice, auto })
            {
                _graph.AddNode(node);
            }

            Connect(entry, play);
            Connect(play, _event);
            Connect(_event, alice);
            Connect(play, auto);
            Connect(auto, alice);
            var runner = new GraphRunner(_graph);
            runner.Start();
            try
            {
                var actions = RuntimeActions.For(runner.Query, runner.Current);
                Assert.That(actions.Select(a => a.Label), Is.EqualTo(new[] { "Done" }));
                Assert.That(RuntimeActions.GetBlockedReason(runner, actions[0]), Is.EqualTo("Needs Affection >= 5"));

                runner.SetInt("Affection", 5);
                Assert.That(RuntimeActions.GetBlockedReason(runner, actions[0]), Is.Empty);
            }
            finally
            {
                runner.Stop();
            }
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
