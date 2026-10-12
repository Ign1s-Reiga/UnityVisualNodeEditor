using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>
    /// テスト用: Yarn Spinner の Dialogue System の代わりのプレハブ。Dialogue Runner と、その Variable Storage に設定した In Memory Variable Storage だけ
    /// （本物と同じ。TextMesh Pro を使わないので、Essential Resources の読み込みのウィンドウを開かない）。
    /// </summary>
    internal static class TestDialogueSystemPrefab
    {
        /// <summary><paramref name="path"/>（.prefab）に作り、プレハブのアセットを返す。</summary>
        public static GameObject Create(string path)
        {
            var template = new GameObject("Dialogue System");
            try
            {
                var runner = template.AddComponent<DialogueRunner>();
                var storage = template.AddComponent<InMemoryVariableStorage>();
                var serializedRunner = new SerializedObject(runner);
                serializedRunner.FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue = storage;
                serializedRunner.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }
    }
}
