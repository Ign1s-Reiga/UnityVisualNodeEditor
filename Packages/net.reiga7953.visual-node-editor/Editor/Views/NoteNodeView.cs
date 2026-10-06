namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>メモノード（<see cref="NoteNode"/>）の View。ポートを持たず、サマリーに本文の 1 行目を出す。</summary>
    [CustomNodeView(typeof(NoteNode))]
    public sealed class NoteNodeView : NodeView
    {
        protected override string GetSummary() => (Data as NoteNode)?.Text;
    }
}
