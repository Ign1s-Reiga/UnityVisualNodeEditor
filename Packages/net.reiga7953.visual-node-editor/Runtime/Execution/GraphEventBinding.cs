using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphRunnerBehaviour"/> のインスペクタで「イベント名 → 呼び出す処理」を対応付ける 1 項目。</summary>
    [Serializable]
    public sealed class GraphEventBinding
    {
        [Tooltip("Event ノードのイベント名（大文字小文字を区別する）。")]
        [SerializeField] private string _eventName;

        [SerializeField] private UnityEvent _response = new UnityEvent();

        public GraphEventBinding()
        {
        }

        public GraphEventBinding(string eventName)
        {
            _eventName = eventName;
        }

        /// <summary>反応するイベント名。</summary>
        public string EventName => _eventName;

        /// <summary>イベントが発生したときに呼ぶ処理。</summary>
        public UnityEvent Response => _response;

        /// <summary>
        /// <paramref name="bindings"/> のうち、イベント名が一致するものの <see cref="Response"/> をすべて呼ぶ。呼んだ数を返す。
        /// </summary>
        public static int Dispatch(IEnumerable<GraphEventBinding> bindings, string eventName)
        {
            if (bindings == null || string.IsNullOrEmpty(eventName))
            {
                return 0;
            }

            var count = 0;
            foreach (var binding in bindings)
            {
                if (binding != null && string.Equals(binding._eventName, eventName, StringComparison.Ordinal))
                {
                    binding._response?.Invoke();
                    count++;
                }
            }

            return count;
        }
    }
}
