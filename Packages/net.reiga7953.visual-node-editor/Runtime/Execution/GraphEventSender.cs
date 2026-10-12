using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ボタンから実行中の Runner へ送る処理（Graph Event Button と UI Toolkit の Graph Button が共有する）。
    /// Runner を探し、Raise / Advance / Save / Load を行う。送れなければ開発ビルドで警告を出す（保存の失敗はビルドでも出す）。
    /// </summary>
    internal static class GraphEventSender
    {
        /// <summary>
        /// <paramref name="graph"/>（null なら実行中のどれか）の Runner へ送る。遷移した（または通知の後で遷移する）、保存した、ロードしたなら true。
        /// <paramref name="sender"/> は警告に出す名前、<paramref name="context"/> は警告を選べるようにする物（無ければ null）。
        /// </summary>
        internal static bool Send(NodeGraphAsset graph, GraphEventButtonAction action, string eventName, string slot, string sender, Object context)
        {
            var runner = GraphRunner.FindRunning(graph);
            if (runner == null)
            {
                Warn(graph != null
                    ? $"No Graph Runner is running '{graph.name}', so '{sender}' did nothing."
                    : $"No Graph Runner is running, so '{sender}' did nothing.", context);
                return false;
            }

            if (action == GraphEventButtonAction.Save || action == GraphEventButtonAction.Load)
            {
                return SendSlot(runner, action, slot, sender, context);
            }

            var moved = action == GraphEventButtonAction.Advance ? runner.Advance() : runner.Raise(eventName);
            if (!moved)
            {
                Warn(action == GraphEventButtonAction.Advance
                    ? $"'{sender}': there is nothing to advance to from '{runner.Current?.Title}'."
                    : string.IsNullOrEmpty(eventName)
                        ? $"'{sender}' has no event name to raise."
                        : $"'{sender}': '{eventName}' does not lead anywhere from '{runner.Current?.Title}'.", context);
            }

            return moved;
        }

        /// <summary>
        /// Load のボタンを押せないようにするか: <paramref name="disableWithoutSave"/> で Load のとき、グラフ（null なら実行中のどれかのグラフ）の
        /// スロットから再開できなければ true。グラフが空で実行中の Runner も無ければ、どのグラフのセーブか分からないので false。
        /// </summary>
        internal static bool ShouldDisableLoad(NodeGraphAsset graph, GraphEventButtonAction action, string slot, bool disableWithoutSave)
        {
            if (!disableWithoutSave || action != GraphEventButtonAction.Load)
            {
                return false;
            }

            var target = graph != null ? graph : GraphRunner.FindRunning(null)?.Graph;
            return target != null && !GraphSaveSlots.CanLoad(target, slot);
        }

        private static bool SendSlot(GraphRunner runner, GraphEventButtonAction action, string slot, string sender, Object context)
        {
            if (!GraphSaveSlots.IsValidSlotName(slot))
            {
                Warn($"'{sender}': '{slot}' is not a usable slot name. {GraphSaveSlots.SlotNameRule}", context);
                return false;
            }

            if (action == GraphEventButtonAction.Save)
            {
                try
                {
                    GraphSaveSlots.Save(runner, slot);
                    return true;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // ほかのプロセスがスロットを開いているなど（古いセーブはそのまま残る）。クリックの処理で例外にせず、ビルドでも伝える
                    Debug.LogWarning($"[VisualNodeEditor] '{sender}': could not save to slot '{slot}': {exception.Message}", context);
                    return false;
                }
            }

            if (!GraphSaveSlots.Load(runner, slot))
            {
                Warn($"'{sender}': there is no save in slot '{slot}' for '{runner.Graph.name}' (or its node is gone).", context);
                return false;
            }

            return true;
        }

        private static void Warn(string message, Object context)
        {
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning("[VisualNodeEditor] " + message, context);
            }
        }
    }
}
