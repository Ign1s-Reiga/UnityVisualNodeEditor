using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// Yarn Spinner の会話を 1 つ流す待機ノード。入ると <see cref="YarnGraphBridge"/> が Yarn のノード（<see cref="YarnNode"/>）で会話を始め、
    /// 会話が終わったら完了イベント（<see cref="CompleteEvent"/>。条件付きの Event で分岐できる）で次へ進む。完了イベントの行き先が無ければ Advance する。
    /// Yarn は会話の途中から再開できないので、ロードするとこのノードの最初から（入る前の値で）やり直す（<see cref="IRestartOnLoad"/>）。
    /// </summary>
    [Serializable]
    [NodeMenu("ADV/Dialogue")]
    public sealed class DialogueNode : NodeData, IBehaviourHost, IRestartOnLoad
    {
        /// <summary>完了イベントの既定の名前。</summary>
        public const string DefaultCompleteEvent = "Done";

        [Tooltip("会話を始める Yarn のノード名。")]
        [SerializeField, YarnNode] private string _yarnNode;

        [Tooltip("会話が終わったときに Raise するイベント名。この名前の Event を条件違いで並べると、会話の結果で行き先を分けられる。")]
        [SerializeField, EventName] private string _completeEvent = DefaultCompleteEvent;

        // インスペクタでは専用の一覧で編集する（NodeInspectorView が名前で除外する）
        [SerializeReference] private List<NodeBehaviour> _behaviours = new();

        /// <summary>会話を始める Yarn のノード名。</summary>
        public string YarnNode
        {
            get => _yarnNode;
            set => _yarnNode = value;
        }

        /// <summary>会話が終わったときに Raise するイベント名（既定 <see cref="DefaultCompleteEvent"/>）。</summary>
        public string CompleteEvent
        {
            get => _completeEvent;
            set => _completeEvent = value;
        }

        /// <inheritdoc />
        public IReadOnlyList<NodeBehaviour> Behaviours => _behaviours;

        /// <inheritdoc />
        public void AddBehaviour(NodeBehaviour behaviour) => NodeBehaviourList.Add(_behaviours, behaviour);

        /// <inheritdoc />
        public bool RemoveBehaviourAt(int index) => NodeBehaviourList.RemoveAt(_behaviours, index);

        /// <inheritdoc />
        public bool MoveBehaviour(int index, int newIndex) => NodeBehaviourList.Move(_behaviours, index, newIndex);

        /// <summary>タイトル未入力なら Yarn のノード名を出す。</summary>
        public override string ContentTitle => string.IsNullOrWhiteSpace(_yarnNode) ? null : _yarnNode.Trim();

        /// <inheritdoc />
        public override IEnumerable<GraphIssue> GetIssues()
        {
            if (string.IsNullOrWhiteSpace(_yarnNode))
            {
                yield return new GraphIssue(GraphIssueSeverity.Warning,
                    $"'{Title}' has no Yarn node, so it shows no dialogue. Pick the Yarn node to run.", Id);
            }
        }

        protected override string DefaultTitle => "Dialogue";
    }
}
