using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>ノードグラフを編集するメインウィンドウ。</summary>
    public sealed class NodeGraphEditorWindow : EditorWindow
    {
        private const string NoAssetLabel = "(no asset)";

        // ドメインリロード後も同じアセットを開き直せるようシリアライズする
        [SerializeField] private NodeGraphAsset _asset;

        private NodeGraphView _graphView;
        private NodeSearchWindow _searchWindow;
        private Label _assetNameLabel;

        [MenuItem("Window/Visual Node Editor")]
        public static void Open() => Open(null);

        /// <summary>ウィンドウを開き、<paramref name="asset"/> が指定されていれば読み込む。</summary>
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

            _searchWindow = CreateInstance<NodeSearchWindow>();
            _searchWindow.hideFlags = HideFlags.HideAndDontSave;
            _searchWindow.Initialize(this, _graphView);
            _graphView.nodeCreationRequest = OnNodeCreationRequest;

            var saveButton = rootVisualElement.Q<ToolbarButton>("save-button");
            if (saveButton != null)
            {
                saveButton.clicked += Save;
            }

            _assetNameLabel = rootVisualElement.Q<Label>("asset-name");

            Refresh();
        }

        private void OnDisable()
        {
            if (_searchWindow != null)
            {
                DestroyImmediate(_searchWindow);
            }
        }

        private void Load(NodeGraphAsset asset)
        {
            _asset = asset;
            Refresh();
        }

        private void Refresh()
        {
            titleContent = new GUIContent(_asset != null ? _asset.name : "Visual Node Editor");
            if (_assetNameLabel != null)
            {
                _assetNameLabel.text = _asset != null ? AssetDatabase.GetAssetPath(_asset) : NoAssetLabel;
            }

            _graphView?.Populate(_asset);
        }

        private void OnNodeCreationRequest(NodeCreationContext context)
        {
            if (_asset == null)
            {
                ShowNotification(new GUIContent("Open a Node Graph asset first."));
                return;
            }

            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
        }

        private void Save()
        {
            if (_asset != null)
            {
                AssetDatabase.SaveAssetIfDirty(_asset);
            }
        }
    }
}
