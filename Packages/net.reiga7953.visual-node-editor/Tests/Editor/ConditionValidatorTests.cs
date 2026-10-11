using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>条件付き遷移の検証（パラメータ・比較の型・使われない条件・選ばれない自動の Event）。</summary>
    public sealed class ConditionValidatorTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _affection;
        private StateNode _play;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _affection = new GraphParameter("Affection", GraphParameterType.Int);
            _graph.AddParameter(_affection);
            var entry = Add(new EntryNode());
            _play = Add(new StateNode { Title = "Play" });
            Connect(entry, _play);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void ValidConditions_HaveNoIssues()
        {
            var done = Add(new EventNode { EventName = "Done" });
            done.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 5 });
            Connect(_play, done);

            Assert.That(Issues(), Is.Empty);
        }

        [Test]
        public void MissingParameter_IsAnError()
        {
            var done = Add(new EventNode { EventName = "Done" });
            done.AddCondition(new TransitionCondition("deleted", ConditionOperator.IsTrue));
            Connect(_play, done);

            Assert.That(Issues().Single(i => i.NodeId == done.Id).Severity, Is.EqualTo(GraphIssueSeverity.Error));
        }

        [Test]
        public void OperatorThatDoesNotFitTheType_IsAnError()
        {
            var done = Add(new EventNode { EventName = "Done" });
            done.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.IsTrue));
            Connect(_play, done);

            var issue = Issues().Single(i => i.NodeId == done.Id);
            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Error));
            Assert.That(issue.Message, Does.Contain("Affection"));
        }

        [Test]
        public void ConditionsOfAChainedEvent_AreWarnedAsUnused()
        {
            var go = Add(new EventNode { EventName = "Go" });
            var then = Add(new EventNode { EventName = "Then" });
            then.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.Equal));
            Connect(_play, go);
            Connect(go, then);

            var issue = Issues().Single(i => i.NodeId == then.Id);
            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("not used"));
        }

        [Test]
        public void AutomaticEventsAfterAnUnconditionedOne_NeverFire()
        {
            var always = Add(new EventNode { Trigger = EventTriggerMode.Automatic, Title = "Always" });
            var later = Add(new EventNode { Trigger = EventTriggerMode.Automatic, Title = "Later" });
            later.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.Greater));
            Connect(_play, always);
            Connect(_play, later);

            var issue = Issues().Single(i => i.NodeId == later.Id);
            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("Always"));
        }

        [Test]
        public void AutomaticEvents_NeedNoName()
        {
            var automatic = Add(new EventNode { Trigger = EventTriggerMode.Automatic });
            var raised = Add(new EventNode());
            Connect(_play, automatic);
            Connect(_play, raised);

            var issues = Issues();
            Assert.That(issues.Any(i => i.NodeId == automatic.Id), Is.False);
            Assert.That(issues.Any(i => i.NodeId == raised.Id && i.Message.Contains("no event name")), Is.True);
        }

        private System.Collections.Generic.List<GraphIssue> Issues() =>
            GraphValidator.Validate(_graph).Where(i => i.Kind != GraphIssueKind.MissingEntry).ToList();

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
