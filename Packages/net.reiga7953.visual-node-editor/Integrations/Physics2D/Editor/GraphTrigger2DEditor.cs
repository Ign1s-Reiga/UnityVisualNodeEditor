using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Physics2D.Editor
{
    /// <summary>
    /// <see cref="GraphTrigger2D"/> のインスペクタ。タグはプロジェクトのタグから、パラメータの変更はグラフの Blackboard から、イベント名はグラフの Event ノードの名前から選ぶ。
    /// 設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphTrigger2D))]
    public sealed class GraphTrigger2DEditor : GameComponentEditor
    {
        // GraphTrigger2D の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string TagPropertyName = "_tag";
        internal const string WhenPropertyName = "_when";
        internal const string ChangesPropertyName = "_changes";
        internal const string EventNamePropertyName = "_eventName";
        internal const string OncePropertyName = "_once";
        internal const string DeactivatePropertyName = "_deactivate";

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            root.Add(new PropertyField(serializedObject.FindProperty(GraphPropertyName), "Graph"));
            var tag = new TagField("Tag") { bindingPath = TagPropertyName, tooltip = serializedObject.FindProperty(TagPropertyName).tooltip };
            tag.AddToClassList(TagField.alignedFieldUssClassName);
            root.Add(tag);
            root.Add(new PropertyField(serializedObject.FindProperty(WhenPropertyName), "When"));
            root.Add(new PropertyField(serializedObject.FindProperty(ChangesPropertyName), "Changes"));
            root.Add(new PropertyField(serializedObject.FindProperty(EventNamePropertyName), "Event"));
            root.Add(new PropertyField(serializedObject.FindProperty(OncePropertyName), "Once"));
            root.Add(new PropertyField(serializedObject.FindProperty(DeactivatePropertyName), "Deactivate"));
            AddHints(root);
            return root;
        }

        protected override List<string> GetHints()
        {
            var hints = Physics2DHints.ForTrigger(HasCollider(((Component)target).gameObject), serializedObject.FindProperty(TagPropertyName).stringValue);
            hints.AddRange(GraphDestroyTriggerEditor.GetReactionHints(serializedObject, GraphPropertyName, ChangesPropertyName, EventNamePropertyName));
            return hints;
        }

        /// <summary>
        /// 触れたことが届くコライダーがあるか: 同じ GameObject の Collider 2D。Rigidbody 2D があれば、子の Collider 2D も
        /// （物理の通知は、コライダーの Rigidbody 2D の GameObject にも届く）。
        /// </summary>
        internal static bool HasCollider(GameObject owner) =>
            owner.TryGetComponent<Collider2D>(out _)
            || (owner.TryGetComponent<Rigidbody2D>(out _) && owner.GetComponentInChildren<Collider2D>(true) != null);
    }
}
