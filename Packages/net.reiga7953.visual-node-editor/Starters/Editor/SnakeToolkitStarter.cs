using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スネークゲームのひな形の UI Toolkit 版（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Snake Starter (UI Toolkit)</c>）。1 つのシーンで、画面をノードに付ける。
    /// <code>
    /// Entry → [Scene: Main] ─(自動)→ [State: Title]（画面 Title）─(Start)→ [Container: Round]（画面 Game）─(GameOver)→ [State: Game Over]（画面 Game Over）
    /// </code>
    /// Round の中とその先は uGUI 版（<see cref="SnakeStarter"/>）と同じ。Ready（画面 Ready: 案内だけ）と Paused（画面 Paused: 暗くしてボタン）は Game の上に重なる。
    /// Game の画面はスコアの Graph Parameter Label と盤（<c>board</c>）。ゲームのスクリプトはひな形 <c>SnakeGameToolkitTemplate</c>（盤の中に VisualElement で描く）。
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

        internal override NodeGraphAsset BuildGraph()
        {
            var builder = new NodeGraphBuilder();
            var score = SnakeStarter.AddParameters(builder);
            var title = AddMainAndTitle(builder);
            var round = SnakeStarter.AddRound(builder, score, title, 1250f);
            builder.Connect(title, builder.AddEvent(StartEvent, 1000f, 0f), round);
            return builder.Graph;
        }

        internal override IEnumerable<(string Name, string Node, string Uxml)> GameScreens(string styleFile)
        {
            yield return ("Game", SnakeStarter.RoundContainer, StarterScreens.Screen(styleFile,
                "        <ui:VisualElement name=\"hud\" class=\"vne-starter-hud\">\n" +
                $"            {StarterScreens.ParameterLabel("score", SnakeStarter.ScoreParameter, "Score: {0}", "Score: 0")}\n" +
                $"            {StarterScreens.ParameterLabel("best", SnakeStarter.HighScoreParameter, "Best: {0}", "Best: 0")}\n" +
                "        </ui:VisualElement>\n" +
                "        <ui:VisualElement name=\"board\" class=\"vne-starter-board\" />\n" +
                $"        {StarterScreens.Label(Controls, "vne-starter-controls")}\n"));

            // Ready: 盤の上に案内だけ（盤は見えるまま）。Paused: 暗くしてボタン
            yield return ("Ready", SnakeStarter.ReadyNode, StarterScreens.Overlay(styleFile,
                $"        {StarterScreens.Label("Press Space to start", "vne-starter-panel__heading")}\n",
                "vne-starter-panel vne-starter-panel--clear", passClicks: true));
            yield return ("Paused", SnakeStarter.PausedNode, StarterScreens.Overlay(styleFile,
                $"        {StarterScreens.Label("Paused", "vne-starter-panel__heading")}\n" +
                $"        {StarterScreens.Button("resume", "Resume", SnakeStarter.PauseEvent)}\n" +
                $"        {StarterScreens.Button("quit", "Quit", SnakeStarter.QuitEvent)}\n"));

            // Game Over は Round の外なので、盤の代わりに出る
            yield return ("Game Over", SnakeStarter.GameOverNode, StarterScreens.Screen(styleFile,
                $"        {StarterScreens.Label("Game Over", "vne-starter-panel__heading")}\n" +
                $"        {StarterScreens.ParameterLabel("final-score", SnakeStarter.ScoreParameter, "Score: {0}", "Score: 0", "vne-starter-panel__text")}\n" +
                $"        {StarterScreens.ParameterLabel("final-best", SnakeStarter.HighScoreParameter, "Best: {0}", "Best: 0", "vne-starter-panel__text")}\n" +
                $"        {StarterScreens.Button("retry", "Retry", SnakeStarter.RetryEvent)}\n" +
                $"        {StarterScreens.Button("title", "Title", SnakeStarter.TitleEvent)}\n" +
                $"        {StarterScreens.Label("Enter: retry", "vne-starter-panel__hint")}\n"));
        }

        internal override void AddGameKeys(GameObject keys, NodeGraphAsset graph) => SnakeStarter.AddKeys(keys, graph);
    }
}
