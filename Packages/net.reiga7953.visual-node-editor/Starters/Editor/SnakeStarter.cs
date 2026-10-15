using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// スネークゲームのひな形（<c>Assets &gt; Create &gt; Visual Node Editor &gt; Snake Starter</c>）。
    /// <code>
    /// Entry → [Scene: Title] ─(Start)→ [Scene: Game] ─(自動)→ [Container: Round] ─(GameOver)→ [State: Game Over] ─(Retry)→ [Container: Round]
    ///                                                                            └(Quit)→ [Scene: Title]           └(Title)→ [Scene: Title]
    /// Round の中: Entry → [State: Ready]（Score = 0）─(Go)→ [State: Playing] ─(Pause)→ [State: Paused]（Time Scale 0）─(Pause)→ [State: Playing]
    ///                                                        └(Crash)→ Exit (GameOver)              └(Quit)→ Exit (Quit)
    /// </code>
    /// ゲームのスクリプト（ひな形 <c>SnakeGameTemplate</c>）は、Playing の間だけ蛇を動かし、Score を増やし、ぶつかったら Crash を Raise する。
    /// </summary>
    internal sealed class SnakeStarter : GameStarter
    {
        /// <summary>1 つだけ使う。</summary>
        internal static readonly SnakeStarter Instance = new SnakeStarter();

        internal const string RoundContainer = "Round";
        internal const string ReadyNode = "Ready";
        internal const string PlayingNode = "Playing";
        internal const string PausedNode = "Paused";
        internal const string GameOverNode = "Game Over";
        internal const string GoEvent = "Go";
        internal const string PauseEvent = "Pause";
        internal const string CrashEvent = "Crash";
        internal const string RetryEvent = "Retry";
        internal const string TitleEvent = "Title";
        internal const string QuitEvent = "Quit";
        internal const string GameOverExit = "GameOver";
        internal const string QuitExit = "Quit";
        internal const string ScoreParameter = "Score";
        internal const string HighScoreParameter = "HighScore";
        internal const string SpeedParameter = "Speed";

        internal override string StarterName => "Snake Starter";

        internal override string BaseName => "Snake";

        internal override string TemplateClass => "SnakeGameTemplate";

        internal override string Controls => "Arrow keys / WASD: move    Space: start a round    Esc: pause";

        [MenuItem("Assets/Create/Visual Node Editor/Snake Starter", priority = 82)]
        private static void CreateFromMenuItem() => Instance.CreateFromMenu();

        internal override NodeGraphAsset BuildGraph()
        {
            var builder = new StarterGraphBuilder();
            var score = AddParameters(builder);

            // ルート: Title → Game（シーン）→ Round → Game Over → Retry / Title
            var title = builder.Add(new SceneNode { Title = TitleSceneNode }, 250f, 0f);
            var game = builder.Add(new SceneNode { Title = GameSceneNode }, 750f, 0f);
            builder.Connect(builder.Entry, title);
            builder.Connect(title, builder.AddEvent(StartEvent, 500f, 0f), game);
            var round = AddRound(builder, score, title, 1250f);
            builder.Connect(game, builder.AddEvent(string.Empty, 1000f, 0f, automatic: true), round);
            return builder.Graph;
        }

        /// <summary>パラメータ（Score・HighScore・Speed）を足し、Score を返す（uGUI 版と UI Toolkit 版で同じ）。</summary>
        internal static GraphParameter AddParameters(StarterGraphBuilder builder)
        {
            var score = builder.AddParameter(new GraphParameter(ScoreParameter, GraphParameterType.Int));
            builder.AddParameter(new GraphParameter(HighScoreParameter, GraphParameterType.Int));
            builder.AddParameter(new GraphParameter(SpeedParameter, GraphParameterType.Float) { FloatValue = 8f });
            return score;
        }

        /// <summary>
        /// ルートの <paramref name="x"/> から右へ、Round（中身ごと）と Game Over（Retry で Round、Title で <paramref name="title"/>）を足し、Round を返す。
        /// Round の Quit も <paramref name="title"/> へ戻る（uGUI 版と UI Toolkit 版で同じ）。
        /// </summary>
        internal static ContainerNode AddRound(StarterGraphBuilder builder, GraphParameter score, NodeData title, float x)
        {
            var round = builder.AddContainer(RoundContainer, x, 0f, null, GameOverExit, QuitExit);
            var gameOver = builder.Add(new StateNode { Title = GameOverNode, Description = "Shows the score, Retry and Title." }, x + 250f, 0f);
            builder.ConnectExit(round, GameOverExit, gameOver);
            builder.ConnectExit(round, QuitExit, title);
            builder.Connect(gameOver, builder.AddEvent(RetryEvent, x + 500f, -100f), round);
            builder.Connect(gameOver, builder.AddEvent(TitleEvent, x + 500f, 100f), title);

            // Round の中: Ready（Space で始める）→ Playing ⇄ Paused、ぶつかったら Game Over へ
            var ready = builder.Add(new StateNode { Title = ReadyNode, Description = "A new round: the snake goes back to the start. Press Space." }, 250f, 0f, round);
            builder.SetOnEnter(ready, new ParameterChange(score.Id) { IntValue = 0 });
            var playing = builder.Add(new StateNode { Title = PlayingNode, Description = "The game script moves the snake only here." }, 750f, 0f, round);
            var paused = builder.Add(new StateNode { Title = PausedNode }, 1250f, -150f, round);
            builder.TimeScaleWhileOn(paused, 0f);
            builder.Connect(builder.EntryOf(round), ready);
            builder.Connect(ready, builder.AddEvent(GoEvent, 500f, 0f, round), playing);
            builder.Connect(playing, builder.AddEvent(PauseEvent, 1000f, -150f, round), paused);
            builder.Connect(paused, builder.AddEvent(PauseEvent, 1000f, -300f, round), playing);
            builder.Connect(paused, builder.AddEvent(QuitEvent, 1500f, -150f, round), builder.AddExit(round, QuitExit, 1750f, -150f));
            builder.Connect(playing, builder.AddEvent(CrashEvent, 1000f, 150f, round), builder.AddExit(round, GameOverExit, 1250f, 150f));
            return round;
        }

        internal override GameObject BuildGame(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            StarterScenes.AddEventSystem(scene);
            var canvas = StarterScenes.AddCanvas(scene);
            var white = Color.white;

            StarterScenes.AddParameterText(canvas, "Score", graph, ScoreParameter, "Score: {0}", 44,
                new Vector2(-720f, 480f), new Vector2(400f, 80f), TextAnchor.MiddleLeft);
            StarterScenes.AddParameterText(canvas, "Best", graph, HighScoreParameter, "Best: {0}", 44,
                new Vector2(720f, 480f), new Vector2(400f, 80f), TextAnchor.MiddleRight);
            var board = StarterScenes.AddBoard(canvas, 880f, new Vector2(0f, -20f));
            StarterScenes.AddText(canvas, "Controls", Controls, 26, new Color(0.65f, 0.65f, 0.7f), new Vector2(0f, -505f), new Vector2(1600f, 50f));

            // Ready: 盤の上に案内だけ（盤は見えるまま）。Paused・Game Over: 暗くしてボタン
            var readyPanel = StarterScenes.AddPanel(canvas, "Ready Panel", "Press Space to start", false);
            var pausedPanel = StarterScenes.AddPanel(canvas, "Paused Panel", "Paused", true);
            StarterScenes.AddRaiseButton(pausedPanel, "Resume", new Vector2(0f, 0f), graph, PauseEvent);
            StarterScenes.AddRaiseButton(pausedPanel, "Quit", new Vector2(0f, -100f), graph, QuitEvent);
            var gameOverPanel = StarterScenes.AddPanel(canvas, "Game Over Panel", "Game Over", true);
            StarterScenes.AddParameterText(gameOverPanel, "Final Score", graph, ScoreParameter, "Score: {0}", 48, new Vector2(0f, 70f), new Vector2(800f, 80f));
            StarterScenes.AddRaiseButton(gameOverPanel, "Retry", new Vector2(0f, -40f), graph, RetryEvent);
            StarterScenes.AddRaiseButton(gameOverPanel, "Title", new Vector2(0f, -140f), graph, TitleEvent);
            StarterScenes.AddText(gameOverPanel, "Hint", "Enter: retry", 26, white, new Vector2(0f, -230f), new Vector2(800f, 50f));

            // パネルはノードにいる間だけ出す（Canvas はいつも有効なので、そこに付ける）
            StarterScenes.AddActivator(canvas.gameObject, graph, ReadyNode, readyPanel.gameObject);
            StarterScenes.AddActivator(canvas.gameObject, graph, PausedNode, pausedPanel.gameObject);
            StarterScenes.AddActivator(canvas.gameObject, graph, GameOverNode, gameOverPanel.gameObject);

            // キー: Space で始める、Esc でポーズ（もう一度で戻る）、Enter でやり直す。画面ごとに別のキーにして、1 回の押下で 2 つ進まないようにする
            AddKeys(StarterScenes.CreateRoot(scene, "Keys"), graph);
            return board.gameObject;
        }

        /// <summary>Game のキー（uGUI 版と UI Toolkit 版で同じ）: Space で始める、Esc でポーズ（もう一度で戻る）、Enter でやり直す。</summary>
        internal static void AddKeys(GameObject keys, NodeGraphAsset graph)
        {
            StarterScenes.AddKey(keys, graph, KeyCode.Space, GoEvent);
            StarterScenes.AddKey(keys, graph, KeyCode.Escape, PauseEvent);
            StarterScenes.AddKey(keys, graph, KeyCode.Return, RetryEvent);
        }
    }
}
