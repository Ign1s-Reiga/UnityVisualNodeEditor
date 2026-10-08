namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>表示中の階層が「空」のとき、キャンバスに出す案内の種類。</summary>
    public enum EmptyStateKind
    {
        /// <summary>案内を出さない（ノードがある）。</summary>
        None,

        /// <summary>ルートにノードが 1 つも無い（Entry も無い）。</summary>
        EmptyGraph,

        /// <summary>ルートに Entry しか無い。</summary>
        OnlyEntry,

        /// <summary>コンテナの中に Entry / Exit しか無い。</summary>
        EmptyContainer,
    }
}
