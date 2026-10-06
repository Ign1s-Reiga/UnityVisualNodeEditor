using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class SceneReferencePostprocessorTests
    {
        private const string OldPath = "Assets/Scenes/Title.unity";
        private const string NewPath = "Assets/Scenes/TitleScreen.unity";

        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.AddNode(new SceneNode { Scene = new SceneReference("guid", OldPath) });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void CleanGraph_IsUpdatedAndSaved()
        {
            EditorUtility.ClearDirty(_graph);

            var changed = SceneReferencePostprocessor.SyncGraph(_graph, _ => NewPath, out var shouldSave);

            Assert.That(changed, Is.True);
            Assert.That(shouldSave, Is.True);
            Assert.That(EditorUtility.IsDirty(_graph), Is.True);
        }

        [Test]
        public void GraphWithUnsavedEdits_IsUpdatedButNotSaved()
        {
            EditorUtility.SetDirty(_graph);

            var changed = SceneReferencePostprocessor.SyncGraph(_graph, _ => NewPath, out var shouldSave);

            Assert.That(changed, Is.True);
            Assert.That(shouldSave, Is.False, "the user's unsaved edits must not be written behind their back");
            Assert.That(EditorUtility.IsDirty(_graph), Is.True);
        }

        [Test]
        public void UpToDateGraph_IsLeftAlone()
        {
            EditorUtility.ClearDirty(_graph);

            var changed = SceneReferencePostprocessor.SyncGraph(_graph, _ => OldPath, out var shouldSave);

            Assert.That(changed, Is.False);
            Assert.That(shouldSave, Is.False);
            Assert.That(EditorUtility.IsDirty(_graph), Is.False);
        }
    }
}
