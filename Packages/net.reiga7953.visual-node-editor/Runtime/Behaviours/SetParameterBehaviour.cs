using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードに入ったとき、パラメータ（Blackboard）を変える組み込みの振る舞い。変更（<see cref="ParameterChange"/>）を上から順に行う。
    /// 例: ラウンドの始めの State に <c>Score = 0</c>、次のレベルへ進む State に <c>Level += 1</c>。
    /// 値は <see cref="GraphRunner.SetInt"/> などで変えるので、<see cref="GraphRunner.ParameterChanged"/> と自動遷移の判定もふだんどおり動く。
    /// </summary>
    [Serializable]
    [BehaviourMenu("Visual Node Editor/Set Parameter")]
    public sealed class SetParameterBehaviour : NodeBehaviour
    {
        [Tooltip("Parameters to change when the graph enters this node, from top to bottom.")]
        [SerializeField] private List<ParameterChange> _changes = new List<ParameterChange>();

        /// <summary>変更の一覧（行う順）。</summary>
        public List<ParameterChange> Changes => _changes;

        /// <summary>変更を行う。できないもの（パラメータが無いなど）は飛ばして警告を出す。</summary>
        public override void OnEnter()
        {
            foreach (var change in _changes)
            {
                var problem = change?.ApplyTo(Runner);
                if (problem != null)
                {
                    Debug.LogWarning($"[VisualNodeEditor] '{Node?.Title}': Set Parameter skipped a change because {problem}.");
                }
            }
        }

        /// <summary>パラメータが無い・型に合わない操作（Bool・String の Add）は Error。</summary>
        public override IEnumerable<GraphIssue> GetIssues(NodeGraphAsset graph, NodeData node)
        {
            foreach (var change in _changes)
            {
                var problem = change?.GetProblem(graph);
                if (problem != null)
                {
                    yield return new GraphIssue(GraphIssueSeverity.Error, $"'{node.Title}': a Set Parameter change has a problem: {problem}.", node.Id);
                }
            }
        }

        /// <summary>変更を並べた説明（例: <c>Score = 0, Level += 1</c>）。変更が無ければ null（クラスの名前を出す）。</summary>
        public override string Describe(NodeGraphAsset graph) =>
            _changes.Count == 0 ? null : string.Join(", ", _changes.Where(change => change != null).Select(change => change.Describe(graph)));
    }
}
