using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ステート間遷移などのトリガーとなるイベント。</summary>
    [Serializable]
    [NodeMenu("Event/Event")]
    public sealed class EventNode : NodeData
    {
        [SerializeField] private string _eventName;

        public string EventName
        {
            get => _eventName;
            set => _eventName = value;
        }

        protected override string DefaultTitle => "Event";
    }
}
