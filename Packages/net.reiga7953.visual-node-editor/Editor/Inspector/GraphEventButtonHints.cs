using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary><see cref="GraphEventButton"/> のインスペクタに出す注意（次に何をすればよいか）を決める。</summary>
    public static class GraphEventButtonHints
    {
        /// <summary>
        /// 設定の問題と、その直し方の一覧。問題が無ければ空。
        /// </summary>
        /// <param name="graphEvents">送り先のグラフにあるイベント名。グラフ未設定なら null。</param>
        /// <param name="eventName">設定されているイベント名。</param>
        /// <param name="action">送る操作。</param>
        /// <param name="sendOnClick">同じ GameObject のボタンのクリックで送るか。</param>
        /// <param name="hasClickSource">同じ GameObject に onClick を持つコンポーネントがあるか。</param>
        /// <param name="clickAlreadyCallsSend">その onClick が、インスペクタで既にこのコンポーネントの Send を呼んでいるか。</param>
        public static List<string> Get(
            IReadOnlyList<string> graphEvents, string eventName, GraphEventButtonAction action, bool sendOnClick, bool hasClickSource,
            bool clickAlreadyCallsSend = false)
        {
            var hints = new List<string>();
            if (action == GraphEventButtonAction.Raise)
            {
                if (graphEvents == null)
                {
                    hints.Add("Assign the graph to pick the event from a list.");
                }
                else if (graphEvents.Count == 0)
                {
                    hints.Add("This graph has no Event nodes yet. Add one in the Visual Node Editor, then pick it here.");
                }

                if (string.IsNullOrEmpty(eventName))
                {
                    hints.Add("Choose the event this button raises.");
                }
                else if (graphEvents != null && graphEvents.Count > 0 && !graphEvents.Contains(eventName, StringComparer.Ordinal))
                {
                    hints.Add($"'{eventName}' is not an event in this graph, so the button will do nothing.");
                }
            }

            if (sendOnClick && !hasClickSource)
            {
                hints.Add("No button on this GameObject. Add a UI Button here, or call Send() from any UnityEvent.");
            }
            else if (sendOnClick && clickAlreadyCallsSend)
            {
                hints.Add("The button's OnClick already calls Send(), so Send On Click does not hook it again (one click sends once).");
            }

            return hints;
        }
    }
}
