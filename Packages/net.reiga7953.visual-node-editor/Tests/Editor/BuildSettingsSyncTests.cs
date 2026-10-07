using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class BuildSettingsSyncTests
    {
        private const string GuidA = "0123456789abcdef0123456789abcdef";
        private const string GuidB = "fedcba9876543210fedcba9876543210";
        private const string GuidC = "00000000000000000000000000000abc";

        [Test]
        public void AddScenes_EnablesDisabledAndAppendsMissingKeepingOrder()
        {
            var current = new[]
            {
                BuildScene("Assets/Boot.unity", GuidC, true),
                BuildScene("Assets/A.unity", GuidA, false),
            };
            var scenes = new[]
            {
                new SceneReference(GuidA, "Assets/A.unity"),
                new SceneReference(GuidB, "Assets/B.unity"),
                new SceneReference(GuidB, "Assets/B.unity"),
                new SceneReference(null, "Assets/NoGuid.unity"),
            };

            var result = BuildSettingsSync.AddScenes(current, scenes, out var added, out var enabled);

            Assert.That(added, Is.EqualTo(1));
            Assert.That(enabled, Is.EqualTo(1));
            Assert.That(result.Select(s => s.path), Is.EqualTo(new[] { "Assets/Boot.unity", "Assets/A.unity", "Assets/B.unity" }));
            Assert.That(result.All(s => s.enabled), Is.True);
            Assert.That(result[2].guid.ToString(), Is.EqualTo(GuidB));
            Assert.That(current[1].enabled, Is.False, "the input list must not be modified");
        }

        [Test]
        public void AddScenes_NothingToDo_ReportsZero()
        {
            var current = new[] { BuildScene("Assets/A.unity", GuidA, true) };

            var result = BuildSettingsSync.AddScenes(current, new[] { new SceneReference(GuidA, "Assets/A.unity") }, out var added, out var enabled);

            Assert.That(added + enabled, Is.Zero);
            Assert.That(result, Has.Length.EqualTo(1));
        }

        [Test]
        public void Issues_ListSceneNodesWhoseSceneIsNotEnabled()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                var inBuild = new SceneNode { Scene = new SceneReference(GuidA, "Assets/A.unity") };
                var missing = new SceneNode { Scene = new SceneReference(GuidB, "Assets/B.unity") };
                graph.AddNode(inBuild);
                graph.AddNode(missing);
                graph.AddNode(new SceneNode());

                var enabledGuids = BuildSettingsSync.GetEnabledSceneGuids(new[]
                {
                    BuildScene("Assets/A.unity", GuidA, true),
                    BuildScene("Assets/B.unity", GuidB, false),
                });
                var issue = BuildSettingsSync.GetIssues(graph, enabledGuids).Single();

                Assert.That(issue.NodeId, Is.EqualTo(missing.Id));
                Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static EditorBuildSettingsScene BuildScene(string path, string guid, bool enabled) =>
            new EditorBuildSettingsScene(path, enabled) { guid = new GUID(guid) };
    }
}
