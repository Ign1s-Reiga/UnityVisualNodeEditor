using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スライドパズル（15 パズル）のひな形（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Sliding Puzzle Starter</c>）。コンテナを 2 段に入れ子にする。
    /// <code>
    /// Entry → [Scene: Title] ─(Start)→ [Scene: Game]（Level = 1）─(自動)→ [Container: Levels] ─(AllClear)→ [State: All Clear] ─(Title)→ [Scene: Title]
    ///                                                                                        └(Quit)→ [Scene: Title]
    /// Levels の中: Entry → [Container: Level] ─(Next)→ [State: Next Level]（Level += 1）─(自動)→ [Container: Level]
    ///                                         ├(AllClear)→ Exit (AllClear)
    ///                                         └(Quit)→ Exit (Quit)
    /// Level の中: Entry → [State: Shuffle]（Moves = 0）─(自動)→ [State: Playing] ─(Pause)→ [State: Paused]（Time Scale 0）─(Pause)→ [State: Playing]
    ///                                                           │                     ├(Retry)→ [State: Shuffle]
    ///                                                           │                     └(Quit)→ Exit (Quit)
    ///                                                           └(Solved)→ [State: Solved] ─(Next, Level &gt;= 3)→ Exit (AllClear)
    ///                                                                                      └(Next)→ Exit (Next)
    /// </code>
    /// ゲームのスクリプト（ひな形 <c>SlidingPuzzleGameTemplate</c>）は、Shuffle に入ったら Level から盤を作って混ぜ、Playing の間だけタイルを動かし、
    /// Moves を増やし、揃ったら Solved を Raise する。
    /// </summary>
    internal sealed class SlidingPuzzleStarter : GameStarter
    {
        /// <summary>1 つだけ使う。</summary>
        internal static readonly SlidingPuzzleStarter Instance = new SlidingPuzzleStarter();

        /// <summary>レベルの数（この Level で揃えたら All Clear。グラフの条件 <c>Level &gt;= 3</c>）。</summary>
        internal const int LastLevel = 3;

        internal const string LevelsContainer = "Levels";
        internal const string LevelContainer = "Level";
        internal const string ShuffleNode = "Shuffle";
        internal const string PlayingNode = "Playing";
        internal const string PausedNode = "Paused";
        internal const string SolvedNode = "Solved";
        internal const string NextLevelNode = "Next Level";
        internal const string AllClearNode = "All Clear";
        internal const string PauseEvent = "Pause";
        internal const string RetryEvent = "Retry";
        internal const string QuitEvent = "Quit";
        internal const string SolvedEvent = "Solved";
        internal const string NextEvent = "Next";
        internal const string TitleEvent = "Title";
        internal const string NextExit = "Next";
        internal const string AllClearExit = "AllClear";
        internal const string QuitExit = "Quit";
        internal const string LevelParameter = "Level";
        internal const string MovesParameter = "Moves";

        internal override string StarterName => "Sliding Puzzle Starter";

        internal override string BaseName => "Sliding Puzzle";

        internal override string TemplateClass => "SlidingPuzzleGameTemplate";

        internal override string Controls => "Click a tile next to the gap, or use the arrow keys    Esc: pause";

        [MenuItem("Assets/Create/Visual Node Editor/Sliding Puzzle Starter", priority = 83)]
        private static void CreateFromMenuItem() => Instance.CreateFromMenu();

        internal override NodeGraphAsset BuildGraph()
        {
            var builder = new StarterGraphBuilder();
            var level = builder.AddParameter(new GraphParameter(LevelParameter, GraphParameterType.Int) { IntValue = 1 });
            var moves = builder.AddParameter(new GraphParameter(MovesParameter, GraphParameterType.Int));

            // ルート: Title → Game（シーン。Level = 1）→ Levels → All Clear → Title
            var title = builder.Add(new SceneNode { Title = TitleSceneNode }, 250f, 0f);
            var game = builder.Add(new SceneNode { Title = GameSceneNode }, 750f, 0f);
            builder.SetOnEnter(game, new ParameterChange(level.Id) { IntValue = 1 });
            var levels = builder.AddContainer(LevelsContainer, 1250f, 0f, null, AllClearExit, QuitExit);
            var allClear = builder.Add(new StateNode { Title = AllClearNode, Description = "Every level is solved." }, 1500f, -100f);
            builder.Connect(builder.Entry, title);
            builder.Connect(title, builder.AddEvent(StartEvent, 500f, 0f), game);
            builder.Connect(game, builder.AddEvent(string.Empty, 1000f, 0f, automatic: true), levels);
            builder.ConnectExit(levels, AllClearExit, allClear);
            builder.ConnectExit(levels, QuitExit, title);
            builder.Connect(allClear, builder.AddEvent(TitleEvent, 1750f, -100f), title);

            // Levels の中: 1 つのレベル（Level）を解くたびに Level を 1 増やして、また Level に入る
            var levelContainer = builder.AddContainer(LevelContainer, 250f, 0f, levels, NextExit, AllClearExit, QuitExit);
            var nextLevel = builder.Add(new StateNode { Title = NextLevelNode }, 500f, -150f, levels);
            builder.SetOnEnter(nextLevel, new ParameterChange(level.Id, ParameterChangeOperation.Add) { IntValue = 1 });
            builder.Connect(builder.EntryOf(levels), levelContainer);
            builder.ConnectExit(levelContainer, NextExit, nextLevel);
            builder.Connect(nextLevel, builder.AddEvent(string.Empty, 750f, -150f, levels, automatic: true), levelContainer);
            builder.ConnectExit(levelContainer, AllClearExit, builder.AddExit(levels, AllClearExit, 500f, 0f));
            builder.ConnectExit(levelContainer, QuitExit, builder.AddExit(levels, QuitExit, 500f, 150f));

            // Level の中: 混ぜて遊ぶ。揃ったら、最後のレベルなら All Clear、そうでなければ次のレベルへ
            var shuffle = builder.Add(new StateNode { Title = ShuffleNode, Description = "The game script builds a new shuffled board for the Level." },
                250f, 0f, levelContainer);
            builder.SetOnEnter(shuffle, new ParameterChange(moves.Id) { IntValue = 0 });
            var playing = builder.Add(new StateNode { Title = PlayingNode, Description = "Tiles slide only here." }, 750f, 0f, levelContainer);
            var paused = builder.Add(new StateNode { Title = PausedNode }, 1250f, -150f, levelContainer);
            builder.TimeScaleWhileOn(paused, 0f);
            var solved = builder.Add(new StateNode { Title = SolvedNode }, 1250f, 150f, levelContainer);
            builder.Connect(builder.EntryOf(levelContainer), shuffle);
            builder.Connect(shuffle, builder.AddEvent(string.Empty, 500f, 0f, levelContainer, automatic: true), playing);
            builder.Connect(playing, builder.AddEvent(PauseEvent, 1000f, -150f, levelContainer), paused);
            builder.Connect(paused, builder.AddEvent(PauseEvent, 1000f, -300f, levelContainer), playing);
            builder.Connect(paused, builder.AddEvent(RetryEvent, 1500f, -250f, levelContainer), shuffle);
            builder.Connect(paused, builder.AddEvent(QuitEvent, 1500f, -100f, levelContainer), builder.AddExit(levelContainer, QuitExit, 1750f, -100f));
            builder.Connect(playing, builder.AddEvent(SolvedEvent, 1000f, 150f, levelContainer), solved);

            // 同じ名前の Event は、エッジの順に条件を見る（最後のレベルを先に、それ以外を後に）
            var toAllClear = builder.AddEvent(NextEvent, 1500f, 100f, levelContainer);
            toAllClear.Title = "Last level";
            toAllClear.AddCondition(new TransitionCondition(level.Id, ConditionOperator.GreaterOrEqual) { IntValue = LastLevel });
            builder.Connect(solved, toAllClear, builder.AddExit(levelContainer, AllClearExit, 1750f, 100f));
            builder.Connect(solved, builder.AddEvent(NextEvent, 1500f, 220f, levelContainer), builder.AddExit(levelContainer, NextExit, 1750f, 220f));
            return builder.Graph;
        }

        internal override GameObject BuildGame(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            StarterScenes.AddEventSystem(scene);
            var canvas = StarterScenes.AddCanvas(scene);

            StarterScenes.AddParameterText(canvas, "Level", graph, LevelParameter, "Level {0}", 44,
                new Vector2(-720f, 480f), new Vector2(400f, 80f), TextAnchor.MiddleLeft);
            StarterScenes.AddParameterText(canvas, "Moves", graph, MovesParameter, "Moves: {0}", 44,
                new Vector2(720f, 480f), new Vector2(400f, 80f), TextAnchor.MiddleRight);
            var board = StarterScenes.AddBoard(canvas, 820f, new Vector2(0f, -10f));
            StarterScenes.AddText(canvas, "Controls", Controls, 26, new Color(0.65f, 0.65f, 0.7f), new Vector2(0f, -505f), new Vector2(1600f, 50f));

            var pausedPanel = StarterScenes.AddPanel(canvas, "Paused Panel", "Paused", true);
            StarterScenes.AddRaiseButton(pausedPanel, "Resume", new Vector2(0f, 0f), graph, PauseEvent);
            StarterScenes.AddRaiseButton(pausedPanel, "Retry", new Vector2(0f, -100f), graph, RetryEvent);
            StarterScenes.AddRaiseButton(pausedPanel, "Quit", new Vector2(0f, -200f), graph, QuitEvent);
            var solvedPanel = StarterScenes.AddPanel(canvas, "Solved Panel", "Solved!", true);
            StarterScenes.AddParameterText(solvedPanel, "Moves", graph, MovesParameter, "in {0} moves", 44, new Vector2(0f, 70f), new Vector2(800f, 80f));
            StarterScenes.AddRaiseButton(solvedPanel, "Next", new Vector2(0f, -60f), graph, NextEvent);
            StarterScenes.AddText(solvedPanel, "Hint", "Enter: next", 26, Color.white, new Vector2(0f, -150f), new Vector2(800f, 50f));
            var allClearPanel = StarterScenes.AddPanel(canvas, "All Clear Panel", "All Clear!", true);
            StarterScenes.AddRaiseButton(allClearPanel, "Title", new Vector2(0f, -40f), graph, TitleEvent);

            StarterScenes.AddActivator(canvas.gameObject, graph, PausedNode, pausedPanel.gameObject);
            StarterScenes.AddActivator(canvas.gameObject, graph, SolvedNode, solvedPanel.gameObject);
            StarterScenes.AddActivator(canvas.gameObject, graph, AllClearNode, allClearPanel.gameObject);

            // キー: Esc でポーズ（もう一度で戻る）、Enter で次のレベルへ。All Clear はボタンだけ（Enter が Next の先から続けて Title へ進まないように）
            AddKeys(StarterScenes.CreateRoot(scene, "Keys"), graph);
            return board.gameObject;
        }

        /// <summary>Game のキー（uGUI 版と UI Toolkit 版で同じ）: Esc でポーズ（もう一度で戻る）、Enter で次のレベルへ。</summary>
        internal static void AddKeys(GameObject keys, NodeGraphAsset graph)
        {
            StarterScenes.AddKey(keys, graph, KeyCode.Escape, PauseEvent);
            StarterScenes.AddKey(keys, graph, KeyCode.Return, NextEvent);
        }
    }
}
