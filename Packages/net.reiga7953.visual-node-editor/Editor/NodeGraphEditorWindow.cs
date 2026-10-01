using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>ノードグラフを編集するメインウィンドウ。</summary>
    public sealed class NodeGraphEditorWindow : EditorWindow
    {
        private NodeGraphView _graphView;
        private NodeGraphAsset _asset;

        [MenuItem("Window/Visual Node Editor")]
        public static void Open() => Open(null);

        public static void Open(NodeGraphAsset asset)
        {
            var window = GetWindow<NodeGraphEditorWindow>("Visual Node Editor");
            if (asset != null)
            {
                window.Load(asset);
            }
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is NodeGraphAsset asset)
            {
                Open(asset);
                return true;
            }

            return false;
        }

        private void CreateGUI()
        {
            var uxml = Resources.Load<VisualTreeAsset>("VisualNodeEditor/NodeGraphEditorWindow");
            uxml?.CloneTree(rootVisualElement);

            var uss = Resources.Load<StyleSheet>("VisualNodeEditor/NodeGraphEditor");
            if (uss != null)
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            _graphView = new NodeGraphView { name = "graph-view" };
            _graphView.StretchToParentSize();
            var container = rootVisualElement.Q("graph-container") ?? rootVisualElement;
            container.Add(_graphView);

            if (_asset != null)
            {
                _graphView.Populate(_asset);
            }
        }

        private void Load(NodeGraphAsset asset)
        {
            _asset = asset;
            titleContent = new GUIContent(asset.name);
            _graphView?.Populate(asset);
        }
    }
}
