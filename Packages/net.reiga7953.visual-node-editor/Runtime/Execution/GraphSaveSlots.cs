using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// セーブのスロット。名前を付けてファイルに保存し、そこから再開する
    /// （<c>Application.persistentDataPath/VisualNodeEditor/グラフ名-セーブキー/スロット.json</c>）。
    /// 別の場所に保存したいときは <see cref="GraphRunner.CaptureState"/> と <see cref="GraphRunnerState.ToJson"/> を直接使う。
    /// </summary>
    public static class GraphSaveSlots
    {
        private const int MaxSlotLength = 64;

        /// <summary>保存先のフォルダ（テストで差し替える）。null なら persistentDataPath の下。</summary>
        internal static string RootOverride;

        /// <summary>今のセーブを一時ファイルで 1 回で置き換える（テストで、<see cref="File.Replace(string, string, string)"/> が使えない場所を真似るのに差し替える）。</summary>
        internal static Action<string, string> ReplaceFile = (source, destination) => File.Replace(source, destination, null);

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

            return Path.Combine(Root, ToFolderName(graph), slot + ".json");
        }

        /// <summary>そのスロットにセーブがあるか。</summary>
        public static bool Exists(NodeGraphAsset graph, string slot) => IsValidSlotName(slot) && graph != null && File.Exists(GetPath(graph, slot));

        /// <summary>
        /// <paramref name="runner"/> の今の状態を保存する（同じスロットは上書き）。一時ファイルに書いてから 1 回で置き換える（書きかけのファイルも、スロットが無い瞬間も作らない）。
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

            // 古いセーブを消してから移すと、その間に落ちた・移すのに失敗したときにスロットが無くなるので、1 回で置き換える
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            try
            {
                ReplaceFile(temporary, path);
            }
            catch (Exception exception) when (exception is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
            {
                // File.Replace が使えない（対応しないファイルシステム・プラットフォーム）。消してから移す
                File.Delete(path);
                File.Move(temporary, path);
            }
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

        // グラフの置き場のフォルダ名: グラフ名（使えない文字は '_'）とセーブキー（同じ名前の別のグラフと混ざらないように。空ならグラフ名だけ）
        private static string ToFolderName(NodeGraphAsset graph)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((graph.name ?? string.Empty).Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            if (name.Length == 0)
            {
                name = "Graph";
            }

            var key = new string(graph.SaveKey.Where(c => !invalid.Contains(c)).ToArray());
            return key.Length > 0 ? $"{name}-{key}" : name;
        }
    }
}
