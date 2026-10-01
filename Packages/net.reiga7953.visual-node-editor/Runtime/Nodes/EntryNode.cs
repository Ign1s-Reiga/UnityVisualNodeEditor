using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ゲーム／フローの開始点。グラフに 1 つだけ存在する想定。</summary>
    [Serializable]
    [NodeMenu("Flow/Entry")]
    public sealed class EntryNode : NodeData
    {
        protected override string DefaultTitle => "Entry";
    }
}
