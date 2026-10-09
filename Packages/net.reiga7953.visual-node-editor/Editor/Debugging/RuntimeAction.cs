namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>Play 中に、現在のノードからエディタで起こせる操作 1 つ（ボタン 1 つ）。</summary>
    public sealed class RuntimeAction
    {
        private RuntimeAction(RuntimeActionKind kind, string eventName, string label)
        {
            Kind = kind;
            EventName = eventName;
            Label = label;
        }

        /// <summary>操作の種類。</summary>
        public RuntimeActionKind Kind { get; }

        /// <summary>Raise するイベント名（Advance なら空）。</summary>
        public string EventName { get; }

        /// <summary>ボタンに出す文字列。</summary>
        public string Label { get; }

        /// <summary>イベント名で遷移する操作。</summary>
        public static RuntimeAction Raise(string eventName) => new(RuntimeActionKind.Raise, eventName, eventName);

        /// <summary>次のノード（<paramref name="nextTitle"/>）へ進む操作。</summary>
        public static RuntimeAction Advance(string nextTitle) => new(RuntimeActionKind.Advance, string.Empty, "Advance → " + nextTitle);

        /// <summary>この操作を Runner に送る。遷移したら true。</summary>
        public bool Run(GraphRunner runner) =>
            runner != null && (Kind == RuntimeActionKind.Advance ? runner.Advance() : runner.Raise(EventName));
    }
}
