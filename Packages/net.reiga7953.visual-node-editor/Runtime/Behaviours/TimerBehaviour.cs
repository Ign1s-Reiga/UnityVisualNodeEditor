using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードにいる間、時間を数え、時間になったらイベントを Raise する組み込みの振る舞い（制限時間・「Ready… Go!」・無敵時間）。
    /// 残り時間を Float パラメータに出せる（Graph Parameter Text などで表示する）。
    /// 時間は Runner の Update の <c>deltaTime</c>（<c>Time.deltaTime</c>）で数えるので、Time Scale 0 のポーズ中は止まる。
    /// </summary>
    [Serializable]
    [BehaviourMenu("Visual Node Editor/Timer")]
    public sealed class TimerBehaviour : NodeBehaviour
    {
        [Tooltip("Seconds on this node before the event is raised.")]
        [SerializeField, Min(0f)] private float _seconds = 3f;

        [Tooltip("Raised once when the time is up. Empty: only count (with Time Left).")]
        [SerializeField, EventName] private string _eventName = string.Empty;

        [Tooltip("Optional Float parameter that holds the time left (show it with Graph Parameter Text).")]
        [SerializeField, GraphParameterName] private string _timeLeft = string.Empty;

        [Tooltip("With Time Left: do not start again from Seconds when entering; keep counting down from the parameter's value " +
                 "(so going to a pause state and back does not reset a time limit). Set the start value with Set Parameter.")]
        [SerializeField] private bool _keepCounting;

        private float _remaining;
        private bool _done;

        /// <summary>時間（秒）。</summary>
        public float Seconds
        {
            get => _seconds;
            set => _seconds = Mathf.Max(0f, value);
        }

        /// <summary>時間になったら Raise するイベント名（空なら数えるだけ）。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        /// <summary>残り時間を入れる Float パラメータの名前（空なら入れない）。</summary>
        public string TimeLeft
        {
            get => _timeLeft;
            set => _timeLeft = value ?? string.Empty;
        }

        /// <summary><see cref="TimeLeft"/> があるとき、入るたびに数え直さず、パラメータの値から数え続けるか。</summary>
        public bool KeepCounting
        {
            get => _keepCounting;
            set => _keepCounting = value;
        }

        /// <summary>入ったときから数える（Keep Counting ならパラメータの今の値から）。</summary>
        public override void OnEnter()
        {
            _done = false;
            _remaining = _seconds;
            if (!HasTimeLeft())
            {
                return;
            }

            if (_keepCounting)
            {
                _remaining = Runner.GetFloat(_timeLeft);
            }
            else
            {
                Runner.SetFloat(_timeLeft, _remaining);
            }
        }

        /// <summary>時間を進め、残り時間をパラメータに入れ、時間になったら 1 度だけ Raise する。</summary>
        public override void OnUpdate(float deltaTime)
        {
            if (_done)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - Mathf.Max(0f, deltaTime));
            if (HasTimeLeft())
            {
                Runner.SetFloat(_timeLeft, _remaining);
            }

            if (_remaining > 0f)
            {
                return;
            }

            _done = true;
            if (!string.IsNullOrEmpty(_eventName))
            {
                Runner.Raise(_eventName);
            }
        }

        /// <summary>イベントもパラメータも無ければ Warning。パラメータが無い・Float でなければ Error。</summary>
        public override IEnumerable<GraphIssue> GetIssues(NodeGraphAsset graph, NodeData node)
        {
            if (string.IsNullOrEmpty(_eventName) && string.IsNullOrEmpty(_timeLeft))
            {
                yield return new GraphIssue(GraphIssueSeverity.Warning,
                    $"'{node.Title}': the Timer has no event to raise and no Time Left parameter, so it does nothing.", node.Id);
            }

            if (string.IsNullOrEmpty(_timeLeft))
            {
                yield break;
            }

            var parameter = graph != null ? graph.FindParameterByName(_timeLeft) : null;
            if (parameter == null)
            {
                yield return new GraphIssue(GraphIssueSeverity.Error, $"'{node.Title}': the Timer's Time Left parameter '{_timeLeft}' is not in the Blackboard.", node.Id);
            }
            else if (parameter.Type != GraphParameterType.Float)
            {
                yield return new GraphIssue(GraphIssueSeverity.Error,
                    $"'{node.Title}': the Timer's Time Left parameter '{_timeLeft}' is {parameter.Type}. Use a Float parameter.", node.Id);
            }
        }

        /// <summary>例: <c>Timer 3 s → Go</c>、<c>Timer 300 s → TimeUp (Time)</c>、Keep Counting なら <c>Timer Time → TimeUp</c>。</summary>
        public override string Describe(NodeGraphAsset graph)
        {
            var length = _keepCounting && !string.IsNullOrEmpty(_timeLeft) ? _timeLeft : $"{_seconds.ToString(CultureInfo.InvariantCulture)} s";
            var target = string.IsNullOrEmpty(_eventName) ? string.Empty : $" → {_eventName}";
            var shown = !_keepCounting && !string.IsNullOrEmpty(_timeLeft) ? $" ({_timeLeft})" : string.Empty;
            return $"Timer {length}{target}{shown}";
        }

        // 残り時間のパラメータを使えるか（名前が無い・Runner に同じ名前の Float が無ければ使わない）
        private bool HasTimeLeft() => !string.IsNullOrEmpty(_timeLeft) && Runner != null && Runner.HasParameter(_timeLeft, GraphParameterType.Float);
    }
}
