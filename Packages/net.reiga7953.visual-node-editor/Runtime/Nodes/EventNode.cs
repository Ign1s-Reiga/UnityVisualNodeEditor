using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ステート間遷移などのトリガーとなるイベント。条件（<see cref="Conditions"/>。すべて満たすとき有効）を付けると、
    /// 同じ名前の Event を並べて行き先を分けたり（<see cref="EventTriggerMode.Raise"/>）、
    /// 条件を満たしたら自分で進んだり（<see cref="EventTriggerMode.Automatic"/>）できる。
    /// </summary>
    [Serializable]
    [NodeMenu("Event/Event")]
    public sealed class EventNode : NodeData
    {
        [SerializeField] private string _eventName;
        [SerializeField] private EventTriggerMode _trigger;

        // インスペクタでは専用の一覧で編集する（NodeInspectorView が名前で除外する）
        [SerializeField] private List<TransitionCondition> _conditions = new();

        public string EventName
        {
            get => _eventName;
            set => _eventName = value;
        }

        /// <summary>どうやって起こされるか。既定は <see cref="EventTriggerMode.Raise"/>。</summary>
        public EventTriggerMode Trigger
        {
            get => _trigger;
            set => _trigger = value;
        }

        /// <summary>条件を満たしたら自分で進むか（<see cref="EventTriggerMode.Automatic"/>）。</summary>
        public bool IsAutomatic => _trigger == EventTriggerMode.Automatic;

        /// <summary>条件。すべて満たすときに有効（空なら常に有効）。</summary>
        public IReadOnlyList<TransitionCondition> Conditions => _conditions ??= new List<TransitionCondition>();

        /// <summary>条件を末尾に足す。</summary>
        public void AddCondition(TransitionCondition condition)
        {
            if (condition != null)
            {
                (_conditions ??= new List<TransitionCondition>()).Add(condition);
            }
        }

        /// <summary>条件を外す。範囲外なら false。</summary>
        public bool RemoveConditionAt(int index)
        {
            if (_conditions == null || index < 0 || index >= _conditions.Count)
            {
                return false;
            }

            _conditions.RemoveAt(index);
            return true;
        }

        /// <summary>条件をすべて外す。</summary>
        public void ClearConditions() => _conditions?.Clear();

        protected override string DefaultTitle => "Event";
    }
}
