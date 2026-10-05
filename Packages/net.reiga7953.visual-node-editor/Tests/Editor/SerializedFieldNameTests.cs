using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// Editor が SerializedProperty で参照している Runtime の private フィールド名が、
    /// リネームで黙って壊れないことを保証する。
    /// </summary>
    public sealed class SerializedFieldNameTests
    {
        private NodeGraphAsset _graph;
        private SerializedObject _serializedObject;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.AddNode(new SceneNode());
            _serializedObject = new SerializedObject(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void NodeIdIsReachable()
        {
            var node = _serializedObject.FindProperty(NodeInspectorView.NodesPropertyName).GetArrayElementAtIndex(0);

            Assert.That(node.FindPropertyRelative(NodeInspectorView.IdPropertyName).stringValue,
                Is.EqualTo(_graph.Nodes[0].Id));
        }

        [Test]
        public void SceneReferenceFieldsAreReachable()
        {
            var scene = _serializedObject.FindProperty(NodeInspectorView.NodesPropertyName)
                .GetArrayElementAtIndex(0).FindPropertyRelative("_scene");

            Assert.That(scene, Is.Not.Null);
            Assert.That(scene.FindPropertyRelative(SceneReferenceDrawer.GuidPropertyName), Is.Not.Null);
            Assert.That(scene.FindPropertyRelative(SceneReferenceDrawer.PathPropertyName), Is.Not.Null);
        }
    }
}
