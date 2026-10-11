namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphEventButton"/> が押されたときに Runner へ送る操作。</summary>
    public enum GraphEventButtonAction
    {
        /// <summary>イベント名で遷移する（<see cref="GraphRunner.Raise"/>）。</summary>
        Raise,

        /// <summary>イベントを介さずに次のノードへ進む（<see cref="GraphRunner.Advance"/>）。</summary>
        Advance,

        /// <summary>今の状態をスロットに保存する（<see cref="GraphSaveSlots.Save"/>）。</summary>
        Save,

        /// <summary>スロットから再開する（<see cref="GraphSaveSlots.Load"/>。タイトル画面の「続きから」など）。</summary>
        Load,
    }
}
