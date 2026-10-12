using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 文字列がノードの場所（<see cref="GraphNodePath"/>。例: <c>Round/Paused</c>）であることを示す。インスペクタ（UI Builder を含む）では、
    /// 同じ並びの <c>graph</c> の値のグラフの待機ノードとコンテナから選べる欄になる（グラフが無ければ入力欄）。例: UI Toolkit の Graph Node Panel の <c>node</c>。
    /// </summary>
    public sealed class GraphNodePathAttribute : PropertyAttribute
    {
    }
}
