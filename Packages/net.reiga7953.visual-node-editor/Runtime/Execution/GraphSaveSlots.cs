using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// セーブのスロット。名前を付けてファイルに保存し、そこから再開する
    /// （<c>Application.persistentDataPath/VisualNodeEditor/グラフ名/スロット.json</c>）。
    /// 別の場所に保存したいときは <see cref="GraphRunner.CaptureState"/> と <see cref="GraphRunnerState.ToJson"/> を直接使う。
    /// </summary>
    public static class GraphSaveSlots
    {
        private const int MaxSlotLength = 64;

        /// <summary>保存先のフォルダ（テストで差し替える）。null なら persistentDataPath の下。</summary>
        internal static string RootOverride;

        /// <summary>スロットを置くフォルダ。</summary>
        public static string Root => RootOverride ?? Path.Combine(Application.persistentDataPath, "VisualNodeEditor");

        /// <summary>
        /// スロット名に使えるか（空でなく、英数字・空白・'-'・'_' だけで、前後に空白が無く、64 文字まで。
        /// Windows がファイル名に使わせない CON・NUL・COM1 などは不可）。
        /// </summary>
        public static bool IsValidSlotName(string slot) =>
            !string.IsNullOrWhiteSpace(slot) && slot.Length <= MaxSlotLength && slot.Trim() == slot
            && slot.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
            && !IsReservedName(slot);

        // Windows が装置の名前として予約している名前（拡張子 .json を付けても使えない。大文字小文字は問わない）。
        // 英数字以外の文字を含む予約名（CONIN$・COM¹ など）は、上の文字の決まりで既に断っている
        private static bool IsReservedName(string slot)
        {
            var upper = slot.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return true;
            }

            return upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal))
                                     && upper[3] >= '0' && upper[3] <= '9';
        }

        /// <summary>そのグラフのスロットのファイルのパス。</summary>
        /// <exception cref="ArgumentException">スロット名が使えない。</exception>
        public static string GetPath(NodeGraphAsset graph, string slot)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (!IsValidSlotName(slot))
            {
                throw new ArgumentException($"'{slot}' is not a usable slot name. Use letters, digits, spaces, '-' and '_'.", nameof(slot));
            }

            return Path.Combine(Root, ToFolderName(graph.name), slot + ".json");
        }

        /// <summary>そのスロットにセーブがあるか。</summary>
        public static bool Exists(NodeGraphAsset graph, string slot) => IsValidSlotName(slot) && graph != null && File.Exists(GetPath(graph, slot));

        /// <summary>
        /// <paramref name="runner"/> の今の状態を保存する（同じスロットは上書き）。書きかけのファイルが残らないよう、一時ファイルに書いてから置き換える。
        /// </summary>
        /// <exception cref="InvalidOperationException">Runner が実行中でない。</exception>
        public static void Save(GraphRunner runner, string slot)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            var path = GetPath(runner.Graph, slot);
            var json = runner.CaptureState().ToJson();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporary, path);
        }

        /// <summary>そのスロットのセーブ。無ければ null。</summary>
        /// <exception cref="ArgumentException">ファイルがセーブとして読めない。</exception>
        public static GraphRunnerState Read(NodeGraphAsset graph, string slot) =>
            Exists(graph, slot) ? GraphRunnerState.FromJson(File.ReadAllText(GetPath(graph, slot))) : null;

        /// <summary>
        /// スロットから再開する（<see cref="GraphRunner.RestoreState"/>。止まっていても始められる）。
        /// スロットが無い・保存したノードがグラフに無ければ false。
        /// </summary>
        public static bool Load(GraphRunner runner, string slot)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            var state = Read(runner.Graph, slot);
            return state != null && runner.RestoreState(state);
        }

        /// <summary>スロットを消す。無ければ false。</summary>
        public static bool Delete(NodeGraphAsset graph, string slot)
        {
            if (!Exists(graph, slot))
            {
                return false;
            }

            File.Delete(GetPath(graph, slot));
            return true;
        }

        // グラフ名をフォルダ名に使える形にする（使えない文字は '_'）
        private static string ToFolderName(string graphName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((graphName ?? string.Empty).Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return name.Length > 0 ? name : "Graph";
        }
    }
}
