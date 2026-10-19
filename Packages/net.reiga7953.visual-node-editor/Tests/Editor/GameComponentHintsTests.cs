using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ゲーム向けのコンポーネントのインスペクタ: 注意（<see cref="GameComponentHints"/>）と、選べるノード（<see cref="ActivatorNodeChoices"/>）。</summary>
    public sealed class GameComponentHintsTests
    {
        [Test]
        public void Activator_AsksForTheGraphNodeAndTargets()
        {
            Assert.That(GameComponentHints.ForActivator(false, string.Empty, false, 0, false), Has.Count.EqualTo(2));
            Assert.That(GameComponentHints.ForActivator(true, string.Empty, false, 1, false).Single(), Does.Contain("Pick the node"));
            Assert.That(GameComponentHints.ForActivator(true, "gone", false, 1, false).Single(), Does.Contain("no longer"));
            Assert.That(GameComponentHints.ForActivator(true, "id", true, 1, true).Single(), Does.Contain("this GameObject or one of its parents"));
            Assert.That(GameComponentHints.ForActivator(true, "id", true, 1, false), Is.Empty);
        }

        [Test]
        public void ParameterText_AsksForTextAndAParameterOfTheGraph()
        {
            Assert.That(GameComponentHints.ForParameterText(false, null, "Score").Single(), Does.Contain("No text"));
            Assert.That(GameComponentHints.ForParameterText(true, null, string.Empty).Single(), Does.Contain("assign the graph"));
            Assert.That(GameComponentHints.ForParameterText(true, new[] { "Score" }, "Lives").Single(), Does.Contain("'Lives' is not a parameter"));
            Assert.That(GameComponentHints.ForParameterText(true, new[] { "Score" }, "Score"), Is.Empty);
            Assert.That(GameComponentHints.ForParameterText(true, null, "Score"), Is.Empty, "without a graph the name cannot be checked");
        }

        [Test]
        public void EventKey_AsksForAKeyAndAnEventOfTheGraph()
        {
            Assert.That(GameComponentHints.ForEventKey(null, "Pause", GraphEventKeyAction.Raise, KeyCode.None).Single(), Does.Contain("key"));
            Assert.That(GameComponentHints.ForEventKey(null, string.Empty, GraphEventKeyAction.Raise, KeyCode.Escape).Single(), Does.Contain("Choose the event"));
            Assert.That(GameComponentHints.ForEventKey(new[] { "Pause" }, "Go", GraphEventKeyAction.Raise, KeyCode.Space).Single(), Does.Contain("'Go'"));
            Assert.That(GameComponentHints.ForEventKey(new[] { "Pause" }, string.Empty, GraphEventKeyAction.Advance, KeyCode.Space), Is.Empty);
            Assert.That(GameComponentHints.ForEventKey(new string[0], "Pause", GraphEventKeyAction.Raise, KeyCode.Escape).Single(), Does.Contain("no events yet"),
                "a graph without Event nodes: the key does nothing either");
        }

        [Test]
        public void ActivatorChoices_AreNodesTheGraphCanStayOn_WithTheirPlace()
        {
            var game = new GameComponentTestGraph();
            try
            {
                var choices = ActivatorNodeChoices.Get(game.Graph);

                Assert.That(choices.Select(choice => choice.Value), Is.EqualTo(new[] { "Game Over", "Round", "Round › Paused", "Round › Playing" }));
                Assert.That(choices.Single(choice => choice.Value == "Round › Paused").Key, Is.EqualTo(game.Paused.Id));
                Assert.That(ActivatorNodeChoices.Get(null), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(game.Graph);
            }
        }

        [Test]
        public void ActivatorChoices_NumberNodesWithTheSameLabel_SoEachCanBePicked()
        {
            // DropdownField は表示の文字で選んだ位置を決めるので、同じ文字だと 2 つ目を選べない
            var game = new GameComponentTestGraph();
            try
            {
                var twin = new StateNode { Title = "Playing", ParentId = game.Round.Id };
                game.Graph.AddNode(twin);

                var choices = ActivatorNodeChoices.Get(game.Graph);

                Assert.That(choices.Single(choice => choice.Value == "Round › Playing").Key, Is.EqualTo(game.Playing.Id));
                Assert.That(choices.Single(choice => choice.Value == "Round › Playing (2)").Key, Is.EqualTo(twin.Id));
            }
            finally
            {
                Object.DestroyImmediate(game.Graph);
            }
        }

        [Test]
        public void DropdownLabels_NumberRepeats_WithoutClashingWithExistingLabels()
        {
            Assert.That(DropdownLabels.MakeUnique(new[] { "State", "Playing", "State", "State (2)", "State" }),
                Is.EqualTo(new[] { "State", "Playing", "State (3)", "State (2)", "State (4)" }));
            Assert.That(DropdownLabels.MakeUnique(new[] { "(missing)", "(unnamed)", "(unnamed)" }),
                Is.EqualTo(new[] { "(missing)", "(unnamed)", "(unnamed) (2)" }));
            Assert.That(DropdownLabels.MakeUnique(new string[0]), Is.Empty);
        }

        [Test]
        public void EditorFieldNames_Exist()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var name in new[] { GraphNodeActivatorEditor.GraphPropertyName, GraphNodeActivatorEditor.NodeIdPropertyName, GraphNodeActivatorEditor.TargetsPropertyName })
            {
                Assert.That(typeof(GraphNodeActivator).GetField(name, flags), Is.Not.Null, name);
            }

            foreach (var name in new[] { GraphParameterTextEditor.GraphPropertyName, GraphParameterTextEditor.ParameterPropertyName, GraphParameterTextEditor.FormatPropertyName })
            {
                Assert.That(typeof(GraphParameterText).GetField(name, flags), Is.Not.Null, name);
            }

            foreach (var name in new[]
                     {
                         GraphEventKeyEditor.GraphPropertyName, GraphEventKeyEditor.KeyPropertyName, GraphEventKeyEditor.ActionPropertyName,
                         GraphEventKeyEditor.EventNamePropertyName,
                     })
            {
                Assert.That(typeof(GraphEventKey).GetField(name, flags), Is.Not.Null, name);
            }
        }
    }
}
