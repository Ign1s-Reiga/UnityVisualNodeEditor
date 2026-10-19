using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Physics2D.Editor;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Physics2D.Tests
{
    /// <summary>
    /// Graph Trigger 2D: タグの合う物が触れたら、パラメータを変えてイベントを Raise する。Once・Deactivate、Runner が無いとき。
    /// Entry → Playing ─StageClear→ Clear（Coins）
    /// </summary>
    public sealed class GraphTrigger2DTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _coins;
        private GraphRunner _runner;
        private GameObject _goal;
        private GameObject _player;
        private GraphTrigger2D _trigger;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _coins = new GraphParameter("Coins", GraphParameterType.Int);
            _graph.AddParameter(_coins);
            var entry = new EntryNode();
            var playing = new StateNode { Title = "Playing" };
            var stageClear = new EventNode { EventName = "StageClear" };
            var clear = new StateNode { Title = "Clear" };
            foreach (var node in new NodeData[] { entry, playing, stageClear, clear })
            {
                _graph.AddNode(node);
            }

            _graph.AddEdge(new EdgeData(entry.Id, "out", playing.Id, "in"));
            _graph.AddEdge(new EdgeData(playing.Id, "out", stageClear.Id, "in"));
            _graph.AddEdge(new EdgeData(stageClear.Id, "out", clear.Id, "in"));
            _runner = new GraphRunner(_graph);

            _goal = new GameObject("Goal");
            _goal.AddComponent<BoxCollider2D>().isTrigger = true;
            _trigger = _goal.AddComponent<GraphTrigger2D>();
            _trigger.Graph = _graph;
            _player = new GameObject("Player") { tag = "Player" };
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_goal);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void ThePlayerTouching_ChangesThenRaises()
        {
            _trigger.Changes.Add(new ParameterChange(_coins.Id, ParameterChangeOperation.Add) { IntValue = 10 });
            _trigger.EventName = "StageClear";
            _runner.Start();

            Assert.That(_trigger.Touch(_player.AddComponent<BoxCollider2D>()), Is.True);

            Assert.That(_runner.GetInt("Coins"), Is.EqualTo(10));
            Assert.That(_runner.Current.Title, Is.EqualTo("Clear"));
        }

        [Test]
        public void OtherTags_DoNotSetItOff_UnlessTheTagIsEmpty()
        {
            _trigger.EventName = "StageClear";
            _runner.Start();
            var enemy = new GameObject("Enemy");
            try
            {
                Assert.That(_trigger.Touch(enemy.AddComponent<BoxCollider2D>()), Is.False);
                Assert.That(_runner.Current.Title, Is.EqualTo("Playing"));

                _trigger.TargetTag = string.Empty;
                Assert.That(_trigger.Touch(enemy.GetComponent<BoxCollider2D>()), Is.True, "no tag: anything");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void TheTagOfTheRigidbody_Counts()
        {
            // プレイヤーのコライダーが子にあっても、Rigidbody 2D の GameObject のタグで反応する
            var feet = new GameObject("Feet");
            try
            {
                Assert.That(_trigger.Matches(feet, _player), Is.True);
                Assert.That(_trigger.Matches(_player, null), Is.True);
                Assert.That(_trigger.Matches(feet, null), Is.False);
                Assert.That(_trigger.Matches(null, null), Is.False);

                _trigger.TargetTag = "NoSuchTag";
                Assert.That(_trigger.Matches(_player, null), Is.False, "an undefined tag is just a name that nothing has");
            }
            finally
            {
                Object.DestroyImmediate(feet);
            }
        }

        [Test]
        public void Once_AndDeactivate()
        {
            _trigger.Changes.Add(new ParameterChange(_coins.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            _trigger.Once = true;
            _runner.Start();

            Assert.That(_trigger.Fire(), Is.True);
            Assert.That(_trigger.Fire(), Is.False);
            Assert.That(_runner.GetInt("Coins"), Is.EqualTo(1));

            var coin = new GameObject("Coin").AddComponent<GraphTrigger2D>();
            try
            {
                coin.Graph = _graph;
                coin.Deactivate = true;
                Assert.That(coin.Fire(), Is.True);
                Assert.That(coin.gameObject.activeSelf, Is.False, "picked up");
            }
            finally
            {
                Object.DestroyImmediate(coin.gameObject);
            }
        }

        [Test]
        public void WithoutARunner_NothingIsUsedUp()
        {
            _trigger.Once = true;
            _trigger.Deactivate = true;

            Assert.That(_trigger.Fire(), Is.False);
            Assert.That(_goal.activeSelf, Is.True);

            _runner.Start();
            Assert.That(_trigger.Fire(), Is.True, "Once was not used up");
        }

        [Test]
        public void APlayerWithTwoColliders_SetsItOffOnce()
        {
            // 体と足のコライダーを持つプレイヤーが触れても、1 回だけ（トゲで 2 つ減らない）
            _trigger.Changes.Add(new ParameterChange(_coins.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            _runner.Start();
            var body = _player.AddComponent<BoxCollider2D>();
            var feet = _player.AddComponent<CircleCollider2D>();

            Assert.That(_trigger.Began(body), Is.True);
            Assert.That(_trigger.Began(feet), Is.False);
            Assert.That(_trigger.Ended(feet), Is.False);
            Assert.That(_trigger.Ended(body), Is.False);
            Assert.That(_runner.GetInt("Coins"), Is.EqualTo(1));

            Assert.That(_trigger.Began(feet), Is.True, "touching again after leaving counts again");
            Assert.That(_runner.GetInt("Coins"), Is.EqualTo(2));
        }

        [Test]
        public void Exit_SetsItOff_WhenTheLastColliderLeaves()
        {
            _trigger.When = GraphTriggerMoment.Exit;
            _trigger.Changes.Add(new ParameterChange(_coins.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            _runner.Start();
            var body = _player.AddComponent<BoxCollider2D>();
            var feet = _player.AddComponent<CircleCollider2D>();

            Assert.That(_trigger.Began(body), Is.False);
            Assert.That(_trigger.Began(feet), Is.False);
            Assert.That(_trigger.Ended(feet), Is.False, "the body still touches");
            Assert.That(_trigger.Ended(body), Is.True);
            Assert.That(_trigger.Ended(body), Is.False, "an exit that was not counted is ignored");
            Assert.That(_runner.GetInt("Coins"), Is.EqualTo(1));
        }

        [Test]
        public void Hints_ForTheCollider_AndTheTag()
        {
            Assert.That(Physics2DHints.ForTrigger(true, "Player"), Is.Empty);
            Assert.That(Physics2DHints.ForTrigger(false, "Player").Single(), Does.Contain("Collider 2D"));
            Assert.That(Physics2DHints.ForTrigger(true, string.Empty).Single(), Does.Contain("anything"));
        }

        [Test]
        public void ChildColliders_UnderARigidbody_Count()
        {
            // 物理の通知は、コライダーの Rigidbody 2D の GameObject にも届くので、子のコライダーでよい
            var root = new GameObject("Hazard");
            var child = new GameObject("Spikes");
            try
            {
                child.transform.SetParent(root.transform);
                child.AddComponent<BoxCollider2D>();
                Assert.That(GraphTrigger2DEditor.HasCollider(root), Is.False, "no Rigidbody 2D: the child's messages do not reach the root");

                root.AddComponent<Rigidbody2D>();
                Assert.That(GraphTrigger2DEditor.HasCollider(root), Is.True);
                Assert.That(GraphTrigger2DEditor.HasCollider(child), Is.True);

                child.SetActive(false);
                Assert.That(GraphTrigger2DEditor.HasCollider(root), Is.False, "an inactive child collider sends nothing");

                child.SetActive(true);
                child.AddComponent<Rigidbody2D>();
                Assert.That(GraphTrigger2DEditor.HasCollider(root), Is.False, "a child with its own Rigidbody 2D sends to itself, not to the root");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheEditorsFieldNames_AreOnTheComponent()
        {
            var serialized = new SerializedObject(_trigger);
            foreach (var name in new[]
                     {
                         GraphTrigger2DEditor.GraphPropertyName, GraphTrigger2DEditor.TagPropertyName, GraphTrigger2DEditor.WhenPropertyName,
                         GraphTrigger2DEditor.ChangesPropertyName, GraphTrigger2DEditor.EventNamePropertyName, GraphTrigger2DEditor.OncePropertyName,
                         GraphTrigger2DEditor.DeactivatePropertyName,
                     })
            {
                Assert.That(serialized.FindProperty(name), Is.Not.Null, name);
            }
        }
    }
}
