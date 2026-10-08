using System;
using Reiga.VisualNodeEditor.Editor.Views;
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
        internal const string TitlePropertyName = "_title";

        private const string TitlePlaceholder = "(Title)";
        private const string CategoryClassPrefix = "vne-category--";

        private SerializedObject _serializedObject;
        private int _index = -1;

        public NodeInspectorView()
        {
            AddToClassList("vne-inspector-view");
            ShowEmpty();
        }

        /// <summary>表示中のノードが編集されたときに、そのノード ID を伴って呼ばれる。</summary>
        public event Action<string> NodeChanged;

        /// <summary>
        /// ポートの構成が変わる編集（コンテナの出口の追加・改名・並べ替え・削除、Exit ノードの出口の付け替え）の後に、
        /// 表示中のノード ID を伴って呼ばれる。受け取った側でグラフを作り直す（このインスペクタも作り直してよい）。
        /// </summary>
        public event Action<string> StructureChanged;

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
            content.Add(CreateHeader(asset.Nodes[_index].GetType(), nodeProperty));

            var child = nodeProperty.Copy();
            var end = nodeProperty.GetEndProperty();
            var hasChild = child.NextVisible(true);
            while (hasChild && !SerializedProperty.EqualContents(child, end))
            {
                // タイトルはヘッダーで編集する
                if (child.name != TitlePropertyName)
                {
                    var field = new PropertyField(child.Copy(), NodeDisplay.GetFieldLabel(child.name));
                    field.AddToClassList("vne-inspector-view__field");
                    content.Add(field);
                }

                hasChild = child.NextVisible(false);
            }

            // 出口はポートとエッジに関わるので、PropertyField ではなく専用の UI で編集する
            switch (asset.Nodes[_index])
            {
                case ContainerNode container:
                    var exitList = new ContainerExitListView(asset, container);
                    exitList.Changed += OnStructureChanged;
                    content.Add(exitList);
                    break;
                case ContainerExitNode exitNode:
                    var exitPicker = new ContainerExitPicker(asset, exitNode);
                    exitPicker.Changed += OnStructureChanged;
                    content.Add(exitPicker);
                    break;
            }

            Add(content);
            content.Bind(_serializedObject);
            content.TrackSerializedObjectValue(_serializedObject, OnSerializedObjectChanged);
        }

        /// <summary>カテゴリ色のアイコン + 型の表示名 + タイトル入力欄。</summary>
        private static VisualElement CreateHeader(Type nodeType, SerializedProperty nodeProperty)
        {
            var header = new VisualElement();
            header.AddToClassList("vne-inspector-view__header");

            var typeRow = new VisualElement();
            typeRow.AddToClassList("vne-inspector-view__type-row");

            var icon = new VisualElement();
            icon.AddToClassList("vne-inspector-view__icon");
            var category = NodeCategory.FromType(nodeType);
            if (category.Length > 0)
            {
                icon.AddToClassList(CategoryClassPrefix + category);
            }

            var typeLabel = new Label(NodeDisplay.GetTypeDisplayName(nodeType));
            typeLabel.AddToClassList("vne-inspector-view__type");
            typeRow.Add(icon);
            typeRow.Add(typeLabel);

            var titleField = new TextField
            {
                bindingPath = nodeProperty.FindPropertyRelative(TitlePropertyName).propertyPath,
            };
            titleField.textEdition.placeholder = TitlePlaceholder;
            titleField.AddToClassList("vne-inspector-view__title");

            header.Add(typeRow);
            header.Add(titleField);
            return header;
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

        private void OnStructureChanged()
        {
            if (NodeId != null)
            {
                StructureChanged?.Invoke(NodeId);
            }
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
