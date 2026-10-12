using System;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// エディタで Yarn Graph Bridge を足したとき、同じ GameObject に Graph Variable Storage を足し、
    /// DialogueRunner の Variable Storage に設定する（Yarn の変数をグラフと共有するのに、手で繋がなくてよいように）。
    /// </summary>
    [InitializeOnLoad]
    internal static class YarnBridgeSetup
    {
        // DialogueRunner の Variable Storage のフィールド名（Yarn Spinner 3.x）
        internal const string VariableStoragePropertyName = "variableStorage";

        static YarnBridgeSetup()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component is YarnGraphBridge bridge)
            {
                ShareVariables(bridge.GetComponent<DialogueRunner>());
            }
        }

        /// <summary>
        /// <paramref name="runner"/> の Variable Storage を Graph Variable Storage にする（無ければ同じ GameObject に足す）。
        /// Yarn の既定の In Memory Variable Storage（<c>GameObject &gt; Yarn Spinner &gt; Dialogue System</c> のプレハブに最初から入っている）は置き換える。
        /// それ以外の Variable Storage が設定されていれば置き換えない（ユーザーが選んだものを壊さない）。設定したら true。
        /// </summary>
        internal static bool ShareVariables(DialogueRunner runner)
        {
            if (runner == null)
            {
                return false;
            }

            var serializedRunner = new SerializedObject(runner);
            var storageProperty = serializedRunner.FindProperty(VariableStoragePropertyName);
            if (storageProperty == null || !IsReplaceable(storageProperty.objectReferenceValue?.GetType()))
            {
                return false;
            }

            var storage = runner.GetComponent<GraphVariableStorage>();
            if (storage == null)
            {
                storage = Undo.AddComponent<GraphVariableStorage>(runner.gameObject);
            }

            storageProperty.objectReferenceValue = storage;
            serializedRunner.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// その型の Variable Storage を Graph Variable Storage にしてよいか: 未設定（null）・Graph Variable Storage・
        /// Yarn の既定の In Memory Variable Storage（ちょうどその型。それを継いだクラスはユーザーが選んだものとして残す）。
        /// </summary>
        internal static bool IsReplaceable(Type storageType) =>
            storageType == null || typeof(GraphVariableStorage).IsAssignableFrom(storageType) || storageType == typeof(InMemoryVariableStorage);
    }
}
