using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>接続を持たないメモ用ノード。</summary>
    [Serializable]
    [NodeMenu("Misc/Note")]
    public sealed class NoteNode : NodeData
    {
        [SerializeField, TextArea] private string _text;

        public string Text
        {
            get => _text;
            set => _text = value;
        }

        protected override string DefaultTitle => "Note";
    }
}
