namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>Play 中に、現在のノードからエディタで起こせる操作の種類。</summary>
    public enum RuntimeActionKind
    {
        /// <summary>イベント名で遷移する（<see cref="GraphRunner.Raise"/>）。</summary>
        Raise,

        /// <summary>イベントを介さずに次へ進む（<see cref="GraphRunner.Advance"/>）。</summary>
        Advance,
    }
}
