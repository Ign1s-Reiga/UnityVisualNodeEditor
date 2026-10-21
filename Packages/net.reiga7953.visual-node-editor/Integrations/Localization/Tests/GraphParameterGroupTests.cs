using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Formatting;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Reiga.VisualNodeEditor.Localization.Tests
{
    /// <summary>
    /// グラフのパラメータ全部を Smart Strings で読む（<see cref="GraphParameterGroup"/>）。
    /// 文字列は、Localization の既定の SmartFormatter で、Global Variables Group（<see cref="VariablesGroupAsset"/>）に <c>graph</c> として入れて作る。
    /// </summary>
    public sealed class GraphParameterGroupTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private GraphParameterGroup _group;
        private VariablesGroupAsset _variables;
        private SmartFormatter _formatter;

        [SetUp]
        public void SetUp()
        {
            _graph = LocalizationTestGraph.Create();
            _runner = new GraphRunner(_graph);
            _group = new GraphParameterGroup(_graph);
            _variables = ScriptableObject.CreateInstance<VariablesGroupAsset>();
            _variables.Add("graph", _group);
            _formatter = Smart.CreateDefaultSmartFormat();
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_variables);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void TheText_ShowsTheStartValues_ThenTheRunningOnes()
        {
            Assert.That(Format("Score: {graph.Score}, Speed: {graph.Speed}"), Is.EqualTo("Score: 0, Speed: 1.5"));

            _runner.Start();
            _runner.SetInt("Score", 1234);

            Assert.That(Format("Score: {graph.Score:N0}"), Is.EqualTo("Score: 1,234"), "Smart Strings formats");
        }

        [Test]
        public void ABool_ChoosesItsText()
        {
            Assert.That(Format("{graph.Cleared:Cleared|Not yet}"), Is.EqualTo("Not yet"));

            _runner.Start();
            _runner.SetBool("Cleared", true);

            Assert.That(Format("{graph.Cleared:Cleared|Not yet}"), Is.EqualTo("Cleared"));
        }

        [Test]
        public void AnUnknownName_IsAFormatError()
        {
            Assert.That(_group.TryGetValue("Scroe", out var variable), Is.False);
            Assert.That(variable, Is.Null);
            Assert.Throws<FormattingException>(() => Format("{graph.Scroe}"));
        }

        [Test]
        public void WithoutAGraph_AnyNameIsAccepted_AndFilledOnceARunnerStarts()
        {
            _group.Graph = null;
            Assert.That(Format("[{graph.Score}]"), Is.EqualTo("[]"), "nothing running yet");

            _runner.Start();
            _runner.SetInt("Score", 4);

            Assert.That(Format("[{graph.Score}]"), Is.EqualTo("[4]"));
        }

        [Test]
        public void TheSameName_GivesTheSameVariable()
        {
            Assert.That(_group.TryGetValue("Score", out var first), Is.True);
            Assert.That(_group.TryGetValue("Score", out var second), Is.True);
            Assert.That(_group.TryGetValue("Speed", out var speed), Is.True);

            Assert.That(second, Is.SameAs(first));
            Assert.That(speed, Is.Not.SameAs(first));
            Assert.That(first, Is.TypeOf<GraphParameterVariable>());
        }

        [Test]
        public void ALocalizedString_IsToldWhenAUsedParameterChanges()
        {
            // Localized String と同じく、文字列を作ったときの FormatCache の VariableTriggers を聞く
            FormatCache cache = null;
            var text = _formatter.FormatWithCache(ref cache, "Score: {graph.Score}", CultureInfo.InvariantCulture, new object[] { _variables });
            Assert.That(text, Is.EqualTo("Score: 0"));
            Assert.That(cache.VariableTriggers, Has.Count.EqualTo(1), "only the parameter is a trigger");

            var trigger = cache.VariableTriggers[0];
            var changes = 0;
            void OnChanged(IVariable _) => changes++;
            trigger.ValueChanged += OnChanged;
            try
            {
                _runner.Start();
                changes = 0;
                _runner.SetInt("Score", 5);

                Assert.That(changes, Is.EqualTo(1));
                Assert.That(Format("Score: {graph.Score}"), Is.EqualTo("Score: 5"));
            }
            finally
            {
                trigger.ValueChanged -= OnChanged;
            }
        }

        [Test]
        public void ChangingTheGraph_TellsTheUsedVariables()
        {
            var other = LocalizationTestGraph.Create(score: 5);
            Assert.That(_group.TryGetValue("Score", out var score), Is.True);
            var variable = (GraphParameterVariable)score;
            var changes = 0;
            void OnChanged(IVariable _) => changes++;
            variable.ValueChanged += OnChanged;
            try
            {
                _group.Graph = other;

                Assert.That(changes, Is.EqualTo(1));
                Assert.That(variable.Graph, Is.SameAs(other));
                Assert.That(Format("{graph.Score}"), Is.EqualTo("5"));
            }
            finally
            {
                variable.ValueChanged -= OnChanged;
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AGraphChangedInTheInspector_IsUsedOnTheNextLookup_WithoutTelling()
        {
            var other = LocalizationTestGraph.Create(score: 5);
            Assert.That(_group.TryGetValue("Score", out var score), Is.True);
            var variable = (GraphParameterVariable)score;
            var changes = 0;
            void OnChanged(IVariable _) => changes++;
            variable.ValueChanged += OnChanged;
            try
            {
                // インスペクタはセッターを通らずにフィールドを書き換える
                typeof(GraphParameterGroup).GetField("_graph", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_group, other);

                Assert.That(Format("{graph.Score}"), Is.EqualTo("5"));
                Assert.That(variable.Graph, Is.SameAs(other));
                Assert.That(changes, Is.EqualTo(0), "not told while the text is being made");
            }
            finally
            {
                variable.ValueChanged -= OnChanged;
                Object.DestroyImmediate(other);
            }
        }

        private string Format(string format) => _formatter.Format(CultureInfo.InvariantCulture, format, _variables);
    }
}
