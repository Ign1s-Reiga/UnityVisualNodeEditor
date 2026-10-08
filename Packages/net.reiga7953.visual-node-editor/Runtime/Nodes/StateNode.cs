using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ゲームステート（タイトル / プレイ中 / ポーズ など）。<see cref="NodeBehaviour"/> を付けると、このステートにいる間の処理を書ける。</summary>
    [Serializable]
    [NodeMenu("State/State")]
    public sealed class StateNode : NodeData, IBehaviourHost
    {
        [SerializeField, TextArea] private string _description;

        // インスペクタでは専用の一覧で編集する（NodeInspectorView が名前で除外する）。
        // [HideInInspector] を付けると、振る舞いの中のフィールドまでインスペクタから見えなくなるので付けない
        [SerializeReference] private List<NodeBehaviour> _behaviours = new();

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <inheritdoc />
        public IReadOnlyList<NodeBehaviour> Behaviours => _behaviours;

        /// <inheritdoc />
        public void AddBehaviour(NodeBehaviour behaviour) => NodeBehaviourList.Add(_behaviours, behaviour);

        /// <inheritdoc />
        public bool RemoveBehaviourAt(int index) => NodeBehaviourList.RemoveAt(_behaviours, index);

        /// <inheritdoc />
        public bool MoveBehaviour(int index, int newIndex) => NodeBehaviourList.Move(_behaviours, index, newIndex);

        protected override string DefaultTitle => "State";
    }
}
