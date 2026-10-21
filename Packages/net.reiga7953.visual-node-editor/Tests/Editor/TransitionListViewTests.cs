using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>インスペクタの Transitions（出ているエッジを調べる順・選ばれない行・並べ替え）。</summary>
    public sealed class TransitionListViewTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _affection;
        private StateNode _story;
        private StateNode _romance;
        private StateNode _normal;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _affection = new GraphParameter("Affection", GraphParameterType.Int);
            _graph.AddParameter(_affection);
            _story = Add(new StateNode { Title = "Story" });
            _romance = Add(new StateNode { Title = "Romance" });
            _normal = Add(new StateNode { Title = "Normal" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void TheList_ShowsTheEdgesInOrder_OnlyWhenThereAreTwoOrMore()
        {
            var first = Connect(_story, Add(new EventNode { EventName = "Finish" }));
            var list = new TransitionListView(_graph, _story);
            Assert.That(list.IsShown, Is.False, "one way only: nothing to order");

            var second = Connect(_story, Add(new EventNode { EventName = "Retry" }));
            list.Reload();

            Assert.That(list.IsShown, Is.True);
            Assert.That(list.Items, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void Rows_TellTheKindTheEventAndWhereItGoes()
        {
            var loved = Add(new EventNode { EventName = "Finish" });
            loved.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            Connect(loved, _romance);
            var automatic = Add(new EventNode { Trigger = EventTriggerMode.Automatic });
            var notifyOnly = Add(new EventNode { EventName = "Ping" });

            Assert.That(TransitionListView.GetKind(_story, loved), Is.EqualTo("Raise"));
            Assert.That(TransitionListView.GetLabel(_graph, loved), Is.EqualTo("Finish if Affection >= 60"));
            Assert.That(TransitionListView.GetDestination(_graph, _story, loved), Is.EqualTo("→ Romance"));

            Assert.That(TransitionListView.GetKind(_story, automatic), Is.EqualTo("Auto"));
            Assert.That(TransitionListView.GetLabel(_graph, automatic), Is.EqualTo("(always)"));
            Assert.That(TransitionListView.GetDestination(_graph, _story, notifyOnly), Does.Contain("only notifies"));

            Assert.That(TransitionListView.GetKind(_story, _normal), Is.EqualTo("Next"));
            Assert.That(TransitionListView.GetLabel(_graph, _normal), Is.EqualTo("Normal"));
            Assert.That(TransitionListView.GetDestination(_graph, _story, _normal), Is.Empty);

            Assert.That(TransitionListView.GetKind(loved, _romance), Is.EqualTo("Next"), "from an Event");
            Assert.That(TransitionListView.GetLabel(_graph, null), Is.EqualTo("(missing node)"));
        }

        [Test]
        public void ARowThatIsNeverChosen_SaysWhy()
        {
            var always = Connect(_story, Add(new EventNode { EventName = "Finish" }));
            var loved = Add(new EventNode { EventName = "Finish", Title = "Loved" });
            loved.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            var hidden = Connect(_story, loved);

            var list = new TransitionListView(_graph, _story);

            Assert.That(list.GetHiddenReason(always), Is.Null);
            Assert.That(list.GetHiddenReason(hidden), Does.Contain("'Loved' never fires from 'Story'"));
        }

        [Test]
        public void Move_ReordersTheEdges_AndCanBeUndone()
        {
            var always = Add(new EventNode { EventName = "Finish" });
            var loved = Add(new EventNode { EventName = "Finish", Title = "Loved" });
            loved.AddCondition(new TransitionCondition(_affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            Connect(_story, always);
            var lovedEdge = Connect(_story, loved);
            var list = new TransitionListView(_graph, _story);
            var changed = 0;
            list.Changed += () => changed++;

            Undo.IncrementCurrentGroup();
            Assert.That(list.Move(lovedEdge, 0), Is.True);

            Assert.That(Targets(), Is.EqualTo(new[] { loved.Id, always.Id }));
            Assert.That(list.Items.Select(e => e.ToNodeId), Is.EqualTo(new[] { loved.Id, always.Id }), "the list follows");
            Assert.That(list.Items.Select(list.GetHiddenReason), Has.All.Null, "the conditioned one comes first now");
            Assert.That(GraphValidator.Validate(_graph).Where(i => i.Message.Contains("never")), Is.Empty);
            Assert.That(changed, Is.EqualTo(1));

            Undo.PerformUndo();
            Assert.That(Targets(), Is.EqualTo(new[] { always.Id, loved.Id }));
        }

        [Test]
        public void Move_OfAnEdgeThatIsGone_OnlyReloads()
        {
            Connect(_story, _romance);
            Connect(_story, _normal);
            var list = new TransitionListView(_graph, _story);

            Assert.That(list.Move(new EdgeData(_story.Id, "out", "gone", "in"), 0), Is.False);
            Assert.That(Targets(), Is.EqualTo(new[] { _romance.Id, _normal.Id }));
        }

        [Test]
        public void ContainersAndNotes_HaveNoList()
        {
            Assert.That(TransitionListView.Supports(_story), Is.True);
            Assert.That(TransitionListView.Supports(new EventNode()), Is.True);
            Assert.That(TransitionListView.Supports(new ContainerNode()), Is.False, "edges per exit");
            Assert.That(TransitionListView.Supports(new NoteNode()), Is.False);
        }

        private string[] Targets() => _graph.Edges.Where(e => e.FromNodeId == _story.Id).Select(e => e.ToNodeId).ToArray();

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private EdgeData Connect(NodeData from, NodeData to)
        {
            var edge = new EdgeData(from.Id, "out", to.Id, "in");
            _graph.AddEdge(edge);
            return edge;
        }
    }
}
