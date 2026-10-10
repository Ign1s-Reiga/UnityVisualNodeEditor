using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP サーバーの設定（ユーザーごと。<see cref="EditorPrefs"/> に保存し、プロジェクトには入れない）。既定では無効。
    /// </summary>
    public static class McpSettings
    {
        /// <summary>既定のポート。</summary>
        public const int DefaultPort = 8790;

        private const string EnabledKey = "Reiga.VisualNodeEditor.Mcp.Enabled";
        private const string PortKey = "Reiga.VisualNodeEditor.Mcp.Port";

        /// <summary>サーバーを動かすか。</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>待ち受けるポート（1024〜65535。範囲外なら既定）。</summary>
        public static int Port
        {
            get => Clamp(EditorPrefs.GetInt(PortKey, DefaultPort));
            set => EditorPrefs.SetInt(PortKey, Clamp(value));
        }

        /// <summary>使えるポートに直す（範囲外なら既定）。</summary>
        public static int Clamp(int port) => port >= 1024 && port <= 65535 ? port : DefaultPort;
    }
}
