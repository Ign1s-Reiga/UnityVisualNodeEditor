using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// コンテナの中の出口。ここに着くとコンテナから出て、親コンテナの出力ポートのうち ID が <see cref="ExitId"/> のものから続く。
    /// 同じ出口を複数の Exit ノードが指してよい。コンテナの中にだけ置ける。
    /// </summary>
    [Serializable]
    [NodeMenu("Container/Exit")]
    public sealed class ContainerExitNode : NodeData
    {
        // エディタでは親コンテナの出口から選ぶドロップダウンで編集する（生の ID は見せない）
        [SerializeField, HideInInspector] private string _exitId;

        /// <summary>親コンテナの出口（<see cref="ContainerExit.Id"/>）。</summary>
        public string ExitId
        {
            get => _exitId ?? string.Empty;
            set => _exitId = value ?? string.Empty;
        }

        protected override string DefaultTitle => "Exit";
    }
}
