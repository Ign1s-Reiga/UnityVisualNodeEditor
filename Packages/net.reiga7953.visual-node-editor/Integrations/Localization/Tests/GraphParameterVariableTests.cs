using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Localization.Tests
{
    /// <summary>
    /// グラフのパラメータ 1 つを Smart Strings で読む（<see cref="GraphParameterVariable"/>）。
    /// </summary>
    public sealed class GraphParameterVariableTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private GraphRunner _next;
        private GraphParameterVariable _variable;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _graph = LocalizationTestGraph.Create();
            _runner = new GraphRunner(_graph);
            _variable = new GraphParameterVariable(_graph, "Score");
            _changes = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _variable.ValueChanged -= OnChanged;
            _runner.Stop();
            _next?.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void WithoutARunner_TheGraphStartValueIsUsed()
        {
            _variable.Parameter = "Speed";

            Assert.That(_variable.Value, Is.EqualTo(1.5f));
            Assert.That(_variable.GetSourceValue(null), Is.EqualTo(1.5f));
        }

        [Test]
        public void WhileRunning_TheRunnerValueIsUsed()
        {
            _runner.Start();
            _runner.SetInt("Score", 12);

            Assert.That(_variable.Value, Is.EqualTo(12));
        }

        [Test]
        public void AnUnknownParameter_HasNoValue()
        {
            _variable.Parameter = "Scroe";
            Assert.That(_variable.Value, Is.Null, "no runner: not in the graph");

            _runner.Start();
            Assert.That(_variable.Value, Is.Null, "running: not in the runner");
        }

        [Test]
        public void WhileUsed_ItTellsWhenTheValueChanges()
        {
            _variable.ValueChanged += OnChanged;

            _runner.Start();
            Assert.That(_changes, Is.GreaterThanOrEqualTo(1), "a runner of the graph started");

            _changes = 0;
            _runner.SetInt("Score", 3);
            Assert.That(_changes, Is.EqualTo(1));

            _runner.SetFloat("Speed", 2f);
            Assert.That(_changes, Is.EqualTo(1), "other parameters do not count");

            _runner.Stop();
            Assert.That(_changes, Is.EqualTo(2), "the runner it read stopped (back to the start value)");
            Assert.That(_variable.Value, Is.EqualTo(0));
        }

        [Test]
        public void WhenNothingListens_ItLetsGoOfTheRunner()
        {
            _runner.Start();
            Assert.That(_variable.IsListening, Is.False, "not used yet");

            _variable.ValueChanged += OnChanged;
            Assert.That(_variable.IsListening, Is.True);

            _variable.ValueChanged -= OnChanged;
            Assert.That(_variable.IsListening, Is.False);

            _runner.SetInt("Score", 3);
            Assert.That(_changes, Is.EqualTo(0));
            Assert.That(_variable.Value, Is.EqualTo(3), "the value is still read on demand");
        }

        [Test]
        public void AStoppedRunner_IsReplacedByTheNextOne()
        {
            _runner.Start();
            _next = new GraphRunner(_graph);
            _next.Start();
            _variable.ValueChanged += OnChanged;

            _runner.Stop();
            _changes = 0;
            _next.SetInt("Score", 7);

            Assert.That(_changes, Is.EqualTo(1), "follows the runner still running");
            Assert.That(_variable.Value, Is.EqualTo(7));
        }

        [Test]
        public void ChangingTheParameter_TellsTheText()
        {
            _variable.ValueChanged += OnChanged;

            _variable.Parameter = "Speed";
            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(_variable.Value, Is.EqualTo(1.5f));

            _variable.Parameter = "Speed";
            Assert.That(_changes, Is.EqualTo(1), "the same name changes nothing");
        }

        [Test]
        public void ChangingTheGraph_TellsTheText_AndFollowsItsRunner()
        {
            var other = LocalizationTestGraph.Create(score: 5);
            var otherRunner = new GraphRunner(other);
            try
            {
                _variable.ValueChanged += OnChanged;

                _variable.Graph = other;
                Assert.That(_changes, Is.EqualTo(1));
                Assert.That(_variable.Value, Is.EqualTo(5), "the other graph's start value");

                otherRunner.Start();
                _changes = 0;
                otherRunner.SetInt("Score", 9);
                Assert.That(_changes, Is.EqualTo(1));
                Assert.That(_variable.Value, Is.EqualTo(9));
            }
            finally
            {
                otherRunner.Stop();
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void WithoutAGraph_AnyRunningGraphIsUsed()
        {
            _variable.Graph = null;
            _variable.ValueChanged += OnChanged;
            Assert.That(_variable.Value, Is.Null, "nothing running yet");

            _runner.Start();

            Assert.That(_changes, Is.GreaterThanOrEqualTo(1));
            Assert.That(_variable.Value, Is.EqualTo(0));
        }

        [Test]
        public void TheVariables_CanBeAddedFromTheLocalVariablesMenu()
        {
            // Localization の追加のメニューは、抽象でもジェネリックでもない IVariable の型をすべて出し、引数の無いコンストラクタで作る
            var types = TypeCache.GetTypesDerivedFrom<IVariable>()
                .Where(type => type.Assembly == typeof(GraphParameterVariable).Assembly)
                .ToList();

            Assert.That(types, Is.EquivalentTo(new[] { typeof(GraphParameterVariable), typeof(GraphParameterGroup) }));
            foreach (var type in types)
            {
                Assert.That(type.IsSerializable, Is.True, type.Name);
                Assert.That(Activator.CreateInstance(type), Is.Not.Null, type.Name);
                Assert.That(Attribute.IsDefined(type, typeof(DisplayNameAttribute)), Is.True, type.Name);
            }
        }

        private void OnChanged(IVariable variable)
        {
            Assert.That(variable, Is.SameAs(_variable));
            _changes++;
        }
    }
}
