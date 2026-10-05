using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// 選択中のノード 1 つ分のフィールドを <see cref="PropertyField"/> で表示・編集するパネル。
    /// 編集は <see cref="SerializedObject"/> のバインディング経由なので Undo も効く。
    /// </summary>
    public sealed class NodeInspectorView : VisualElement
    {
        // NodeGraphAsset / NodeData の private フィールド名（EditMode テストで存在を検証している）
        internal const string NodesPropertyName = "_nodes";
        internal const string IdPropertyName = "_id";

        private SerializedObject _serializedObject;
        private int _index = -1;

        public NodeInspectorView()
        {
            AddToClassList("vne-inspector-view");
            ShowEmpty();
        }

        /// <summary>表示中のノードが編集されたときに、そのノード ID を伴って呼ばれる。</summary>
        public event Action<string> NodeChanged;

        /// <summary>表示中のノード ID。何も表示していなければ null。</summary>
        public string NodeId { get; private set; }

        /// <summary><paramref name="asset"/> 内の <paramref name="nodeId"/> のノードを表示する。見つからなければ空表示にする。</summary>
        public void Show(NodeGraphAsset asset, string nodeId)
        {
            Clear();
            NodeId = null;
            _index = -1;
            _serializedObject = null;

            _index = asset != null && nodeId != null ? IndexOfNode(asset, nodeId) : -1;
            if (_index < 0)
            {
                ShowEmpty();
                return;
            }

            NodeId = nodeId;
            _serializedObject = new SerializedObject(asset);
            var nodeProperty = _serializedObject.FindProperty(NodesPropertyName).GetArrayElementAtIndex(_index);

            // 中身ごと差し替えることで、前のノードのバインディングと変更監視を確実に外す
            var content = new VisualElement();
            content.AddToClassList("vne-inspector-view__content");

            var header = new Label(ObjectNames.NicifyVariableName(asset.Nodes[_index].GetType().Name));
            header.AddToClassList("vne-inspector-view__header");
            content.Add(header);

            var child = nodeProperty.Copy();
            var end = nodeProperty.GetEndProperty();
            var hasChild = child.NextVisible(true);
            while (hasChild && !SerializedProperty.EqualContents(child, end))
            {
                content.Add(new PropertyField(child.Copy()));
                hasChild = child.NextVisible(false);
            }

            Add(content);
            content.Bind(_serializedObject);
            content.TrackSerializedObjectValue(_serializedObject, OnSerializedObjectChanged);
        }

        private void ShowEmpty()
        {
            var label = new Label("Select a single node to edit it.");
            label.AddToClassList("vne-inspector-view__empty");
            Add(label);
        }

        private void OnSerializedObjectChanged(SerializedObject serializedObject)
        {
            if (NodeId == null)
            {
                return;
            }

            // ノードの削除などで配列の位置がずれていたら、別ノードを編集しないよう表示をやめる
            var nodes = serializedObject.FindProperty(NodesPropertyName);
            if (nodes == null || _index >= nodes.arraySize
                || nodes.GetArrayElementAtIndex(_index).FindPropertyRelative(IdPropertyName)?.stringValue != NodeId)
            {
                Show(null, null);
                return;
            }

            NodeChanged?.Invoke(NodeId);
        }

        private static int IndexOfNode(NodeGraphAsset asset, string nodeId)
        {
            for (var i = 0; i < asset.Nodes.Count; i++)
            {
                if (asset.Nodes[i] != null && asset.Nodes[i].Id == nodeId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
