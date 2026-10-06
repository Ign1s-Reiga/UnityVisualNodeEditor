using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 全ノードの基底データ。サブクラスを追加するときは Editor 側に対応する NodeView を用意し、
    /// <see cref="NodeMenuAttribute"/> で検索メニューに登録する。
    /// </summary>
    [Serializable]
    public abstract class NodeData
    {
        [SerializeField, HideInInspector] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _title;
        [SerializeField, HideInInspector] private Vector2 _position;

        /// <summary>グラフ内で一意な ID。</summary>
        public string Id => _id;

        public string Title
        {
            get => string.IsNullOrEmpty(_title) ? DefaultTitle : _title;
            set => _title = value;
        }

        /// <summary>エディタ上の表示位置（Runtime では無視される）。</summary>
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>タイトル未設定時に表示される既定名。</summary>
        protected abstract string DefaultTitle { get; }

        /// <summary>新しい ID を振り直す（貼り付け・複製でコピーを作るときに使う）。</summary>
        internal void AssignNewId() => _id = Guid.NewGuid().ToString("N");
    }
}
