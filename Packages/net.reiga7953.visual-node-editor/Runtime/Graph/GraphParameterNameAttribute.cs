using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 文字列がグラフのパラメータ（Blackboard）の名前であることを示す。インスペクタ（UI Builder を含む）では、同じ並びの <c>graph</c> の値のグラフから
    /// 選べる欄になる（グラフが無ければ入力欄）。例: UI Toolkit の Graph Parameter Label の <c>parameter</c>。
    /// </summary>
    public sealed class GraphParameterNameAttribute : PropertyAttribute
    {
    }
}
