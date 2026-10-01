using System;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// <see cref="NodeView"/> のサブクラスに付け、対応する <see cref="NodeData"/> 型を宣言する。
    /// <see cref="NodeViewFactory"/> が自動で収集するため、手動登録は不要。
    /// </summary>
    /// <example><c>[CustomNodeView(typeof(SceneNode))] public sealed class SceneNodeView : NodeView { }</c></example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeViewAttribute : Attribute
    {
        public CustomNodeViewAttribute(Type nodeType) => NodeType = nodeType;

        /// <summary>この View が表示する <see cref="NodeData"/> の型。</summary>
        public Type NodeType { get; }
    }
}
