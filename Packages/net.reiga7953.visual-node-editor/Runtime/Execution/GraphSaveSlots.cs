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

        /// <summary>スロット名の決まり（エラー・警告・インスペクタのヒントの文に使う。<see cref="IsValidSlotName"/> と同じ決まり）。</summary>
        public static readonly string SlotNameRule =
            $"Use up to {MaxSlotLength} letters, digits, spaces, '-' and '_' (no space at either end), and not a name Windows reserves such as CON, NUL or COM1.";

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
                throw new ArgumentException($"'{slot}' is not a usable slot name. {SlotNameRule}", nameof(slot));
            }

            return Path.Combine(Root, ToFolderName(graph), slot + ".json");
        }

        /// <summary>
        /// そのスロットにセーブがあるか。キーを持つ前にグラフ名だけのフォルダへ保存した、このグラフのセーブも含む
        /// （見つけたらキーのフォルダへ移す。<see cref="Read"/>・<see cref="Load"/>・<see cref="Delete"/> も同じ）。
        /// </summary>
        public static bool Exists(NodeGraphAsset graph, string slot) => FindFile(graph, slot) != null;

        /// <summary>
        /// <paramref name="runner"/> の今の状態を保存する（同じスロットは上書き）。一時ファイルに書いてから 1 回で置き換える（書きかけのファイルも、スロットが無い瞬間も作らない）。
        /// </summary>
        /// <exception cref="InvalidOperationException">Runner が実行中でない。</exception>
        /// <exception cref="IOException">ほかのプロセスがスロットを開いているなどで置き換えられない（古いセーブはそのまま残る）。</exception>
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
            catch (NotSupportedException)
            {
                // File.Replace に対応しないプラットフォーム。ほかに置き換える手が無いので、消してから移す
                File.Delete(path);
                File.Move(temporary, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                // 置き換えの途中で失敗し、古いセーブが別の名前に退けられた（Windows の ReplaceFile）。スロットを空けないよう、新しいセーブを移す。
                // ほかのプロセスが開いているなど、古いセーブが残っているときは、そのまま例外にする（一時的なロックでも消してから移すと、その間に落ちたときにスロットが無くなる）
                File.Move(temporary, path);
            }
        }

        /// <summary>そのスロットのセーブ。無ければ null。</summary>
        /// <exception cref="ArgumentException">ファイルがセーブとして読めない。</exception>
        public static GraphRunnerState Read(NodeGraphAsset graph, string slot)
        {
            var file = FindFile(graph, slot);
            return file != null ? GraphRunnerState.FromJson(File.ReadAllText(file)) : null;
        }

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
            var file = FindFile(graph, slot);
            if (file == null)
            {
                return false;
            }

            File.Delete(file);
            return true;
        }

        // そのスロットのファイル。無ければ null。
        // キーを持つグラフは、グラフ名だけのフォルダにキーを持つ前のこのグラフのセーブがあれば、先にキーのフォルダへ移す
        // （キーのフォルダにもうあれば、古いほうは要らないので消す。残すと、消したスロットが古いセーブで戻ってくる）
        private static string FindFile(NodeGraphAsset graph, string slot)
        {
            if (graph == null || !IsValidSlotName(slot))
            {
                return null;
            }

            var path = GetPath(graph, slot);
            var legacy = FindLegacyFile(graph, slot);
            if (legacy != null)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(legacy);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.Move(legacy, path);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // 移せなければ（ほかのプロセスが開いているなど）、今回はそのまま使う
                    if (!File.Exists(path))
                    {
                        return legacy;
                    }
                }
            }

            return File.Exists(path) ? path : null;
        }

        // キーを持つ前（グラフ名だけのフォルダ）に保存した、このグラフのセーブ。キーを持たないグラフ・セーブが無い・別のグラフのものなら null。
        // 同じ名前の別のグラフ（キーを持たない古いグラフ）も同じフォルダを使うので、今いたノードがこのグラフにあるかで見分ける（ノードの ID はグラフごとに違う）
        private static string FindLegacyFile(NodeGraphAsset graph, string slot)
        {
            if (graph.SaveKey.Length == 0)
            {
                return null;
            }

            var legacy = Path.Combine(Root, ToFolderName(graph, false), slot + ".json");
            return File.Exists(legacy) && IsSaveOf(legacy, graph) ? legacy : null;
        }

        // セーブの今いたノードがこのグラフにあるか。読めなければ false
        private static bool IsSaveOf(string file, NodeGraphAsset graph)
        {
            try
            {
                var state = GraphRunnerState.FromJson(File.ReadAllText(file));
                return state != null && graph.FindNode(state.NodeId) != null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return false;
            }
        }

        // グラフの置き場のフォルダ名: グラフ名（使えない文字は '_'）とセーブキー（同じ名前の別のグラフと混ざらないように。空ならグラフ名だけ）
        private static string ToFolderName(NodeGraphAsset graph, bool withKey = true)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((graph.name ?? string.Empty).Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            if (name.Length == 0)
            {
                name = "Graph";
            }

            var key = withKey ? new string(graph.SaveKey.Where(c => !invalid.Contains(c)).ToArray()) : string.Empty;
            return key.Length > 0 ? $"{name}-{key}" : name;
        }
    }
}
