using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードの文字列フィールドがイベント名（<see cref="GraphRunner.Raise"/> で起こす名前）であることを示す。
    /// インスペクタでは、Event ノードのイベント名と同じく、グラフで使っている名前から選べる欄になる（打ち間違いを防ぐ）。
    /// 例: Yarn Spinner 連携の Dialogue ノードの完了イベント。
    /// </summary>
    public sealed class EventNameAttribute : PropertyAttribute
    {
    }
}
