using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スライドパズルのひな形の UI Toolkit 版（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Sliding Puzzle Starter (UI Toolkit)</c>）。1 つのシーンで、画面をノードに付ける。
    /// <code>
    /// Entry → [Scene: Main] ─(自動)→ [State: Title]（画面 Title。Level = 1）─(Start)→ [Container: Levels]（画面 Game）─(AllClear)→ [State: All Clear]（画面 All Clear）
    /// </code>
    /// Levels の中とその先は uGUI 版（<see cref="SlidingPuzzleStarter"/>）と同じ。Paused と Solved の画面は Game の上に重なる（暗くしてボタン）。
    /// Game の画面は Level・Moves の Graph Parameter Label と盤（<c>board</c>）。ゲームのスクリプトはひな形 <c>SlidingPuzzleGameToolkitTemplate</c>（盤の中に Button でタイルを置く）。
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

        internal override NodeGraphAsset BuildGraph()
        {
            var builder = new StarterGraphBuilder();
            var level = SlidingPuzzleStarter.AddLevelParameter(builder);
            var title = AddMainAndTitle(builder);
            builder.SetOnEnter(title, new ParameterChange(level.Id) { IntValue = 1 });
            var levels = SlidingPuzzleStarter.AddLevels(builder, level, title, 1250f);
            builder.Connect(title, builder.AddEvent(StartEvent, 1000f, 0f), levels);
            return builder.Graph;
        }

        internal override IEnumerable<(string Name, string Node, string Uxml)> GameScreens(string styleFile)
        {
            // Game は Levels に付ける（次のレベルへ移る間も出したまま）
            yield return ("Game", SlidingPuzzleStarter.LevelsContainer, StarterScreens.Screen(styleFile,
                "        <ui:VisualElement name=\"hud\" class=\"vne-starter-hud\">\n" +
                $"            {StarterScreens.ParameterLabel("level", SlidingPuzzleStarter.LevelParameter, "Level {0}", "Level 1")}\n" +
                $"            {StarterScreens.ParameterLabel("moves", SlidingPuzzleStarter.MovesParameter, "Moves: {0}", "Moves: 0")}\n" +
                "        </ui:VisualElement>\n" +
                "        <ui:VisualElement name=\"board\" class=\"vne-starter-board\" />\n" +
                $"        {StarterScreens.Label(Controls, "vne-starter-controls")}\n"));
            yield return ("Paused", SlidingPuzzleStarter.PausedNode, StarterScreens.Overlay(styleFile,
                $"        {StarterScreens.Label("Paused", "vne-starter-panel__heading")}\n" +
                $"        {StarterScreens.Button("resume", "Resume", SlidingPuzzleStarter.PauseEvent)}\n" +
                $"        {StarterScreens.Button("retry", "Retry", SlidingPuzzleStarter.RetryEvent)}\n" +
                $"        {StarterScreens.Button("quit", "Quit", SlidingPuzzleStarter.QuitEvent)}\n"));
            yield return ("Solved", SlidingPuzzleStarter.SolvedNode, StarterScreens.Overlay(styleFile,
                $"        {StarterScreens.Label("Solved!", "vne-starter-panel__heading")}\n" +
                $"        {StarterScreens.ParameterLabel("solved-moves", SlidingPuzzleStarter.MovesParameter, "in {0} moves", "in 0 moves", "vne-starter-panel__text")}\n" +
                $"        {StarterScreens.Button("next", "Next", SlidingPuzzleStarter.NextEvent)}\n" +
                $"        {StarterScreens.Label("Enter: next", "vne-starter-panel__hint")}\n"));

            // All Clear は Levels の外なので、盤の代わりに出る
            yield return ("All Clear", SlidingPuzzleStarter.AllClearNode, StarterScreens.Screen(styleFile,
                $"        {StarterScreens.Label("All Clear!", "vne-starter-panel__heading")}\n" +
                $"        {StarterScreens.Button("title", "Title", SlidingPuzzleStarter.TitleEvent)}\n"));
        }

        internal override void AddGameKeys(GameObject keys, NodeGraphAsset graph) => SlidingPuzzleStarter.AddKeys(keys, graph);
    }
}
