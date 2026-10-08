using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Issues;
using Reiga.VisualNodeEditor.Editor.Play;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ワンクリック Play の判断（始まるシーン、Build Settings の並び、Graph Runner の有無と追加）。</summary>
    public sealed class PlaySetupTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _entry;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Flow";
            _entry = Add(new EntryNode());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 始まるシーン ----

        [Test]
        public void StartScene_IsTheFirstSceneTheRunnerEnters()
        {
            var title = Add(Scene("Title"));
            var game = Add(Scene("Game"));
            Connect(_entry, title);
            Connect(title, game);

            Assert.That(PlayStart.FindStartScene(_graph).Name, Is.EqualTo("Title"));
        }

        [Test]
        public void StartScene_LooksPastAStartingStateAndIntoContainers()
        {
            // Entry → Boot(State) ─Go→ Stage(Container: Entry → Game(Scene))
            var boot = Add(new StateNode { Title = "Boot" });
            var go = Add(new EventNode { EventName = "Go" });
            var stage = Add(new ContainerNode());
            var inner = Add(new ContainerEntryNode { ParentId = stage.Id });
            var game = Add(Scene("Game"));
            game.ParentId = stage.Id;
            Connect(_entry, boot);
            Connect(boot, go);
            Connect(go, stage);
            Connect(inner, game);

            Assert.That(PlayStart.FindStartScene(_graph).Name, Is.EqualTo("Game"));
        }

        [Test]
        public void StartScene_SkipsSceneNodesWithoutAScene()
        {
            var unset = Add(new SceneNode());
            var title = Add(Scene("Title"));
            Connect(_entry, unset);
            Connect(unset, title);

            Assert.That(PlayStart.FindStartScene(_graph).Name, Is.EqualTo("Title"));
        }

        [Test]
        public void StartScene_IsNullWithoutScenesOrEntry()
        {
            Connect(_entry, Add(new StateNode()));
            Assert.That(PlayStart.FindStartScene(_graph), Is.Null);
            Assert.That(PlayStart.FindStartScene(null), Is.Null);
        }

        // ---- Build Settings の並び ----

        [Test]
        public void MoveToFront_PutsTheStartSceneFirstAndKeepsTheRest()
        {
            var a = GUID.Generate();
            var b = GUID.Generate();
            var c = GUID.Generate();
            var current = new[]
            {
                new EditorBuildSettingsScene("Assets/A.unity", true) { guid = a },
                new EditorBuildSettingsScene("Assets/B.unity", true) { guid = b },
                new EditorBuildSettingsScene("Assets/C.unity", false) { guid = c },
            };

            var moved = BuildSettingsSync.MoveToFront(current, c.ToString(), "Assets/C.unity");

            Assert.That(moved.Select(s => s.guid), Is.EqualTo(new[] { c, a, b }));
            Assert.That(moved[0].enabled, Is.True, "the start scene must be enabled");
            Assert.That(BuildSettingsSync.IsFirst(moved, c.ToString()), Is.True);
            Assert.That(BuildSettingsSync.IsFirst(current, c.ToString()), Is.False);
            Assert.That(current[0].guid, Is.EqualTo(a), "the input is not changed");
        }

        [Test]
        public void MoveToFront_AddsAMissingScene()
        {
            var a = GUID.Generate();
            var title = GUID.Generate();

            var moved = BuildSettingsSync.MoveToFront(
                new[] { new EditorBuildSettingsScene("Assets/A.unity", true) { guid = a } }, title.ToString(), "Assets/Title.unity");

            Assert.That(moved.Select(s => s.path), Is.EqualTo(new[] { "Assets/Title.unity", "Assets/A.unity" }));
        }

        // ---- シーンの Graph Runner ----

        [Test]
        public void RunnerGraphGuids_ComeFromGraphRunnerComponentsOnly()
        {
            const string runnerScript = "aaaa1111";
            const string otherScript = "bbbb2222";
            var yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
                       "--- !u!114 &100\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + runnerScript + ", type: 3}\n" +
                       "  _graph: {fileID: 11400000, guid: graphA, type: 2}\n" +
                       "--- !u!114 &200\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + otherScript + ", type: 3}\n" +
                       "  _graph: {fileID: 11400000, guid: graphButton, type: 2}\n" +
                       "--- !u!114 &300\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + runnerScript + ", type: 3}\n" +
                       "  _graph: {fileID: 0}\n";

            var guids = RunnerUsage.GetRunnerGraphGuids(yaml, runnerScript);

            Assert.That(guids, Is.EquivalentTo(new[] { "graphA" }), "a Graph Event Button's graph does not count, nor does an empty field");
        }

        [Test]
        public void RunnerGraphGuids_UnknownForBinaryScenes()
        {
            Assert.That(RunnerUsage.GetRunnerGraphGuids("\u0000binary", "aaaa1111"), Is.Null);
            Assert.That(RunnerUsage.GetRunnerGraphGuids(null, "aaaa1111"), Is.Null);
        }

        [Test]
        public void UnsavedGraph_IsNotReportedAsMissingARunner()
        {
            Connect(_entry, Add(Scene("Title")));

            Assert.That(RunnerUsage.IsUsedInBuildScenes(_graph), Is.True, "nothing to look up for a graph that is not an asset");
            Assert.That(GraphIssues.Collect(_graph).Select(i => i.Message), Has.No.Member(GraphIssues.NoRunnerMessage));
        }

        [Test]
        public void AddRunner_CreatesAGraphRunnerForTheGraph()
        {
            // テストを動かしているシーン（未保存のため追加のシーンは開けない）に一時的に置き、最後に消す
            var scene = SceneManager.GetActiveScene();
            GraphRunnerBehaviour runner = null;
            try
            {
                Assert.That(PlaySetup.FindRunner(scene, _graph), Is.Null);

                runner = PlaySetup.AddRunner(scene, _graph);

                Assert.That(runner.Graph, Is.SameAs(_graph));
                Assert.That(runner.gameObject.name, Is.EqualTo("Graph Runner (Flow)"));
                Assert.That(runner.gameObject.scene, Is.EqualTo(scene));
                Assert.That(PlaySetup.FindRunner(scene, _graph), Is.SameAs(runner));
            }
            finally
            {
                if (runner != null)
                {
                    Object.DestroyImmediate(runner.gameObject);
                }
            }
        }

        private static SceneNode Scene(string name) =>
            new SceneNode { Scene = new SceneReference("guid-" + name, $"Assets/{name}.unity") };

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
