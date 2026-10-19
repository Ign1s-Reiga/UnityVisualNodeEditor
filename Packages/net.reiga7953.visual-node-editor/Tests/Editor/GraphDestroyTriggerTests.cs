using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// Graph Destroy Trigger（と共通の GraphReactions）: パラメータを先に変えてからイベントを Raise する。シーンを閉じるときは起こさない。
    /// コンポーネントの変更の欄は、Graph の欄のグラフの Blackboard から選ぶ。
    /// Entry → Fight ─BossDefeated→ Clear（Lives・Enemies）
    /// </summary>
    public sealed class GraphDestroyTriggerTests
    {
        private NodeGraphAsset _graph;
        private GraphParameter _enemies;
        private GraphRunner _runner;
        private GameObject _boss;

        [SetUp]
        public void SetUp()
        {
            // グラフを決めないと「実行中のどれか」へ送るので、ほかのテストが残した Runner を止めておく
            foreach (var running in GraphRunner.Running.ToList())
            {
                running.Stop();
            }

            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _enemies = new GraphParameter("Enemies", GraphParameterType.Int) { IntValue = 3 };
            _graph.AddParameter(_enemies);
            var entry = new EntryNode();
            var fight = new StateNode { Title = "Fight" };
            var defeated = new EventNode { EventName = "BossDefeated" };
            var clear = new StateNode { Title = "Clear" };
            foreach (var node in new NodeData[] { entry, fight, defeated, clear })
            {
                _graph.AddNode(node);
            }

            _graph.AddEdge(new EdgeData(entry.Id, "out", fight.Id, "in"));
            _graph.AddEdge(new EdgeData(fight.Id, "out", defeated.Id, "in"));
            _graph.AddEdge(new EdgeData(defeated.Id, "out", clear.Id, "in"));
            _runner = new GraphRunner(_graph);
            _boss = new GameObject("Boss");
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_boss);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Fire_ChangesTheParameters_ThenRaises()
        {
            var trigger = _boss.AddComponent<GraphDestroyTrigger>();
            trigger.Graph = _graph;
            trigger.Changes.Add(new ParameterChange(_enemies.Id, ParameterChangeOperation.Add) { IntValue = -1 });
            trigger.EventName = "BossDefeated";
            _runner.Start();

            Assert.That(trigger.Fire(), Is.True);

            Assert.That(_runner.GetInt("Enemies"), Is.EqualTo(2));
            Assert.That(_runner.Current.Title, Is.EqualTo("Clear"));
        }

        [Test]
        public void Fire_DoesNothing_WithoutARunner()
        {
            var trigger = _boss.AddComponent<GraphDestroyTrigger>();
            trigger.Graph = _graph;
            trigger.EventName = "BossDefeated";

            Assert.That(trigger.Fire(), Is.False, "the graph is not running");
        }

        [Test]
        public void WithoutAGraph_OnlyTheEventIsRaised()
        {
            var trigger = _boss.AddComponent<GraphDestroyTrigger>();
            trigger.Changes.Add(new ParameterChange(_enemies.Id, ParameterChangeOperation.Add) { IntValue = -1 });
            trigger.EventName = "BossDefeated";
            _runner.Start();

            trigger.Fire();

            Assert.That(_runner.GetInt("Enemies"), Is.EqualTo(3), "parameters are chosen from the graph, so they need it");
            Assert.That(_runner.Current.Title, Is.EqualTo("Clear"));
        }

        [TestCase(true, false, true)]
        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        public void ClosingTheScene_DoesNotCount(bool sceneLoaded, bool quitting, bool fires)
        {
            Assert.That(GraphDestroyTrigger.ShouldFire(sceneLoaded, quitting), Is.EqualTo(fires));
        }

        // (コンポーネントが有効か, activeSelf, activeInHierarchy) → 数えるか
        [TestCase(true, false, false, true, TestName = "Turned off itself counts")]
        [TestCase(true, true, true, true, TestName = "Only the component removed counts")]
        [TestCase(true, true, false, false, TestName = "A parent turned off or destroyed does not count")]
        [TestCase(false, true, true, false, TestName = "The component switched off does not count")]
        public void WhatCountsAsGone(bool componentEnabled, bool activeSelf, bool activeInHierarchy, bool counts)
        {
            Assert.That(GraphDestroyTrigger.CountsAsGone(componentEnabled, activeSelf, activeInHierarchy), Is.EqualTo(counts));
        }

        [Test]
        public void ItCounts_OnceUntilItIsBack()
        {
            var trigger = _boss.AddComponent<GraphDestroyTrigger>();
            trigger.Graph = _graph;
            trigger.Changes.Add(new ParameterChange(_enemies.Id, ParameterChangeOperation.Add) { IntValue = -1 });
            _runner.Start();

            Assert.That(trigger.HandleDisabled(true, true, false), Is.False, "a section hidden");
            Assert.That(trigger.HandleDisabled(true, false, false), Is.True, "turned off itself");
            Assert.That(trigger.HandleDisabled(true, false, false), Is.False, "once until it is back (turned off, then destroyed)");
            Assert.That(_runner.GetInt("Enemies"), Is.EqualTo(2));

            trigger.HandleBack();
            Assert.That(trigger.HandleDisabled(true, false, false), Is.True, "a pooled enemy that came back counts again");
            Assert.That(_runner.GetInt("Enemies"), Is.EqualTo(1));
        }

        [Test]
        public void Unity_CallsOnDisable_WithTheStateTheTriggerReads()
        {
            // Graph Destroy Trigger の前提: OnDisable のときの状態で、なぜ無効になったかが分かる。
            // Unity は壊すとき、壊す GameObject を無効にしてから（activeSelf が false）OnDisable を呼ぶので、自分が無効になったのと同じに見える。
            // 親ごと壊したときの子は、親が無効になったのと同じに見える。もう無効な物を壊しても呼ばない
            DisableStateProbe.Seen.Clear();
            var section = new GameObject("Section");
            var enemy = new GameObject("Enemy");
            try
            {
                enemy.transform.SetParent(section.transform);
                var probe = enemy.AddComponent<DisableStateProbe>();

                section.SetActive(false);
                section.SetActive(true);
                enemy.SetActive(false);
                enemy.SetActive(true);
                probe.enabled = false;
                probe.enabled = true;
                Object.DestroyImmediate(enemy);

                var inRoom = new GameObject("Enemy in a room");
                inRoom.transform.SetParent(section.transform);
                inRoom.AddComponent<DisableStateProbe>();
                Object.DestroyImmediate(section);

                Assert.That(DisableStateProbe.Seen, Is.EqualTo(new[] { (true, true, false), (true, false, false), (false, true, true), (true, false, false), (true, true, false) }),
                    "parent hidden, itself turned off, component switched off, itself destroyed, its parent destroyed");
                Assert.That(DisableStateProbe.Seen.Select(seen => GraphDestroyTrigger.CountsAsGone(seen.Enabled, seen.ActiveSelf, seen.ActiveInHierarchy)),
                    Is.EqualTo(new[] { false, true, false, true, false }));

                section = new GameObject("Hidden section");

                var hidden = new GameObject("Hidden enemy");
                hidden.transform.SetParent(section.transform);
                hidden.AddComponent<DisableStateProbe>();
                section.SetActive(false);
                var seenBefore = DisableStateProbe.Seen.Count;
                Object.DestroyImmediate(section);
                Assert.That(DisableStateProbe.Seen.Count, Is.EqualTo(seenBefore), "destroying what is already hidden calls nothing, so it does not count");
            }
            finally
            {
                if (enemy != null)
                {
                    Object.DestroyImmediate(enemy);
                }

                if (section != null)
                {
                    Object.DestroyImmediate(section);
                }

                DisableStateProbe.Seen.Clear();
            }
        }

        [Test]
        public void TheChangeRows_ChooseFromTheComponentsGraph()
        {
            var trigger = _boss.AddComponent<GraphDestroyTrigger>();
            trigger.Changes.Add(new ParameterChange());
            var serialized = new SerializedObject(trigger);
            var row = serialized.FindProperty(GraphDestroyTriggerEditor.ChangesPropertyName).GetArrayElementAtIndex(0);

            var withoutGraph = new ParameterChangeDrawer().CreatePropertyGUI(row);
            Assert.That(withoutGraph.Q<Label>()?.text, Is.EqualTo(ParameterChangeDrawer.NoGraphMessage));

            trigger.Graph = _graph;
            serialized.Update();
            var withGraph = new ParameterChangeDrawer().CreatePropertyGUI(row);
            Assert.That(withGraph.Q<DropdownField>().choices, Does.Contain("Enemies"));
        }

        [Test]
        public void Hints_SayWhatIsMissing()
        {
            Assert.That(GameComponentHints.ForReaction(null, string.Empty, 0, null), Has.Count.EqualTo(1), "nothing to do");
            Assert.That(GameComponentHints.ForReaction(null, "BossDefeated", 1, null).Single(), Does.Contain("Set the graph"));
            Assert.That(GameComponentHints.ForReaction(new[] { "BossDefeated" }, "Typo", 0, null).Single(), Does.Contain("'Typo'"));
            Assert.That(GameComponentHints.ForReaction(new[] { "BossDefeated" }, "BossDefeated", 1, new[] { "its parameter was deleted" }).Single(),
                Does.Contain("deleted"));
            Assert.That(GameComponentHints.ForReaction(new[] { "BossDefeated" }, "BossDefeated", 0, null), Is.Empty);
            Assert.That(GameComponentHints.ForReaction(new string[0], "BossDefeated", 0, null).Single(), Does.Contain("no events yet"),
                "a graph without Event nodes cannot move on either");
        }

        [Test]
        public void TheEditorsFieldNames_AreOnTheComponent()
        {
            var serialized = new SerializedObject(_boss.AddComponent<GraphDestroyTrigger>());
            Assert.That(serialized.FindProperty(GraphDestroyTriggerEditor.GraphPropertyName), Is.Not.Null);
            Assert.That(serialized.FindProperty(GraphDestroyTriggerEditor.ChangesPropertyName)?.isArray, Is.True);
            Assert.That(serialized.FindProperty(GraphDestroyTriggerEditor.EventNamePropertyName), Is.Not.Null);
        }
    }
}
