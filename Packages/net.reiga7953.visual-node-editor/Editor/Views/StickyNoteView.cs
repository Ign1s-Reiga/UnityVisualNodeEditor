using System;
using UnityEditor.Experimental.GraphView;
using GraphViewFontSize = UnityEditor.Experimental.GraphView.StickyNoteFontSize;
using GraphViewTheme = UnityEditor.Experimental.GraphView.StickyNoteTheme;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary><see cref="StickyNoteData"/> 1 件に対応する GraphView の付箋。</summary>
    public sealed class StickyNoteView : StickyNote
    {
        public StickyNoteView(StickyNoteData data)
        {
            viewDataKey = data.Id;
            AddToClassList("vne-sticky-note");
            Apply(data);
        }

        /// <summary>ドラッグでリサイズし終えたときに呼ばれる。</summary>
        public event Action<StickyNoteView> Resized;

        /// <summary>表示している付箋の ID。</summary>
        public string StickyNoteId => viewDataKey;

        /// <summary>データの内容（見出し・本文・配色・文字サイズ・位置と大きさ）を表示に反映する。</summary>
        internal void Apply(StickyNoteData data)
        {
            title = data.Title;
            contents = data.Contents;
            theme = ToGraphView(data.Theme);
            fontSize = ToGraphView(data.FontSize);
            SetPosition(data.Rect);
        }

        public override void OnResized()
        {
            base.OnResized();
            Resized?.Invoke(this);
        }

        /// <summary>Runtime の配色を GraphView の配色に変換する。</summary>
        public static GraphViewTheme ToGraphView(StickyNoteTheme theme) =>
            theme == StickyNoteTheme.Black ? GraphViewTheme.Black : GraphViewTheme.Classic;

        /// <summary>GraphView の配色を Runtime の配色に変換する。</summary>
        public static StickyNoteTheme FromGraphView(GraphViewTheme theme) =>
            theme == GraphViewTheme.Black ? StickyNoteTheme.Black : StickyNoteTheme.Classic;

        /// <summary>Runtime の文字サイズを GraphView の文字サイズに変換する。</summary>
        public static GraphViewFontSize ToGraphView(StickyNoteFontSize size) => size switch
        {
            StickyNoteFontSize.Small => GraphViewFontSize.Small,
            StickyNoteFontSize.Large => GraphViewFontSize.Large,
            StickyNoteFontSize.Huge => GraphViewFontSize.Huge,
            _ => GraphViewFontSize.Medium,
        };

        /// <summary>GraphView の文字サイズを Runtime の文字サイズに変換する。</summary>
        public static StickyNoteFontSize FromGraphView(GraphViewFontSize size) => size switch
        {
            GraphViewFontSize.Small => StickyNoteFontSize.Small,
            GraphViewFontSize.Large => StickyNoteFontSize.Large,
            GraphViewFontSize.Huge => StickyNoteFontSize.Huge,
            _ => StickyNoteFontSize.Medium,
        };
    }
}
