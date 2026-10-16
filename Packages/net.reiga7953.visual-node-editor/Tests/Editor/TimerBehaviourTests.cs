using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// Timer の振る舞い: 時間になったら Raise し、残り時間をパラメータに出す。Keep Counting ならポーズを挟んでも制限時間が戻らない。
    /// Entry → Ready（Time = 5、Timer 2 → Go）→ Playing（Timer Time → TimeUp、Keep Counting）⇄ Paused、TimeUp → Over
    /// </summary>
    public sealed class TimerBehaviourTests
    {
        private NodeGraphAsset _graph;
        private TimerBehaviour _limit;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var time = new GraphParameter("Time", GraphParameterType.Float);
            _graph.AddParameter(time);
            var entry = Add(new EntryNode());
            var ready = Add(new StateNode { Title = "Ready" });
            ready.AddBehaviour(new SetParameterBehaviour { Changes = { new ParameterChange(time.Id) { FloatValue = 5f } } });
            ready.AddBehaviour(new TimerBehaviour { Seconds = 2f, EventName = "Go" });
            var playing = Add(new StateNode { Title = "Playing" });
            _limit = new TimerBehaviour { Seconds = 99f, EventName = "TimeUp", TimeLeft = "Time", KeepCounting = true };
            playing.AddBehaviour(_limit);
            var paused = Add(new StateNode { Title = "Paused" });
            var over = Add(new StateNode { Title = "Over" });
            Connect(entry, ready);
            Connect(ready, "Go", playing);
            Connect(playing, "Pause", paused);
            Connect(paused, "Pause", playing);
            Connect(playing, "TimeUp", over);
            _runner = new GraphRunner(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Ready_GoesOn_WhenTheTimeIsUp()
        {
            _runner.Start();
            _runner.Update(1.5f);
            Assert.That(Current, Is.EqualTo("Ready"));

            _runner.Update(0.6f);
            Assert.That(Current, Is.EqualTo("Playing"));
        }

        [Test]
        public void ATimeLimit_KeepsCounting_ThroughAPause()
        {
            _runner.Start();
            _runner.Update(2f);
            Assert.That(Current, Is.EqualTo("Playing"));
            Assert.That(_runner.GetFloat("Time"), Is.EqualTo(5f), "Keep Counting starts from the value Set Parameter put in");

            _runner.Update(2f);
            Assert.That(_runner.GetFloat("Time"), Is.EqualTo(3f).Within(1e-4f));
            _runner.Raise("Pause");
            _runner.Update(10f);
            _runner.Raise("Pause");
            Assert.That(_runner.GetFloat("Time"), Is.EqualTo(3f).Within(1e-4f), "the pause does not count, and coming back does not reset it");

            _runner.Update(2.5f);
            Assert.That(Current, Is.EqualTo("Playing"));
            _runner.Update(1f);
            Assert.That(Current, Is.EqualTo("Over"));
            Assert.That(_runner.GetFloat("Time"), Is.Zero);
        }

        [Test]
        public void WithoutKeepCounting_EnteringStartsAgainFromSeconds()
        {
            _limit.KeepCounting = false;
            _limit.Seconds = 4f;
            _runner.Start();
            _runner.Update(2f);
            Assert.That(_runner.GetFloat("Time"), Is.EqualTo(4f), "entering puts Seconds into Time Left");

            _runner.Update(3f);
            _runner.Raise("Pause");
            _runner.Raise("Pause");
            Assert.That(_runner.GetFloat("Time"), Is.EqualTo(4f));
        }

        [Test]
        public void ItRaisesOnce()
        {
            var timer = new TimerBehaviour { Seconds = 1f, EventName = "Nowhere" };
            _graph.Nodes.OfType<StateNode>().Single(node => node.Title == "Ready").AddBehaviour(timer);
            _runner.Start();

            _runner.Update(1f);
            _runner.Update(1f);

            Assert.That(Current, Is.EqualTo("Playing"), "Go fired at 2 s; Nowhere has no edge and is not raised again and again");
        }

        [Test]
        public void Issues_AndTheDescription()
        {
            var node = new StateNode { Title = "Wait" };
            Assert.That(new TimerBehaviour { EventName = string.Empty }.GetIssues(_graph, node).Single().Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(new TimerBehaviour { TimeLeft = "Nope" }.GetIssues(_graph, node).Single().Severity, Is.EqualTo(GraphIssueSeverity.Error));
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int));
            Assert.That(new TimerBehaviour { TimeLeft = "Lives" }.GetIssues(_graph, node).Single().Message, Does.Contain("Float"));
            Assert.That(_limit.GetIssues(_graph, node), Is.Empty);

            Assert.That(new TimerBehaviour { Seconds = 2f, EventName = "Go" }.Describe(_graph), Is.EqualTo("Timer 2 s → Go"));
            Assert.That(_limit.Describe(_graph), Is.EqualTo("Timer Time → TimeUp"));
            Assert.That(new TimerBehaviour { Seconds = 300f, EventName = "TimeUp", TimeLeft = "Time" }.Describe(_graph), Is.EqualTo("Timer 300 s → TimeUp (Time)"));
        }

        private string Current => _runner.Current?.Title;

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private void Connect(NodeData from, string eventName, NodeData to)
        {
            var raised = Add(new EventNode { EventName = eventName });
            Connect(from, raised);
            Connect(raised, to);
        }
    }
}
