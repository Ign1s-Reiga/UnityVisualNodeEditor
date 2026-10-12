using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スライドパズルのひな形の UI Toolkit 版（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Sliding Puzzle Starter (UI Toolkit)</c>）。
    /// グラフとキーは uGUI 版（<see cref="SlidingPuzzleStarter"/>）と同じ。Game の画面は Level・Moves の Graph Parameter Label、盤（<c>board</c>）、
    /// Paused・Solved・All Clear の Graph Node Panel。ゲームのスクリプトはひな形 <c>SlidingPuzzleGameToolkitTemplate</c>（盤の中に Button でタイルを置く）。
    /// </summary>
    internal sealed class SlidingPuzzleToolkitStarter : ToolkitGameStarter
    {
        /// <summary>1 つだけ使う。</summary>
        internal static readonly SlidingPuzzleToolkitStarter Instance = new SlidingPuzzleToolkitStarter();

        internal override GameStarter Game => SlidingPuzzleStarter.Instance;

        internal override string StarterName => "Sliding Puzzle Starter (UI Toolkit)";

        internal override string TemplateClass => "SlidingPuzzleGameToolkitTemplate";

        [MenuItem("Assets/Create/Visual Node Editor/Sliding Puzzle Starter (UI Toolkit)", priority = 85)]
        private static void CreateFromMenuItem() => Instance.CreateFromMenu();

        internal override string GameScreenBody(NodeGraphAsset graph) =>
            "        <ui:VisualElement name=\"hud\" class=\"vne-starter-hud\">\n" +
            $"            {StarterScreens.ParameterLabel("level", SlidingPuzzleStarter.LevelParameter, "Level {0}", "Level 1")}\n" +
            $"            {StarterScreens.ParameterLabel("moves", SlidingPuzzleStarter.MovesParameter, "Moves: {0}", "Moves: 0")}\n" +
            "        </ui:VisualElement>\n" +
            "        <ui:VisualElement name=\"board\" class=\"vne-starter-board\" />\n" +
            $"        {StarterScreens.Label(Controls, "vne-starter-controls")}\n" +
            $"        <vne:GraphNodePanel name=\"paused-panel\" node=\"{SlidingPuzzleStarter.PausedNode}\" class=\"vne-starter-panel\">\n" +
            $"            {StarterScreens.Label("Paused", "vne-starter-panel__heading")}\n" +
            $"            {StarterScreens.Button("resume", "Resume", SlidingPuzzleStarter.PauseEvent)}\n" +
            $"            {StarterScreens.Button("retry", "Retry", SlidingPuzzleStarter.RetryEvent)}\n" +
            $"            {StarterScreens.Button("quit", "Quit", SlidingPuzzleStarter.QuitEvent)}\n" +
            "        </vne:GraphNodePanel>\n" +
            $"        <vne:GraphNodePanel name=\"solved-panel\" node=\"{SlidingPuzzleStarter.SolvedNode}\" class=\"vne-starter-panel\">\n" +
            $"            {StarterScreens.Label("Solved!", "vne-starter-panel__heading")}\n" +
            $"            {StarterScreens.ParameterLabel("solved-moves", SlidingPuzzleStarter.MovesParameter, "in {0} moves", "in 0 moves", "vne-starter-panel__text")}\n" +
            $"            {StarterScreens.Button("next", "Next", SlidingPuzzleStarter.NextEvent)}\n" +
            $"            {StarterScreens.Label("Enter: next", "vne-starter-panel__hint")}\n" +
            "        </vne:GraphNodePanel>\n" +
            $"        <vne:GraphNodePanel name=\"all-clear-panel\" node=\"{SlidingPuzzleStarter.AllClearNode}\" class=\"vne-starter-panel\">\n" +
            $"            {StarterScreens.Label("All Clear!", "vne-starter-panel__heading")}\n" +
            $"            {StarterScreens.Button("title", "Title", SlidingPuzzleStarter.TitleEvent)}\n" +
            "        </vne:GraphNodePanel>\n";

        internal override void AddGameKeys(GameObject keys, NodeGraphAsset graph) => SlidingPuzzleStarter.AddKeys(keys, graph);
    }
}
