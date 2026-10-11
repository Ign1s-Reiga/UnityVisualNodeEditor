using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// <see cref="GraphRunner"/> のセーブ（<see cref="GraphRunner.CaptureState"/> で作り、<see cref="GraphRunner.RestoreState"/> で再開する）。
    /// 今いるノード、最後に読み込みを頼んだシーン、パラメータの値、Extras を持つ。<see cref="ToJson"/> で文字列にして、好きな場所に保存できる
    /// （ファイルのスロットは <see cref="GraphSaveSlots"/>）。
    /// </summary>
    [Serializable]
    public sealed class GraphRunnerState
    {
        /// <summary>今の形式の版。</summary>
        public const int CurrentVersion = 1;

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private string _graphName;
        [SerializeField] private string _nodeId;
        [SerializeField] private SceneReference _scene = new SceneReference();
        [SerializeField] private List<GraphStateValue> _parameters = new();
        [SerializeField] private List<GraphStateValue> _extras = new();

        public GraphRunnerState()
        {
        }

        internal GraphRunnerState(string graphName, string nodeId, SceneReference scene, List<GraphStateValue> parameters, List<GraphStateValue> extras)
        {
            _graphName = graphName;
            _nodeId = nodeId;
            _scene = scene ?? new SceneReference();
            _parameters = parameters ?? new List<GraphStateValue>();
            _extras = extras ?? new List<GraphStateValue>();
        }

        /// <summary>形式の版（<see cref="CurrentVersion"/>）。</summary>
        public int Version => _version;

        /// <summary>保存したときのグラフの名前（確認用）。</summary>
        public string GraphName => _graphName;

        /// <summary>保存したときに今いたノードの ID。</summary>
        public string NodeId => _nodeId;

        /// <summary>保存したときに最後に読み込みを頼んだシーン（無ければ空の参照）。</summary>
        public SceneReference Scene => _scene ?? new SceneReference();

        /// <summary>パラメータの値（名前・型・値）。</summary>
        public IReadOnlyList<GraphStateValue> Parameters => _parameters ??= new List<GraphStateValue>();

        /// <summary><see cref="GraphRunner.Extras"/> の値。</summary>
        public IReadOnlyList<GraphStateValue> Extras => _extras ??= new List<GraphStateValue>();

        /// <summary>JSON の文字列にする。</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary><see cref="ToJson"/> の文字列から戻す。</summary>
        /// <exception cref="ArgumentException">空、またはセーブの JSON でない。</exception>
        public static GraphRunnerState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("The save is empty.", nameof(json));
            }

            GraphRunnerState state;
            try
            {
                state = JsonUtility.FromJson<GraphRunnerState>(json);
            }
            catch (Exception exception)
            {
                throw new ArgumentException("The save could not be read: " + exception.Message, nameof(json), exception);
            }

            if (state == null || string.IsNullOrEmpty(state._nodeId))
            {
                throw new ArgumentException("The save has no node to resume from.", nameof(json));
            }

            return state;
        }
    }
}
