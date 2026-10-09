namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>イベント名をコードへ持っていくときの文字列（名前を打ち直させない）。</summary>
    public static class EventNameTools
    {
        /// <summary><c>Raise("イベント名")</c> の呼び出し。C# の文字列リテラルになるよう <c>"</c> と <c>\</c> をエスケープする。</summary>
        public static string FormatRaiseCall(string eventName)
        {
            var escaped = (eventName ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"Raise(\"{escaped}\")";
        }
    }
}
