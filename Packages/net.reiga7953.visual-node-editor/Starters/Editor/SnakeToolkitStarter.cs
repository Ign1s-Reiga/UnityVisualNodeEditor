using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スネークゲームのひな形の UI Toolkit 版（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Snake Starter (UI Toolkit)</c>）。
    /// グラフとキーは uGUI 版（<see cref="SnakeStarter"/>）と同じ。Game の画面はスコアの Graph Parameter Label、盤（<c>board</c>）、
    /// Ready・Paused・Game Over の Graph Node Panel。ゲームのスクリプトはひな形 <c>SnakeGameToolkitTemplate</c>（盤の中に VisualElement で描く）。
    /// </summary>
    internal sealed class SnakeToolkitStarter : ToolkitGameStarter
    {
        /// <summary>1 つだけ使う。</summary>
        internal static readonly SnakeToolkitStarter Instance = new SnakeToolkitStarter();

        internal override GameStarter Game => SnakeStarter.Instance;

        internal override string StarterName => "Snake Starter (UI Toolkit)";

        internal override string TemplateClass => "SnakeGameToolkitTemplate";

        [MenuItem("Assets/Create/Visual Node Editor/Snake Starter (UI Toolkit)", priority = 84)]
        private static void CreateFromMenuItem() => Instance.CreateFromMenu();

        internal override string GameScreenBody(NodeGraphAsset graph) =>
            "        <ui:VisualElement name=\"hud\" class=\"vne-starter-hud\">\n" +
            $"            {StarterScreens.ParameterLabel("score", SnakeStarter.ScoreParameter, "Score: {0}", "Score: 0")}\n" +
            $"            {StarterScreens.ParameterLabel("best", SnakeStarter.HighScoreParameter, "Best: {0}", "Best: 0")}\n" +
            "        </ui:VisualElement>\n" +
            "        <ui:VisualElement name=\"board\" class=\"vne-starter-board\" />\n" +
            $"        {StarterScreens.Label(Controls, "vne-starter-controls")}\n" +
            $"        <vne:GraphNodePanel name=\"ready-panel\" node=\"{SnakeStarter.ReadyNode}\" class=\"vne-starter-panel vne-starter-panel--clear\">\n" +
            $"            {StarterScreens.Label("Press Space to start", "vne-starter-panel__heading")}\n" +
            "        </vne:GraphNodePanel>\n" +
            $"        <vne:GraphNodePanel name=\"paused-panel\" node=\"{SnakeStarter.PausedNode}\" class=\"vne-starter-panel\">\n" +
            $"            {StarterScreens.Label("Paused", "vne-starter-panel__heading")}\n" +
            $"            {StarterScreens.Button("resume", "Resume", SnakeStarter.PauseEvent)}\n" +
            $"            {StarterScreens.Button("quit", "Quit", SnakeStarter.QuitEvent)}\n" +
            "        </vne:GraphNodePanel>\n" +
            $"        <vne:GraphNodePanel name=\"game-over-panel\" node=\"{SnakeStarter.GameOverNode}\" class=\"vne-starter-panel\">\n" +
            $"            {StarterScreens.Label("Game Over", "vne-starter-panel__heading")}\n" +
            $"            {StarterScreens.ParameterLabel("final-score", SnakeStarter.ScoreParameter, "Score: {0}", "Score: 0", "vne-starter-panel__text")}\n" +
            $"            {StarterScreens.Button("retry", "Retry", SnakeStarter.RetryEvent)}\n" +
            $"            {StarterScreens.Button("title", "Title", SnakeStarter.TitleEvent)}\n" +
            $"            {StarterScreens.Label("Enter: retry", "vne-starter-panel__hint")}\n" +
            "        </vne:GraphNodePanel>\n";

        internal override void AddGameKeys(GameObject keys, NodeGraphAsset graph) => SnakeStarter.AddKeys(keys, graph);
    }
}
