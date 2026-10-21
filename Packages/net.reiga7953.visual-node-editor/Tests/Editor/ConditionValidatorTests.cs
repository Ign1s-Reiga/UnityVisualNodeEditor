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
        public void ARaiseEventAfterAnUnconditionedOneWithTheSameName_NeverFires()
        {
            // Codex のエンディングの分岐: 条件の無い Finish が先にあると、Finish if Affection >= 60 は選ばれない
            var normal = Add(new EventNode { EventName = "Finish" });
            var romance = Add(new EventNode { EventName = "Finish", Title = "Romance" });
            romance.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            Connect(_play, normal);
            Connect(_play, romance);

            var issue = Issues().Single();
            Assert.That(issue.NodeId, Is.EqualTo(romance.Id));
            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("'Romance' never fires from 'Play'").And.Contain("Transitions list of 'Play'"));
        }

        [Test]
        public void ARaiseEventWithConditionsBeforeTheUnconditionedOne_IsFine()
        {
            var romance = Add(new EventNode { EventName = "Finish" });
            romance.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            Connect(_play, romance);
            Connect(_play, Add(new EventNode { EventName = "Finish" }));
            Connect(_play, Add(new EventNode { EventName = "Retry" }));

            Assert.That(Issues(), Is.Empty);
        }

        [Test]
        public void ASecondNodeReachedWithoutAnEvent_IsNeverReached()
        {
            var first = Add(new StateNode { Title = "First" });
            var second = Add(new StateNode { Title = "Second" });
            Connect(_play, first);
            Connect(_play, Add(new EventNode { EventName = "Go" }));
            Connect(_play, second);

            var issue = Issues().Single();
            Assert.That(issue.NodeId, Is.EqualTo(second.Id));
            Assert.That(issue.Message, Does.Contain("'Second' is never reached from 'Play'").And.Contain("'First'"));
        }

        [Test]
        public void TheSecondEdgeOfAnEvent_IsNeverUsed()
        {
            var go = Add(new EventNode { EventName = "Go" });
            Connect(_play, go);
            Connect(go, Add(new StateNode { Title = "First" }));
            Connect(go, Add(new StateNode { Title = "Second" }));

            var issue = Issues().Single();
            Assert.That(issue.NodeId, Is.EqualTo(go.Id), "on the Event itself");
            Assert.That(issue.Message, Does.Contain("'Go' only goes on to 'First'").And.Contain("'Second'"));
        }

        [Test]
        public void DuplicatedEdgesAndContainerExits_AreNotHiddenTransitions()
        {
            var done = Add(new EventNode { EventName = "Done" });
            Connect(_play, done);
            Connect(_play, done);

            // コンテナの出口ごとのエッジは対象外
            var stage = Add(new ContainerNode { Title = "Stage" });
            var exit = stage.Exits.First();
            _graph.AddEdge(new EdgeData(stage.Id, exit.Id, _play.Id, "in"));
            _graph.AddEdge(new EdgeData(stage.Id, exit.Id, done.Id, "in"));

            Assert.That(Issues().Where(i => i.Message.Contains("never")), Is.Empty);
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
