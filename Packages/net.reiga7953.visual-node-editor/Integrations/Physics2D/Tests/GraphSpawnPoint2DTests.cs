using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Physics2D.Editor;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Physics2D.Tests
{
    /// <summary>Graph Spawn Point 2D: パラメータが指す地点（無ければ Default）を選び、プレイヤーを置いて勢いを消す。</summary>
    public sealed class GraphSpawnPoint2DTests
    {
        private GameObject _point;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _point = new GameObject("Checkpoint 2");
            _point.transform.position = new Vector3(10f, 4f, 0f);
            _player = new GameObject("Player") { tag = "Player" };
            _player.transform.position = new Vector3(0f, 0f, -1f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_point);
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void Choose_TheNamedPoint_ElseTheDefault()
        {
            var points = new[] { ("Start", true), ("Checkpoint 2", false), ("FromCave", false) };

            Assert.That(GraphSpawnPoint2D.Choose(points, "Checkpoint 2"), Is.EqualTo(1));
            Assert.That(GraphSpawnPoint2D.Choose(points, string.Empty), Is.EqualTo(0), "the start of a stage");
            Assert.That(GraphSpawnPoint2D.Choose(points, "InAnotherScene"), Is.EqualTo(0));
            Assert.That(GraphSpawnPoint2D.Choose(new[] { ("Checkpoint 2", false) }, "Start"), Is.EqualTo(-1), "no default: stay where it is");
            Assert.That(GraphSpawnPoint2D.Choose(new[] { ("checkpoint 2", false) }, "Checkpoint 2"), Is.EqualTo(-1), "names match exactly");
        }

        [Test]
        public void Spawn_PutsThePlayerHere_AndStopsIt()
        {
            var body = _player.AddComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(3f, -20f);
            body.angularVelocity = 90f;
            var point = _point.AddComponent<GraphSpawnPoint2D>();

            point.Spawn(_player);

            Assert.That(_player.transform.position, Is.EqualTo(new Vector3(10f, 4f, -1f)), "x and y of the point, the player's own depth");
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(body.angularVelocity, Is.Zero);
        }

        [Test]
        public void ThePointName_IsTheGameObjectsName_UnlessSet()
        {
            var point = _point.AddComponent<GraphSpawnPoint2D>();
            Assert.That(point.PointName, Is.EqualTo("Checkpoint 2"));

            point.PointName = "CP2";
            Assert.That(point.PointName, Is.EqualTo("CP2"));
        }

        [Test]
        public void FindPlayer_ByTag()
        {
            var point = _point.AddComponent<GraphSpawnPoint2D>();
            Assert.That(point.FindPlayer(), Is.SameAs(_player));

            point.PlayerTag = string.Empty;
            Assert.That(point.FindPlayer(), Is.Null);
        }

        [Test]
        public void ItChoosesOnlyAmongThePointsOfItsGraphAndParameter()
        {
            // ほかのグラフの地点（ミニゲームのグラフも SpawnPoint を使う）は、そのグラフのパラメータで選ばれるので混ぜない
            var main = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var minigame = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var others = new GameObject[3];
            try
            {
                var point = _point.AddComponent<GraphSpawnPoint2D>();
                point.Graph = main;
                var sameGraph = Point(others, 0, "A Start", main, "SpawnPoint");
                var otherGraph = Point(others, 1, "0 Minigame", minigame, "SpawnPoint");
                var otherParameter = Point(others, 2, "B Door", main, "Door");

                Assert.That(point.Candidates(new[] { point, otherGraph, sameGraph, otherParameter }), Is.EqualTo(new[] { sameGraph, point }), "by name");
            }
            finally
            {
                foreach (var other in others)
                {
                    Object.DestroyImmediate(other);
                }

                Object.DestroyImmediate(main);
                Object.DestroyImmediate(minigame);
            }
        }

        [Test]
        public void APointWithoutAGraph_IsGroupedWithTheRunningGraph()
        {
            // グラフを入れた地点と空の地点（実行中のどれか）が混ざっても、同じ Runner を見ていれば 1 つにまとめて選ぶ（両方がプレイヤーを動かさない）
            foreach (var running in GraphRunner.Running.ToList())
            {
                running.Stop();
            }

            var main = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var entry = new EntryNode();
            var playing = new StateNode { Title = "Playing" };
            main.AddNode(entry);
            main.AddNode(playing);
            main.AddEdge(new EdgeData(entry.Id, "out", playing.Id, "in"));
            var runner = new GraphRunner(main);
            var others = new GameObject[1];
            try
            {
                var point = _point.AddComponent<GraphSpawnPoint2D>();
                point.Graph = main;
                var withoutGraph = Point(others, 0, "A Start", null, "SpawnPoint");
                Assert.That(point.Candidates(new[] { point, withoutGraph }), Is.EqualTo(new[] { point }), "nothing running: an empty graph cannot be matched");

                runner.Start();
                Assert.That(point.Candidates(new[] { point, withoutGraph }), Is.EqualTo(new[] { withoutGraph, point }));
                Assert.That(withoutGraph.Candidates(new[] { point, withoutGraph }), Is.EqualTo(new[] { withoutGraph, point }));
            }
            finally
            {
                runner.Stop();
                Object.DestroyImmediate(others[0]);
                Object.DestroyImmediate(main);
            }
        }

        private static GraphSpawnPoint2D Point(GameObject[] made, int index, string name, NodeGraphAsset graph, string parameter)
        {
            made[index] = new GameObject(name);
            var point = made[index].AddComponent<GraphSpawnPoint2D>();
            point.Graph = graph;
            point.Parameter = parameter;
            return point;
        }

        [Test]
        public void Hints_ForTheParameter_AndTheTag()
        {
            Assert.That(Physics2DHints.ForSpawnPoint(true, true, GraphParameterType.String, "SpawnPoint", "Player"), Is.Empty);
            Assert.That(Physics2DHints.ForSpawnPoint(false, false, default, "SpawnPoint", "Player"), Is.Empty, "no graph: any running graph");
            Assert.That(Physics2DHints.ForSpawnPoint(true, false, default, "SpawnPoint", "Player").Single(), Does.Contain("Add a String parameter"));
            Assert.That(Physics2DHints.ForSpawnPoint(true, true, GraphParameterType.Int, "SpawnPoint", "Player").Single(), Does.Contain("Int"));
            Assert.That(Physics2DHints.ForSpawnPoint(true, true, GraphParameterType.String, string.Empty, string.Empty), Has.Count.EqualTo(2));
        }

        [Test]
        public void TheEditorsFieldNames_AreOnTheComponent()
        {
            var serialized = new SerializedObject(_point.AddComponent<GraphSpawnPoint2D>());
            foreach (var name in new[]
                     {
                         GraphSpawnPoint2DEditor.GraphPropertyName, GraphSpawnPoint2DEditor.ParameterPropertyName, GraphSpawnPoint2DEditor.PointNamePropertyName,
                         GraphSpawnPoint2DEditor.PlayerTagPropertyName, GraphSpawnPoint2DEditor.DefaultPropertyName,
                     })
            {
                Assert.That(serialized.FindProperty(name), Is.Not.Null, name);
            }
        }
    }
}
