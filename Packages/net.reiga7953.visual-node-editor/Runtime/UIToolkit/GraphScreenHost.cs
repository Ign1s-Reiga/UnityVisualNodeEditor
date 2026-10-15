using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 同じ GameObject の UIDocument に、グラフの今の場所の画面（ノードの <see cref="IHasScreen.Screen"/>）を出すコンポーネント。
    /// 入っているコンテナの画面の上に、今のノードの画面を重ねる（例: コンテナ Round の HUD の上に、Paused のポーズ画面）。
    /// Graph Runner と同じ GameObject に置く（Runner と一緒にシーンをまたいで残る）。画面は Unity のシーンとは別に、ノードごとに替わる。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Screen Host")]
    [RequireComponent(typeof(UIDocument))]
    public sealed class GraphScreenHost : MonoBehaviour
    {
        [Tooltip("このグラフを実行中の Graph Runner の場所の画面を出す。空なら、実行中のどれか 1 つ。")]
        [SerializeField] private NodeGraphAsset _graph;

        private readonly ScreenStack _stack = new ScreenStack();
        private GraphRunnerFollower _follower;
        private UIDocument _document;
        private bool _dirty = true;

        /// <summary>画面を出すグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                if (_follower != null)
                {
                    _follower.Graph = value;
                }
            }
        }

        /// <summary>今出している画面（下から上の順）。</summary>
        public System.Collections.Generic.IReadOnlyList<VisualTreeAsset> Shown => _stack.Shown;

        /// <summary>今のグラフの場所の画面を、すぐに出し直す（ふだんは場所が替わったフレームの終わりに出し直す）。</summary>
        public void Refresh()
        {
            _dirty = false;
            _stack.Show(Root, GraphScreens.At(_follower?.Runner));
        }

        private VisualElement Root
        {
            get
            {
                if (_document == null)
                {
                    _document = GetComponent<UIDocument>();
                }

                return _document != null ? _document.rootVisualElement : null;
            }
        }

        private void OnEnable()
        {
            _follower ??= new GraphRunnerFollower(MarkDirty, _ => MarkDirty());
            _follower.Graph = _graph;
            _follower.Start();
        }

        private void OnDisable()
        {
            _follower?.Stop();
            _stack.Clear();
        }

        private void MarkDirty() => _dirty = true;

        // 場所が替わったとき、UIDocument が根を作り直したとき（有効になり直すと作り直す）だけ出し直す
        private void LateUpdate()
        {
            if (_dirty || !_stack.IsShownIn(Root))
            {
                Refresh();
            }
        }
    }
}
