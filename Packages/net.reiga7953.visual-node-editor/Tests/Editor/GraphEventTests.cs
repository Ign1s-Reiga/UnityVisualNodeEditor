using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphEventTests
    {
        private NodeGraphAsset _graph;
        private NodeData _play;

        [SetUp]
        public void SetUp()
        {
            // Entry → Play(State) ─OnBossDefeated→ Result(State)、Play ─CoinPicked（出力無し: 通知のみ）
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var entry = Add(new EntryNode());
            _play = Add(new StateNode { Title = "Play" });
            var boss = Add(new EventNode { EventName = "OnBossDefeated" });
            var coin = Add(new EventNode { EventName = "CoinPicked" });
            var result = Add(new StateNode { Title = "Result" });
            Connect(entry, _play);
            Connect(_play, boss);
            Connect(_play, coin);
            Connect(boss, result);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void EventTriggered_FiresForPassedAndNotificationEvents()
        {
            var runner = new GraphRunner(_graph);
            var triggered = new List<string>();
            runner.EventTriggered += triggered.Add;
            runner.Start();

            runner.Raise("CoinPicked");
            runner.Raise("OnBossDefeated");
            runner.Stop();

            Assert.That(triggered, Is.EqualTo(new[] { "CoinPicked", "OnBossDefeated" }));
        }

        [Test]
        public void On_CallsOnlyMatchingHandlerAndOffRemovesIt()
        {
            var runner = new GraphRunner(_graph);
            var coins = 0;
            var bosses = 0;
            void OnCoin() => coins++;
            runner.On("CoinPicked", OnCoin);
            runner.On("OnBossDefeated", () => bosses++);
            runner.Start();

            runner.Raise("CoinPicked");
            runner.Raise("CoinPicked");
            runner.Off("CoinPicked", OnCoin);
            runner.Raise("CoinPicked");
            runner.Stop();

            Assert.That(coins, Is.EqualTo(2));
            Assert.That(bosses, Is.Zero);
        }

        [Test]
        public void Bindings_DispatchByExactName()
        {
            var hits = new List<string>();
            var a = new GraphEventBinding("Coin");
            a.Response.AddListener(() => hits.Add("a"));
            var b = new GraphEventBinding("Coin");
            b.Response.AddListener(() => hits.Add("b"));
            var other = new GraphEventBinding("coin");
            other.Response.AddListener(() => hits.Add("other"));

            var count = GraphEventBinding.Dispatch(new[] { a, null, b, other }, "Coin");

            Assert.That(count, Is.EqualTo(2));
            Assert.That(hits, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(GraphEventBinding.Dispatch(new[] { a }, ""), Is.Zero);
        }

        [Test]
        public void Behaviour_WiresUnityEventsBeforeStart()
        {
            var go = new GameObject("Runner");
            try
            {
                var behaviour = go.AddComponent<GraphRunnerBehaviour>();
                var serialized = new SerializedObject(behaviour);
                serialized.FindProperty("_graph").objectReferenceValue = _graph;
                serialized.FindProperty("_loadScenes").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var entered = new List<string>();
                behaviour.OnNodeEntered.AddListener(entered.Add);
                var boss = new GraphEventBinding("OnBossDefeated");
                var bossCount = 0;
                boss.Response.AddListener(() => bossCount++);
                behaviour.EventBindings.Add(boss);

                behaviour.StartGraph();
                behaviour.Raise("OnBossDefeated");

                Assert.That(entered, Is.EqualTo(new[] { "Entry", "Play", "Event", "Result" }));
                Assert.That(bossCount, Is.EqualTo(1));
                Assert.That(behaviour.Runner.Current.Title, Is.EqualTo("Result"));
                behaviour.StopGraph();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
