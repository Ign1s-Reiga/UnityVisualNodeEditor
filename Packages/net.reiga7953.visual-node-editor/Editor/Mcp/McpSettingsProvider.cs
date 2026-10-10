using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// <c>Edit &gt; Preferences &gt; Visual Node Editor</c>: MCP サーバーの有効・ポート・状態と、Claude Code の設定の例。
    /// 見た目は McpPreferences.uxml / .uss。
    /// </summary>
    public static class McpSettingsProvider
    {
        private const string RunningClassName = "vne-mcp-preferences__status--running";
        private const string ErrorClassName = "vne-mcp-preferences__status--error";

        /// <summary>Preferences の「Visual Node Editor」ページ（ユーザーごとの設定）。</summary>
        [SettingsProvider]
        public static SettingsProvider Create() => new("Preferences/Visual Node Editor", SettingsScope.User)
        {
            label = "Visual Node Editor",
            activateHandler = (_, root) => Build(root),
            keywords = new HashSet<string> { "MCP", "AI", "Claude", "agent", "server" },
        };

        /// <summary>Claude Code の <c>.mcp.json</c> に書く設定（そのポートで）。</summary>
        public static string GetClientConfig(int port) =>
            "{\n" +
            "  \"mcpServers\": {\n" +
            $"    \"{McpServerHost.ServerName}\": {{\n" +
            "      \"type\": \"http\",\n" +
            $"      \"url\": \"http://127.0.0.1:{port}{McpHttpServer.EndpointPath}\"\n" +
            "    }\n" +
            "  }\n" +
            "}";

        /// <summary>状態の文（動いている・止まっている・始められなかった）。</summary>
        public static string GetStatusText(bool enabled, bool running, string error, string url)
        {
            if (!enabled)
            {
                return "Stopped (off by default).";
            }

            return running ? $"Running at {url}" : error ?? "Starting…";
        }

        private static void Build(VisualElement root)
        {
            var uxml = Resources.Load<VisualTreeAsset>("VisualNodeEditor/McpPreferences");
            var uss = Resources.Load<StyleSheet>("VisualNodeEditor/McpPreferences");
            if (uxml == null)
            {
                return;
            }

            uxml.CloneTree(root);
            if (uss != null)
            {
                root.styleSheets.Add(uss);
            }

            var enabled = root.Q<Toggle>("mcp-enabled");
            var port = root.Q<IntegerField>("mcp-port");
            var status = root.Q<Label>("mcp-status");
            var config = root.Q<TextField>("mcp-config");
            config.isReadOnly = true;

            void Refresh()
            {
                enabled.SetValueWithoutNotify(McpSettings.Enabled);
                port.SetValueWithoutNotify(McpSettings.Port);
                status.text = GetStatusText(McpSettings.Enabled, McpServerHost.IsRunning, McpServerHost.LastError, McpServerHost.Url);
                status.EnableInClassList(RunningClassName, McpSettings.Enabled && McpServerHost.IsRunning);
                status.EnableInClassList(ErrorClassName, McpSettings.Enabled && McpServerHost.LastError != null);
                config.SetValueWithoutNotify(GetClientConfig(McpSettings.Port));
            }

            enabled.RegisterValueChangedCallback(evt =>
            {
                McpSettings.Enabled = evt.newValue;
                McpServerHost.Apply();
            });
            port.RegisterValueChangedCallback(evt =>
            {
                McpSettings.Port = evt.newValue;
                McpServerHost.Apply();
            });

            McpServerHost.StateChanged += Refresh;
            root.RegisterCallback<DetachFromPanelEvent>(_ => McpServerHost.StateChanged -= Refresh);
            Refresh();
        }
    }
}
