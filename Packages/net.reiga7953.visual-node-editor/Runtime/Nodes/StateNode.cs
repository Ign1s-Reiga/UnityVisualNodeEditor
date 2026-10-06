using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ゲームステート（タイトル / プレイ中 / ポーズ など）。</summary>
    [Serializable]
    [NodeMenu("State/State")]
    public sealed class StateNode : NodeData
    {
        [SerializeField, TextArea] private string _description;

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        protected override string DefaultTitle => "State";
    }
}
