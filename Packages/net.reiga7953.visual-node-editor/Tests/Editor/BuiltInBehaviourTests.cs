using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Behaviours;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// 組み込みの振る舞い: Set Parameter（ノードに入ったらパラメータを変える）と Time Scale（ノードにいる間 Time.timeScale を変える）。
    /// Entry → Ready（Set Parameter）─Pause→ Paused（Time Scale）─Pause→ Ready
    /// </summary>
    public sealed class BuiltInBehaviourTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _score;
        private GraphParameter _level;
        private GraphParameter _speed;
        private GraphParameter _name;
        private GraphParameter _flag;
        private StateNode _ready;
        private StateNode _paused;
        private GraphRunner _runner;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Behaviours";
            _score = new GraphParameter("Score", GraphParameterType.Int) { IntValue = 5 };
            _level = new GraphParameter("Level", GraphParameterType.Int) { IntValue = 1 };
            _speed = new GraphParameter("Speed", GraphParameterType.Float) { FloatValue = 1.5f };
            _name = new GraphParameter("Name", GraphParameterType.String);
            _flag = new GraphParameter("Flag", GraphParameterType.Bool);
            foreach (var parameter in new[] { _score, _level, _speed, _name, _flag })
            {
                _graph.AddParameter(parameter);
            }

            var entry = new EntryNode();
            _ready = new StateNode { Title = "Ready" };
            _paused = new StateNode { Title = "Paused" };
            var pause = new EventNode { EventName = "Pause" };
            var resume = new EventNode { EventName = "Pause" };
            foreach (var node in new NodeData[] { entry, _ready, _paused, pause, resume })
            {
                _graph.AddNode(node);
            }

            _graph.AddEdge(new EdgeData(entry.Id, "out", _ready.Id, "in"));
            _graph.AddEdge(new EdgeData(_ready.Id, "out", pause.Id, "in"));
            _graph.AddEdge(new EdgeData(pause.Id, "out", _paused.Id, "in"));
            _graph.AddEdge(new EdgeData(_paused.Id, "out", resume.Id, "in"));
            _graph.AddEdge(new EdgeData(resume.Id, "out", _ready.Id, "in"));
            _runner = new GraphRunner(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_graph);
            Time.timeScale = _timeScale;
        }

        [Test]
        public void SetParameter_SetsAndAddsWhenTheGraphEntersTheNode()
        {
            var behaviour = new SetParameterBehaviour();
            behaviour.Changes.Add(new ParameterChange(_score.Id) { IntValue = 0 });
            behaviour.Changes.Add(new ParameterChange(_level.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            behaviour.Changes.Add(new ParameterChange(_speed.Id, ParameterChangeOperation.Add) { FloatValue = 0.5f });
            behaviour.Changes.Add(new ParameterChange(_name.Id) { StringValue = "Alice" });
            behaviour.Changes.Add(new ParameterChange(_flag.Id) { BoolValue = true });
            _ready.AddBehaviour(behaviour);

            _runner.Start();

            Assert.That(_runner.GetInt("Score"), Is.EqualTo(0));
            Assert.That(_runner.GetInt("Level"), Is.EqualTo(2));
            Assert.That(_runner.GetFloat("Speed"), Is.EqualTo(2f));
            Assert.That(_runner.GetString("Name"), Is.EqualTo("Alice"));
            Assert.That(_runner.GetBool("Flag"), Is.True);

            // 入り直すたびに行う
            _runner.Raise("Pause");
            _runner.Raise("Pause");
            Assert.That(_runner.GetInt("Level"), Is.EqualTo(3));
        }

        [Test]
        public void SetParameter_ChangesAreSeenByAutomaticEvents()
        {
            // 次のレベルへ: 入るときに Level += 1、その値で自動の Event を選ぶ
            var behaviour = new SetParameterBehaviour();
            behaviour.Changes.Add(new ParameterChange(_level.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            _ready.AddBehaviour(behaviour);
            var toPaused = new EventNode { Trigger = EventTriggerMode.Automatic };
            toPaused.AddCondition(new TransitionCondition(_level.Id, ConditionOperator.GreaterOrEqual) { IntValue = 2 });
            _graph.AddNode(toPaused);
            _graph.AddEdge(new EdgeData(_ready.Id, "out", toPaused.Id, "in"));
            _graph.AddEdge(new EdgeData(toPaused.Id, "out", _paused.Id, "in"));
            _runner = new GraphRunner(_graph);

            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(_paused));
        }

        [Test]
        public void SetParameter_SkipsChangesItCannotMake()
        {
            var missing = new ParameterChange("no-such-id") { IntValue = 3 };
            var addToBool = new ParameterChange(_flag.Id, ParameterChangeOperation.Add);
            _runner.Start();

            Assert.That(missing.ApplyTo(_runner), Is.Not.Null);
            Assert.That(addToBool.ApplyTo(_runner), Is.Not.Null);
            Assert.That(_runner.GetBool("Flag"), Is.False);
            Assert.That(new ParameterChange(_score.Id) { IntValue = 9 }.ApplyTo(_runner), Is.Null);
            Assert.That(_runner.GetInt("Score"), Is.EqualTo(9));
        }

        [Test]
        public void SetParameter_ReportsMissingParametersAndAddsToBoolOrString()
        {
            var behaviour = new SetParameterBehaviour();
            behaviour.Changes.Add(new ParameterChange(_score.Id));
            _ready.AddBehaviour(behaviour);
            Assert.That(GraphValidator.Validate(_graph), Is.Empty);

            behaviour.Changes.Add(new ParameterChange("no-such-id"));
            behaviour.Changes.Add(new ParameterChange(_name.Id, ParameterChangeOperation.Add));
            behaviour.Changes.Add(new ParameterChange());

            var issues = GraphValidator.Validate(_graph);
            Assert.That(issues.Count(issue => issue.Severity == GraphIssueSeverity.Error && issue.NodeId == _ready.Id), Is.EqualTo(3));
        }

        [Test]
        public void SetParameter_DescribesItsChanges()
        {
            var behaviour = new SetParameterBehaviour();
            Assert.That(behaviour.Describe(_graph), Is.Null, "no changes: the node shows the behaviour's name");

            behaviour.Changes.Add(new ParameterChange(_score.Id) { IntValue = 0 });
            behaviour.Changes.Add(new ParameterChange(_level.Id, ParameterChangeOperation.Add) { IntValue = -1 });
            behaviour.Changes.Add(new ParameterChange(_speed.Id, ParameterChangeOperation.Add) { FloatValue = 0.5f });
            behaviour.Changes.Add(new ParameterChange(_name.Id) { StringValue = "Alice" });
            behaviour.Changes.Add(new ParameterChange(_flag.Id) { BoolValue = true });
            behaviour.Changes.Add(new ParameterChange("no-such-id") { IntValue = 2 });

            Assert.That(behaviour.Describe(_graph),
                Is.EqualTo("Score = 0, Level -= 1, Speed += 0.5, Name = \"Alice\", Flag = true, (missing) = 2"));
        }

        [Test]
        public void TimeScale_ChangesWhileOnTheNode_AndGoesBackWhenItLeaves()
        {
            Time.timeScale = 0.5f;
            _paused.AddBehaviour(new TimeScaleBehaviour { TimeScale = 0f });
            _runner.Start();

            _runner.Raise("Pause");
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            _runner.Raise("Pause");
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "back to the value before it entered");

            _runner.Raise("Pause");
            _runner.Stop();
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "stopping also gives it back");
        }

        [Test]
        public void BuiltInBehaviours_AreListedUnderTheirMenuNames()
        {
            Assert.That(NodeBehaviourCatalog.GetMenuPath(typeof(SetParameterBehaviour)), Is.EqualTo("Visual Node Editor/Set Parameter"));
            Assert.That(NodeBehaviourCatalog.GetMenuPath(typeof(TimeScaleBehaviour)), Is.EqualTo("Visual Node Editor/Time Scale"));
            Assert.That(NodeDisplay.GetBehaviourName(typeof(SetParameterBehaviour)), Is.EqualTo("Set Parameter"));
            Assert.That(NodeBehaviourCatalog.GetTypes(), Does.Contain(typeof(SetParameterBehaviour)));
            Assert.That(NodeBehaviourCatalog.GetTypes(), Does.Contain(typeof(TimeScaleBehaviour)));
            Assert.That(NodeBehaviourCatalog.IsSaveable(typeof(SetParameterBehaviour)) && NodeBehaviourCatalog.IsSaveable(typeof(TimeScaleBehaviour)), Is.True);
        }

        [Test]
        public void Nodes_ShowTheBehavioursDescriptions()
        {
            var setScore = new SetParameterBehaviour();
            setScore.Changes.Add(new ParameterChange(_score.Id));
            _ready.AddBehaviour(setScore);
            _ready.AddBehaviour(new TimeScaleBehaviour());

            Assert.That(NodeDisplay.GetBehavioursText(_ready.Behaviours, _graph), Is.EqualTo("Score = 0, Time Scale 0"));
            Assert.That(NodeDisplay.GetBehavioursText(new NodeBehaviour[] { new SetParameterBehaviour() }), Is.EqualTo("Set Parameter"));
        }

        [Test]
        public void ParameterChangeDrawer_FieldNamesExist()
        {
            var type = typeof(ParameterChange);
            foreach (var name in new[]
                     {
                         ParameterChangeDrawer.ParameterIdPropertyName, ParameterChangeDrawer.OperationPropertyName,
                         ParameterChangeDrawer.BoolValuePropertyName, ParameterChangeDrawer.IntValuePropertyName,
                         ParameterChangeDrawer.FloatValuePropertyName, ParameterChangeDrawer.StringValuePropertyName,
                     })
            {
                Assert.That(type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), Is.Not.Null, name);
            }
        }
    }
}
