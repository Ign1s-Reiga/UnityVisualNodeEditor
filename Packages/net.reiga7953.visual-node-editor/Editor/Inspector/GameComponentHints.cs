using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// ゲーム向けのコンポーネント（<see cref="GraphNodeActivator"/>・<see cref="GraphParameterText"/>・<see cref="GraphEventKey"/>・<see cref="GraphDestroyTrigger"/> など）の
    /// インスペクタに出す注意（次に何をすればよいか）を決める。
    /// </summary>
    public static class GameComponentHints
    {
        /// <summary>Graph Node Activator の注意。問題が無ければ空。</summary>
        /// <param name="hasGraph">グラフが入っているか。</param>
        /// <param name="nodeId">指しているノードの ID。</param>
        /// <param name="nodeExists">そのノードがグラフにあるか。</param>
        /// <param name="targetCount">Targets の数（空の欄も数える）。</param>
        /// <param name="targetsSelfOrParent">Targets に、自分の GameObject かその親があるか。</param>
        public static List<string> ForActivator(bool hasGraph, string nodeId, bool nodeExists, int targetCount, bool targetsSelfOrParent)
        {
            var hints = new List<string>();
            if (!hasGraph)
            {
                hints.Add("Assign the graph, then pick the node to show the targets on.");
            }
            else if (string.IsNullOrEmpty(nodeId))
            {
                hints.Add("Pick the node (or container) to show the targets on.");
            }
            else if (!nodeExists)
            {
                hints.Add("The chosen node is no longer in this graph. Pick another one.");
            }

            if (targetCount == 0)
            {
                hints.Add("Add the GameObjects to show while the graph is on the node (for example a pause panel).");
            }

            if (targetsSelfOrParent)
            {
                hints.Add("A target is this GameObject or one of its parents. Hiding it would turn this component off too, so it is skipped. " +
                          "Put this component on an object that stays active (such as the Canvas).");
            }

            return hints;
        }

        /// <summary>Graph Parameter Text の注意。問題が無ければ空。</summary>
        /// <param name="hasTextComponent">同じ GameObject にテキスト（public な string の text）があるか。</param>
        /// <param name="graphParameters">グラフのパラメータ名。グラフ未設定なら null。</param>
        /// <param name="parameter">指しているパラメータ名。</param>
        public static List<string> ForParameterText(bool hasTextComponent, IReadOnlyCollection<string> graphParameters, string parameter)
        {
            var hints = new List<string>();
            if (!hasTextComponent)
            {
                hints.Add("No text on this GameObject. Add a UI Text (or TextMeshPro text) here.");
            }

            if (string.IsNullOrEmpty(parameter))
            {
                hints.Add(graphParameters == null ? "Type the parameter's name, or assign the graph to pick it from a list." : "Choose the parameter to show.");
            }
            else if (graphParameters != null && !graphParameters.Contains(parameter, StringComparer.Ordinal))
            {
                hints.Add($"'{parameter}' is not a parameter of this graph, so the text will not change.");
            }

            return hints;
        }

        /// <summary>Graph Event Key の注意。問題が無ければ空。</summary>
        /// <param name="graphEvents">送り先のグラフにある Raise できるイベント名。グラフ未設定なら null。</param>
        /// <param name="eventName">設定されているイベント名。</param>
        /// <param name="action">送る操作。</param>
        /// <param name="key">キー。</param>
        public static List<string> ForEventKey(IReadOnlyList<string> graphEvents, string eventName, GraphEventKeyAction action, KeyCode key)
        {
            var hints = new List<string>();
            if (key == KeyCode.None)
            {
                hints.Add("Choose the key.");
            }

            if (action == GraphEventKeyAction.Raise)
            {
                if (string.IsNullOrEmpty(eventName))
                {
                    hints.Add("Choose the event this key raises.");
                }
                else if (graphEvents != null && !graphEvents.Contains(eventName, StringComparer.Ordinal))
                {
                    hints.Add(graphEvents.Count == 0
                        ? $"The graph has no events yet. Add an Event node named '{eventName}' where this key should move the flow on."
                        : $"'{eventName}' is not an event in this graph, so the key will do nothing.");
                }
            }

            return hints;
        }

        /// <summary>
        /// パラメータを変えてイベントを Raise するコンポーネント（Graph Destroy Trigger・Graph Trigger 2D）の注意。問題が無ければ空。
        /// </summary>
        /// <param name="graphEvents">グラフにある Raise できるイベント名。グラフ未設定なら null。</param>
        /// <param name="eventName">設定されているイベント名。</param>
        /// <param name="changeCount">パラメータの変更の数（空の行も数える）。</param>
        /// <param name="changeProblems">変更の問題（パラメータが無いなど。グラフがあるときだけ）。</param>
        public static List<string> ForReaction(IReadOnlyList<string> graphEvents, string eventName, int changeCount, IEnumerable<string> changeProblems)
        {
            var hints = new List<string>();
            if (string.IsNullOrEmpty(eventName) && changeCount == 0)
            {
                hints.Add("Add a parameter change or choose an event, or this does nothing.");
            }

            if (graphEvents == null && changeCount > 0)
            {
                hints.Add("Set the graph to change its parameters. Without it, only the event is raised.");
            }

            if (!string.IsNullOrEmpty(eventName) && graphEvents != null && !graphEvents.Contains(eventName, StringComparer.Ordinal))
            {
                hints.Add(graphEvents.Count == 0
                    ? $"The graph has no events yet. Add an Event node named '{eventName}' where the flow should move on."
                    : $"'{eventName}' is not an event in this graph, so nothing moves on.");
            }

            foreach (var problem in changeProblems ?? Enumerable.Empty<string>())
            {
                hints.Add($"A parameter change is skipped: {problem}.");
            }

            return hints;
        }
    }
}
