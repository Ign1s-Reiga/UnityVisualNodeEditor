using Reiga.VisualNodeEditor.Editor.Scenes;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// 画面を持つノード（<see cref="IHasScreen"/>）のインスペクタの Screen の欄。UXML を選ぶ欄と、その下の Create Screen…（空のとき。
    /// グラフの隣に UXML を作って割り当て、UI Builder で開く。Undo 可）/ Edit Screen（UI Builder で開く）。
    /// 見た目は NodeGraphEditor.uss（.vne-node-screen）。
    /// </summary>
    public sealed class NodeScreenField : VisualElement
    {
        /// <summary>画面のフィールド名（State / Scene / Container ノード共通。EditMode テストで存在を検証している）。</summary>
        internal const string ScreenPropertyName = "_screen";

        internal const string EditLabel = "Edit Screen";
        internal const string CreateLabel = "Create Screen…";

        /// <summary>欄の下の説明: 画面は Graph Runner の Graph Screen Host が出す。</summary>
        internal const string HostNote =
            "A Graph Screen Host on the Graph Runner shows it while the graph is here, over the screens of the containers around. Play adds one.";

        private readonly SerializedProperty _property;
        private readonly NodeGraphAsset _graph;
        private readonly string _nodeId;
        private readonly Button _button;

        /// <param name="screenProperty">ノードの <c>_screen</c>（VisualTreeAsset）。</param>
        /// <param name="graph">ノードのあるグラフ（新しい画面はその隣に作る）。</param>
        /// <param name="nodeId">ノードの ID（新しい画面の名前に、押したときのタイトルを使う）。</param>
        public NodeScreenField(SerializedProperty screenProperty, NodeGraphAsset graph, string nodeId)
        {
            _property = screenProperty.Copy();
            _graph = graph;
            _nodeId = nodeId;
            AddToClassList("vne-node-screen");

            var field = new PropertyField(_property, NodeDisplay.GetFieldLabel(ScreenPropertyName));
            field.AddToClassList("vne-inspector-view__field");
            Add(field);

            var actions = new VisualElement();
            actions.AddToClassList("vne-node-screen__actions");
            _button = new Button(OnClicked);
            _button.AddToClassList("vne-node-screen__button");
            actions.Add(_button);
            Add(actions);

            var note = new Label(HostNote);
            note.AddToClassList("vne-inspector-view__note");
            Add(note);

            UpdateButton();

            // 欄を変えたら（Undo も）、ボタンを付け替える
            this.TrackPropertyValue(_property, _ => UpdateButton());
        }

        /// <summary>今のボタンの文字（テストが確かめる）。</summary>
        internal string ButtonText => _button.text;

        /// <summary>今の画面（欄の値）。</summary>
        internal VisualTreeAsset Screen
        {
            get
            {
                _property.serializedObject.Update();
                return _property.objectReferenceValue as VisualTreeAsset;
            }
        }

        /// <summary>
        /// Create Screen…: グラフの隣に画面を作って欄に入れる（Undo 可）。作った画面を返す。作れなければ null と理由。
        /// UI Builder で開くのはボタンだけ（テストから呼べるように分けてある）。
        /// </summary>
        internal VisualTreeAsset CreateAndAssign(out string problem)
        {
            var path = NodeScreens.CreateScreen(_graph, _graph != null ? _graph.FindNode(_nodeId) : null, out problem);
            if (path == null)
            {
                return null;
            }

            var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            _property.serializedObject.Update();
            _property.objectReferenceValue = screen;
            _property.serializedObject.ApplyModifiedProperties();
            UpdateButton();
            return screen;
        }

        private void OnClicked()
        {
            var screen = Screen;
            if (screen != null)
            {
                NodeScreens.Open(screen);
                return;
            }

            screen = CreateAndAssign(out var problem);
            if (screen == null)
            {
                EditorUtility.DisplayDialog("Create Screen", $"Could not create the screen: {problem}.", "OK");
                return;
            }

            NodeScreens.Open(screen);
        }

        private void UpdateButton()
        {
            var screen = Screen;
            _button.text = screen != null ? EditLabel : CreateLabel;
            _button.tooltip = screen != null
                ? $"Open {screen.name} in UI Builder"
                : "Create a UXML screen next to the graph for this node and open it in UI Builder";
        }
    }
}
