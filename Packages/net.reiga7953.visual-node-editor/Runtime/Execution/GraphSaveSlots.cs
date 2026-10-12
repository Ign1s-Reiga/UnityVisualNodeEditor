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
        /// （<see cref="Read"/>・<see cref="Load"/> も同じ。読むだけで、ファイルは動かさない）。
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
            try
            {
                File.WriteAllText(temporary, json);
                MoveIntoSlot(temporary, path);
            }
            catch
            {
                // 保存できなかった（古いセーブはそのまま残る）。書いた一時ファイルは要らない
                TryDelete(temporary);
                throw;
            }

            // 前に保存の途中で落ちたときの .bak が残っていれば、もう要らない
            DeleteIfExists(BackupPath(path));
        }

        // 一時ファイルをスロットにする。古いセーブを消してから移すと、その間に落ちた・移すのに失敗したときにスロットが無くなるので、1 回で置き換える
        private static void MoveIntoSlot(string temporary, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            try
            {
                ReplaceFile(temporary, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                // 置き換えの途中で失敗し、古いセーブが別の名前に退けられた（Windows の ReplaceFile）。スロットを空けないよう、新しいセーブを移す
                File.Move(temporary, path);
            }
            catch (Exception exception) when (exception is NotSupportedException or IOException)
            {
                // File.Replace に対応しないプラットフォーム・ドライブ（一部のネットワークドライブ・リムーバブルドライブなど）
                ReplaceThroughBackup(temporary, path);
            }
        }

        // 古いセーブを .bak に退けてから一時ファイルを移し、.bak を消す。途中で落ちても、セーブか .bak のどちらかが残る（FindFile が .bak も探す）。
        // ほかのプロセスがスロットを開いていれば、退けるところで失敗し、古いセーブはそのまま残る
        private static void ReplaceThroughBackup(string temporary, string path)
        {
            var backup = BackupPath(path);
            DeleteIfExists(backup);
            File.Move(path, backup);
            try
            {
                File.Move(temporary, path);
            }
            catch
            {
                // 古いセーブを戻す。戻すのにも失敗したら .bak のまま残し（FindFile が読む）、元の失敗を伝える
                try
                {
                    File.Move(backup, path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }

                throw;
            }

            File.Delete(backup);
        }

        // 消せなくても構わないファイルを消す（失敗の後片付け。元の失敗を隠さない）
        private static void TryDelete(string path)
        {
            try
            {
                DeleteIfExists(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
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

        /// <summary>
        /// スロットを消す。無ければ false。保存の途中で落ちたときの .bak と、キーを持つ前のこのグラフのセーブも消す
        /// （残すと、消したスロットがそれで戻ってくる）。
        /// </summary>
        public static bool Delete(NodeGraphAsset graph, string slot)
        {
            if (FindFile(graph, slot) == null)
            {
                return false;
            }

            var path = GetPath(graph, slot);
            DeleteIfExists(path);
            DeleteIfExists(BackupPath(path));
            var legacy = FindLegacyFile(graph, slot);
            if (legacy != null)
            {
                File.Delete(legacy);
            }

            return true;
        }

        // そのスロットのファイル。無ければ null。読むだけで、ファイルは動かさない（消すのは Delete だけ）。
        // セーブが無ければ、保存の途中で落ちたときの .bak、キーを持つグラフならキーを持つ前のこのグラフのセーブの順に探す
        private static string FindFile(NodeGraphAsset graph, string slot)
        {
            if (graph == null || !IsValidSlotName(slot))
            {
                return null;
            }

            var path = GetPath(graph, slot);
            if (File.Exists(path))
            {
                return path;
            }

            var backup = BackupPath(path);
            return File.Exists(backup) ? backup : FindLegacyFile(graph, slot);
        }

        private static string BackupPath(string path) => path + ".bak";

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
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
