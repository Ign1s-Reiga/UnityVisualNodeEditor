using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// パラメータ（Blackboard）の値を、同じ GameObject のテキストに書式（<c>Score: {0}</c>）で出すコンポーネント。
    /// テキストは、public な string の <c>text</c> を持つ最初のコンポーネント（uGUI の Text・TextMeshPro・TextMesh）。
    /// パッケージを uGUI に依存させないため、型ではなく名前で探す。
    /// <para>
    /// 書き換えるのは、有効になったとき・そのパラメータが変わったとき・Runner が始まったとき。Runner が無ければ書き換えない（シーンに置いた文字のまま）。
    /// </para>
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Parameter Text")]
    public sealed class GraphParameterText : MonoBehaviour
    {
        private const string TextPropertyName = "text";

        [Tooltip("このグラフを実行中の Graph Runner の値を出す。空なら、実行中のどれか 1 つ。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("出すパラメータの名前。")]
        [SerializeField] private string _parameter = string.Empty;

        [Tooltip("書式。{0} が値になる（例: \"Score: {0}\"、小数 1 桁なら \"{0:0.0}\"）。")]
        [SerializeField] private string _format = "{0}";

        private GraphRunner _runner;
        private bool _listening;
        private bool _warnedFormat;

        /// <summary>値を読むグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                RefreshIfListening();
            }
        }

        /// <summary>出すパラメータの名前。</summary>
        public string Parameter
        {
            get => _parameter;
            set
            {
                _parameter = value ?? string.Empty;
                RefreshIfListening();
            }
        }

        /// <summary>書式（<c>{0}</c> が値）。</summary>
        public string Format
        {
            get => _format;
            set
            {
                _format = value ?? string.Empty;
                _warnedFormat = false;
                RefreshIfListening();
            }
        }

        /// <summary>今の値でテキストを書き換える。Runner・パラメータ・テキストが無ければ何もしない。</summary>
        public void Refresh()
        {
            var runner = GraphRunner.FindRunning(_graph);
            if (_listening)
            {
                SetRunner(runner);
            }

            var target = FindTextTarget(gameObject);
            if (runner == null || target == null || !runner.TryGetParameter(_parameter, out var value))
            {
                return;
            }

            if (!TryFormat(_format, value, out var text) && !_warnedFormat)
            {
                _warnedFormat = true;
                Debug.LogWarning($"[VisualNodeEditor] '{name}': the format \"{_format}\" is not valid, so only the value is shown. Use {{0}} for the value.", this);
            }

            FindTextProperty(target).SetValue(target, text);
        }

        /// <summary>
        /// <paramref name="value"/> を <paramref name="format"/>（<c>{0}</c> が値。<see cref="CultureInfo.InvariantCulture"/>）で文字にする。
        /// 書式が誤っていれば値だけを文字にして false。
        /// </summary>
        public static bool TryFormat(string format, object value, out string text)
        {
            try
            {
                text = string.Format(CultureInfo.InvariantCulture, string.IsNullOrEmpty(format) ? "{0}" : format, value);
                return true;
            }
            catch (FormatException)
            {
                text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return false;
            }
        }

        /// <summary><paramref name="gameObject"/> の、public な string の <c>text</c>（読み書きできるもの）を持つ最初のコンポーネント。無ければ null。</summary>
        public static Component FindTextTarget(GameObject gameObject) =>
            gameObject == null
                ? null
                : gameObject.GetComponents<Component>().FirstOrDefault(component => !(component is GraphParameterText) && FindTextProperty(component) != null);

        private static PropertyInfo FindTextProperty(Component component)
        {
            if (component == null)
            {
                return null;
            }

            var property = component.GetType().GetProperty(TextPropertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.PropertyType == typeof(string) && property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                ? property
                : null;
        }

        private void OnEnable() => Enable();

        private void OnDisable() => Disable();

        /// <summary>有効になったとき: Runner の開始・停止とパラメータの変化を聞き、テキストを書き換える（テストからも呼ぶ）。</summary>
        internal void Enable()
        {
            _listening = true;
            GraphRunner.Started += OnRunnerStarted;
            GraphRunner.Stopped += OnRunnerStopped;
            Refresh();
        }

        /// <summary>無効になったとき: <see cref="Enable"/> で聞き始めたものを外す。</summary>
        internal void Disable()
        {
            _listening = false;
            GraphRunner.Started -= OnRunnerStarted;
            GraphRunner.Stopped -= OnRunnerStopped;
            SetRunner(null);
        }

        // 値を読んでいる Runner が止まったら、ほかの実行中の Runner を探し直す（無ければ外れて、止まった Runner を持ち続けない。文字はそのまま）
        private void OnRunnerStopped(GraphRunner runner)
        {
            if (runner == _runner)
            {
                Refresh();
            }
        }

        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_graph == null || runner.Graph == _graph)
            {
                Refresh();
            }
        }

        private void OnParameterChanged(string parameter)
        {
            if (parameter == _parameter)
            {
                Refresh();
            }
        }

        private void RefreshIfListening()
        {
            if (_listening)
            {
                Refresh();
            }
        }

        // パラメータの変化を聞く Runner を替える（止まった Runner からは外れる）
        private void SetRunner(GraphRunner runner)
        {
            if (runner == _runner)
            {
                return;
            }

            if (_runner != null)
            {
                _runner.ParameterChanged -= OnParameterChanged;
            }

            _runner = runner;
            if (_runner != null)
            {
                _runner.ParameterChanged += OnParameterChanged;
            }
        }
    }
}
