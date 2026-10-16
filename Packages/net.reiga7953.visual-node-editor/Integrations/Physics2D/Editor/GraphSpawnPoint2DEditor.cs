using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Physics2D.Editor
{
    /// <summary>
    /// <see cref="GraphSpawnPoint2D"/> のインスペクタ。パラメータはグラフの Blackboard から、プレイヤーのタグはプロジェクトのタグから選ぶ。
    /// 設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphSpawnPoint2D))]
    public sealed class GraphSpawnPoint2DEditor : GameComponentEditor
    {
        // GraphSpawnPoint2D の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string ParameterPropertyName = "_parameter";
        internal const string PointNamePropertyName = "_pointName";
        internal const string PlayerTagPropertyName = "_playerTag";
        internal const string DefaultPropertyName = "_default";

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            root.Add(new PropertyField(serializedObject.FindProperty(GraphPropertyName), "Graph"));
            root.Add(new PropertyField(serializedObject.FindProperty(ParameterPropertyName), "Parameter"));
            root.Add(new PropertyField(serializedObject.FindProperty(PointNamePropertyName), "Point Name"));
            var playerTag = new TagField("Player Tag") { bindingPath = PlayerTagPropertyName, tooltip = serializedObject.FindProperty(PlayerTagPropertyName).tooltip };
            playerTag.AddToClassList(TagField.alignedFieldUssClassName);
            root.Add(playerTag);
            root.Add(new PropertyField(serializedObject.FindProperty(DefaultPropertyName), "Default"));
            AddHints(root);
            return root;
        }

        protected override List<string> GetHints()
        {
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            var parameterName = serializedObject.FindProperty(ParameterPropertyName).stringValue;
            var parameter = graph != null ? graph.FindParameterByName(parameterName) : null;
            return Physics2DHints.ForSpawnPoint(graph != null, parameter != null, parameter?.Type ?? default, parameterName,
                serializedObject.FindProperty(PlayerTagPropertyName).stringValue);
        }
    }
}
