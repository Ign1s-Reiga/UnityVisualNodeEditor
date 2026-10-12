using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// UI Toolkit のグラフに繋がるコントロール（Graph Button・Graph Parameter Label・Graph Node Panel）。
    /// ゲームの画面に付いたとき（Connect）・外れたとき（Disconnect）の動きを、パネル無しで確かめる。
    /// </summary>
    public sealed class UIToolkitControlTests
    {
        private GameComponentTestGraph _game;
        private GraphRunner _runner;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _game = new GameComponentTestGraph();
            _runner = new GraphRunner(_game.Graph);
            _saveRoot = Path.Combine(Path.GetTempPath(), "VneUIToolkit_" + Guid.NewGuid().ToString("N"));
            GraphSaveSlots.RootOverride = _saveRoot;
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            GraphSaveSlots.RootOverride = null;
            if (Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }

            Object.DestroyImmediate(_game.Graph);
        }

        [Test]
        public void Button_RaisesItsEvent()
        {
            var button = new GraphButton("Pause") { Graph = _game.Graph, EventName = "Pause" };
            _runner.Start();

            Assert.That(button.TrySend(), Is.True);

            Assert.That(_runner.Current, Is.SameAs(_game.Paused));
        }

        [Test]
        public void Button_SavesAndLoads()
        {
            var save = new GraphButton("Save") { Action = GraphEventButtonAction.Save, Slot = "slot1" };
            var load = new GraphButton("Load") { Action = GraphEventButtonAction.Load, Slot = "slot1" };
            _runner.Start();
            _runner.Raise("Pause");

            Assert.That(save.TrySend(), Is.True);
            _runner.Raise("Pause");
            Assert.That(load.TrySend(), Is.True);

            Assert.That(_runner.Current, Is.SameAs(_game.Paused));
        }

        [Test]
        public void LoadButton_CanBePressedOnlyWhileTheSlotHasASave()
        {
            var load = new GraphButton("Continue") { Graph = _game.Graph, Action = GraphEventButtonAction.Load, Slot = "slot1" };
            load.Connect();
            try
            {
                Assert.That(load.enabledSelf, Is.False);

                _runner.Start();
                GraphSaveSlots.Save(_runner, "slot1");
                Assert.That(load.enabledSelf, Is.True, "saving tells the button");

                GraphSaveSlots.Delete(_game.Graph, "slot1");
                Assert.That(load.enabledSelf, Is.False);

                load.DisableWithoutSave = false;
                Assert.That(load.enabledSelf, Is.True, "turned off: given back");
            }
            finally
            {
                load.Disconnect();
            }

            Assert.That(load.enabledSelf, Is.True);
        }

        [Test]
        public void LoadButton_NeverEnablesAButtonDisabledElsewhere_AndGivesItBackWhenDisconnected()
        {
            var disabled = new GraphButton("Continue") { Graph = _game.Graph, Action = GraphEventButtonAction.Load };
            disabled.SetEnabled(false);
            _runner.Start();
            GraphSaveSlots.Save(_runner, GraphEventButton.DefaultSlot);
            disabled.Connect();
            Assert.That(disabled.enabledSelf, Is.False, "someone else disabled it");
            disabled.Disconnect();

            var load = new GraphButton("Continue") { Graph = _game.Graph, Action = GraphEventButtonAction.Load, Slot = "empty" };
            load.Connect();
            Assert.That(load.enabledSelf, Is.False);
            load.Disconnect();
            Assert.That(load.enabledSelf, Is.True, "not left disabled once it stops watching the slot");
        }

        [Test]
        public void ParameterLabel_FollowsTheParameter_AndTheNextRunner()
        {
            var label = new GraphParameterLabel("Score: -") { Parameter = "Score", Format = "Score: {0}" };
            label.Connect();
            try
            {
                Assert.That(label.text, Is.EqualTo("Score: -"), "no runner yet: the text stays");

                _runner.Start();
                Assert.That(label.text, Is.EqualTo("Score: 0"));
                _runner.SetInt("Score", 4);
                Assert.That(label.text, Is.EqualTo("Score: 4"));

                _runner.Stop();
                _runner.SetInt("Score", 9);
                Assert.That(label.text, Is.EqualTo("Score: 4"), "the stopped runner is let go");

                _runner.Start();
                Assert.That(label.text, Is.EqualTo("Score: 0"));
            }
            finally
            {
                label.Disconnect();
            }

            _runner.SetInt("Score", 7);
            Assert.That(label.text, Is.EqualTo("Score: 0"), "not following once disconnected");
        }

        [Test]
        public void NodePanel_IsShownOnlyWhileTheGraphIsThere()
        {
            var paused = new GraphNodePanel { Graph = _game.Graph, Node = "Paused" };
            var round = new GraphNodePanel { Node = "Round" };
            paused.Connect();
            round.Connect();
            try
            {
                Assert.That(paused.style.display.value, Is.EqualTo(DisplayStyle.None), "no runner yet");

                _runner.Start();
                Assert.That(paused.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(round.style.display.value, Is.EqualTo(DisplayStyle.Flex), "inside Round");

                _runner.Raise("Pause");
                Assert.That(paused.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(paused.IsInNode, Is.True);

                _runner.Raise("Pause");
                _runner.Raise("Crash");
                Assert.That(paused.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(round.style.display.value, Is.EqualTo(DisplayStyle.None), "left Round");

                _runner.Stop();
                Assert.That(round.IsInNode, Is.False);
            }
            finally
            {
                paused.Disconnect();
                round.Disconnect();
            }
        }

        [Test]
        public void Controls_DoNothingUntilTheyAreOnAGameScreen()
        {
            // UI Builder の中（パネルに付いていない・エディタのパネル）では、隠さない・書き換えない
            _runner.Start();
            var panel = new GraphNodePanel { Node = "Paused" };
            var label = new GraphParameterLabel("Score") { Parameter = "Score" };
            var load = new GraphButton("Continue") { Action = GraphEventButtonAction.Load };

            panel.Refresh();
            label.Refresh();
            load.RefreshEnabled();

            Assert.That(panel.style.display.keyword, Is.EqualTo(StyleKeyword.Null), "display is left alone");
            Assert.That(label.text, Is.EqualTo("Score"));
            Assert.That(load.enabledSelf, Is.True);
        }
    }
}
