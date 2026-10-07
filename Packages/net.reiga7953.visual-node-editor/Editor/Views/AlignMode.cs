namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>選択したノードの揃え方。</summary>
    public enum AlignMode
    {
        /// <summary>左端を、いちばん左のノードに揃える。</summary>
        Left,

        /// <summary>右端を、いちばん右のノードに揃える。</summary>
        Right,

        /// <summary>上端を、いちばん上のノードに揃える。</summary>
        Top,

        /// <summary>下端を、いちばん下のノードに揃える。</summary>
        Bottom,

        /// <summary>横方向の中心を、全体の中心に揃える（縦一列に並ぶ）。</summary>
        CenterHorizontally,

        /// <summary>縦方向の中心を、全体の中心に揃える（横一列に並ぶ）。</summary>
        CenterVertically,
    }
}
