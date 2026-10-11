using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>Yarn の変数とグラフの Blackboard の共有（<see cref="GraphVariableStorage"/>）。</summary>
    public sealed class GraphVariableStorageTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private GameObject _gameObject;
        private GraphVariableStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Variables";
            _graph.AddParameter(new GraphParameter("Affection", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Speed", GraphParameterType.Float));
            _graph.AddParameter(new GraphParameter("Met", GraphParameterType.Bool));
            _graph.AddParameter(new GraphParameter("Route", GraphParameterType.String));
            _graph.AddNode(new EntryNode());
            _runner = new GraphRunner(_graph);
            _runner.Start();

            _gameObject = new GameObject("Dialogue System");
            _storage = _gameObject.AddComponent<GraphVariableStorage>();
            _storage.Graph = _graph;
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void YarnWrites_GoToTheBlackboard()
        {
            _storage.SetValue("$Affection", 3f);
            _storage.SetValue("$Speed", 1.5f);
            _storage.SetValue("$Met", true);
            _storage.SetValue("$Route", "Alice");

            Assert.That(_runner.GetInt("Affection"), Is.EqualTo(3));
            Assert.That(_runner.GetFloat("Speed"), Is.EqualTo(1.5f));
            Assert.That(_runner.GetBool("Met"), Is.True);
            Assert.That(_runner.GetString("Route"), Is.EqualTo("Alice"));
        }

        [Test]
        public void YarnReads_ComeFromTheBlackboard_IntsAsNumbers()
        {
            _runner.SetInt("Affection", 4);
            _runner.SetString("Route", "Bob");

            Assert.That(_storage.TryGetValue<float>("$Affection", out var affection), Is.True);
            Assert.That(affection, Is.EqualTo(4f));
            Assert.That(_storage.TryGetValue<object>("$Affection", out var boxed), Is.True);
            Assert.That(boxed, Is.EqualTo(4f), "Yarn numbers are floats");
            Assert.That(_storage.TryGetValue<string>("$Route", out var route), Is.True);
            Assert.That(route, Is.EqualTo("Bob"));
        }

        [Test]
        public void IntParameters_RoundYarnNumbers()
        {
            _storage.SetValue("$Affection", 2.6f);

            Assert.That(_runner.GetInt("Affection"), Is.EqualTo(3));
        }

        [Test]
        public void WrongTypes_AreRefused()
        {
            LogAssert.Expect(LogType.Error, new Regex("Blackboard parameter 'Met' is a Bool"));

            _storage.SetValue("$Met", "yes");

            Assert.That(_runner.GetBool("Met"), Is.False);
        }

        [Test]
        public void VariablesNotInTheBlackboard_GoToExtras_AndAreSaved()
        {
            _storage.SetValue("$visitedShop", true);
            _storage.SetValue("$Yarn.Internal.Visiting.Chapter1", 1f);

            Assert.That(_storage.Contains("$visitedShop"), Is.True);
            Assert.That(_runner.Extras.TryGetValue("$visitedShop", out var visited) && (bool)visited, Is.True);
            Assert.That(_runner.CaptureState().Extras.Select(v => v.Key),
                Is.EquivalentTo(new[] { "$visitedShop", "$Yarn.Internal.Visiting.Chapter1" }));
        }

        [Test]
        public void GraphChanges_ReachYarnListeners()
        {
            var changes = new List<(string, object)>();

            // エディタのテストでは OnEnable が呼ばれないので、有効になったときと同じく Runner を見つけさせる
            Assert.That(_storage.Runner, Is.SameAs(_runner));
            using (_storage.AddChangeListener((name, value) => changes.Add((name, value))))
            {
                _runner.SetInt("Affection", 5);
                _storage.SetValue("$visitedShop", true);
            }

            Assert.That(changes, Is.EqualTo(new List<(string, object)> { ("$Affection", 5f), ("$visitedShop", true) }));
        }

        [Test]
        public void GetAllVariables_ListsTheBlackboardAndYarnOnlyVariables()
        {
            _runner.SetInt("Affection", 2);
            _storage.SetValue("$visitedShop", true);

            var (floats, strings, bools) = _storage.GetAllVariables();

            Assert.That(floats["$Affection"], Is.EqualTo(2f));
            Assert.That(strings.ContainsKey("$Route"), Is.True);
            Assert.That(bools["$visitedShop"], Is.True);
            Assert.That(bools["$Met"], Is.False);
        }

        [Test]
        public void Clear_RemovesYarnOnlyVariables_ButNotTheBlackboard()
        {
            _runner.SetInt("Affection", 2);
            _runner.Extras.SetInt("SomeAddon", 1);
            _storage.SetValue("$visitedShop", true);

            _storage.Clear();

            Assert.That(_storage.Contains("$visitedShop"), Is.False);
            Assert.That(_runner.GetInt("Affection"), Is.EqualTo(2));
            Assert.That(_runner.Extras.Contains("SomeAddon"), Is.True, "only $ variables belong to Yarn");
        }

        [Test]
        public void WithoutARunner_ValuesAreKeptHere()
        {
            _runner.Stop();

            _storage.SetValue("$Affection", 1f);

            Assert.That(_storage.Contains("$Affection"), Is.True);
            Assert.That(_storage.TryGetValue<float>("$Affection", out var value) && value == 1f, Is.True);
        }
    }
}
