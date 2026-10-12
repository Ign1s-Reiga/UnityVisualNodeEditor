namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphEventKey"/> がキーで行う操作。</summary>
    public enum GraphEventKeyAction
    {
        /// <summary>イベント名で遷移する（<see cref="GraphRunner.Raise"/>）。</summary>
        Raise,

        /// <summary>イベントを介さずに次のノードへ進む（<see cref="GraphRunner.Advance"/>）。</summary>
        Advance,
    }
}
