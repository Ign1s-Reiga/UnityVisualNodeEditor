using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>グラフ上に自由に置ける付箋（コメント）。ポートやエッジ、グループへの所属は持たない。</summary>
    [Serializable]
    public sealed class StickyNoteData
    {
        /// <summary>新しく作った付箋の大きさ。</summary>
        public static readonly Vector2 DefaultSize = new Vector2(200f, 160f);

        [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _title = "Comment";
        [SerializeField, TextArea] private string _contents = string.Empty;
        [SerializeField] private Rect _rect = new Rect(Vector2.zero, DefaultSize);
        [SerializeField] private StickyNoteTheme _theme = StickyNoteTheme.Classic;
        [SerializeField] private StickyNoteFontSize _fontSize = StickyNoteFontSize.Medium;

        /// <summary>グラフ内で一意な ID。</summary>
        public string Id => _id;

        /// <summary>見出し。</summary>
        public string Title
        {
            get => _title;
            set => _title = value;
        }

        /// <summary>本文。</summary>
        public string Contents
        {
            get => _contents;
            set => _contents = value;
        }

        /// <summary>エディタ上の位置と大きさ（Runtime では無視される）。</summary>
        public Rect Rect
        {
            get => _rect;
            set => _rect = value;
        }

        /// <summary>配色。</summary>
        public StickyNoteTheme Theme
        {
            get => _theme;
            set => _theme = value;
        }

        /// <summary>文字サイズ。</summary>
        public StickyNoteFontSize FontSize
        {
            get => _fontSize;
            set => _fontSize = value;
        }

        /// <summary>新しい ID を振り直す（貼り付け・複製でコピーを作るときに使う）。</summary>
        internal void AssignNewId() => _id = Guid.NewGuid().ToString("N");
    }
}
