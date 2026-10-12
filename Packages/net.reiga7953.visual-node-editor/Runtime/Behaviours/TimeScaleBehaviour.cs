using System;
using System.Globalization;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードにいる間 <see cref="Time.timeScale"/> を変える組み込みの振る舞い（既定 0 = 止める）。出るとき（<see cref="GraphRunner.Stop"/> でも）入る前の値に戻す。
    /// ポーズの State に付けると、<c>Time.deltaTime</c> で動く物が止まる（<c>Update</c> は呼ばれ続けるので、入力やメニューは動く）。
    /// </summary>
    [Serializable]
    [BehaviourMenu("Visual Node Editor/Time Scale")]
    public sealed class TimeScaleBehaviour : NodeBehaviour
    {
        [Tooltip("Time.timeScale while the graph is on this node (0 pauses the game, 1 is normal speed). It goes back when the graph leaves.")]
        [SerializeField] [Min(0f)] private float _timeScale;

        [NonSerialized] private float _previous = 1f;

        /// <summary>このノードにいる間の <see cref="Time.timeScale"/>。</summary>
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        /// <summary>今の値を覚えてから変える。</summary>
        public override void OnEnter()
        {
            _previous = Time.timeScale;
            Time.timeScale = Mathf.Max(0f, _timeScale);
        }

        /// <summary>入る前の値に戻す。</summary>
        public override void OnExit() => Time.timeScale = _previous;

        /// <summary>例: <c>Time Scale 0</c>。</summary>
        public override string Describe(NodeGraphAsset graph) => "Time Scale " + _timeScale.ToString(CultureInfo.InvariantCulture);
    }
}
