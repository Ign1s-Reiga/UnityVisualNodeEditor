using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// エディタの中の MCP サーバーを、設定（<see cref="McpSettings"/>）に合わせて動かす・止める。
    /// 既定では止まっている。ドメインリロードの前に止め、後に（有効なら）また始める。エディタの終了時に止める。
    /// </summary>
    [InitializeOnLoad]
    public static class McpServerHost
    {
        /// <summary>MCP の <c>serverInfo.name</c>（Claude Code の <c>.mcp.json</c> の名前と同じにしておく）。</summary>
        public const string ServerName = "visual-node-editor";

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private const string Instructions =
            "Tools for the Visual Node Editor (net.reiga7953.visual-node-editor) running inside the Unity editor. " +
            "Graphs are Node Graph assets referenced by asset path. Use list_graphs, then get_graph to read nodes, ports and edges. " +
            "Edits are recorded for Undo and saved. Run validate_graph after editing. Runtime tools work only in Play mode.";

        // 始められなかったとき、何度・どれだけ待ってやり直すか
        private const int MaxRetries = 3;
        private const double RetryDelaySeconds = 1.0;

        private static McpHttpServer _server;
        private static int _retriesLeft;
        private static double _retryAt;

        static McpServerHost()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;

            // 設定を読めるようになってから（起動直後・ドメインリロード後）始める
            EditorApplication.delayCall += Apply;
        }

        /// <summary>動いているか。</summary>
        public static bool IsRunning => _server != null && _server.IsRunning;

        /// <summary>クライアントに設定する URL（設定のポートで）。</summary>
        public static string Url => $"http://127.0.0.1:{McpSettings.Port}{McpHttpServer.EndpointPath}";

        /// <summary>最後に始められなかった理由（ポートが使われているなど）。無ければ null。</summary>
        public static string LastError { get; private set; }

        /// <summary>動き出した・止まった・始められなかったとき（設定画面の表示の更新用）。</summary>
        public static event Action StateChanged;

        /// <summary>
        /// 設定に合わせる（有効なら設定のポートで始め直し、無効なら止める）。始められなければ、少し待って何度かやり直す
        /// （ドメインリロードの直後は、前のポートがまだ放されていないことがあるため）。
        /// </summary>
        public static void Apply()
        {
            _retriesLeft = MaxRetries;
            TryStart();
        }

        /// <summary>止める（設定は変えない。やり直しの予定も取り消す）。</summary>
        public static void Stop()
        {
            EditorApplication.update -= RetryWhenDue;
            _server?.Stop();
            _server = null;
        }

        private static void TryStart()
        {
            Stop();
            if (!McpSettings.Enabled)
            {
                LastError = null;
                StateChanged?.Invoke();
                return;
            }

            var protocol = new McpProtocol(ServerName, GetVersion(), CreateTools(), Instructions);
            var server = new McpHttpServer(McpSettings.Port, body => McpMainThread.Run(() => protocol.Handle(body), RequestTimeout));
            try
            {
                server.Start();
                _server = server;
                LastError = null;
            }
            catch (Exception exception)
            {
                LastError = $"Could not listen on port {McpSettings.Port}: {exception.Message}";
                if (_retriesLeft-- > 0)
                {
                    _retryAt = EditorApplication.timeSinceStartup + RetryDelaySeconds;
                    EditorApplication.update += RetryWhenDue;
                }
                else
                {
                    Debug.LogWarning($"[VisualNodeEditor] MCP server: {LastError}");
                }
            }

            StateChanged?.Invoke();
        }

        private static void RetryWhenDue()
        {
            if (EditorApplication.timeSinceStartup < _retryAt)
            {
                return;
            }

            EditorApplication.update -= RetryWhenDue;
            TryStart();
        }

        /// <summary>サーバーが出すツール（グラフを読む・書き換える・Play 中の流れ）。</summary>
        public static List<McpTool> CreateTools() =>
            GraphReadTools.Create().Concat(GraphEditTools.Create()).Concat(RuntimeTools.Create()).ToList();

        private static string GetVersion() =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(McpServerHost).Assembly)?.version ?? "0.0.0";
    }
}
