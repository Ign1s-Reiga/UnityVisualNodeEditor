using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Play;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// ADV Starter のシーン（Title と Story）を作る。ツールバーの Play だけで最初から最後まで遊べるようにする。
    /// 新しいシーンを追加で開いて組み立て、保存して閉じる（今開いているシーンは切り替えない）。
    /// <list type="bullet">
    /// <item>Title: Main Camera、EventSystem、見出しと「New Game」（Raise <c>NewGame</c>）・「Continue」（スロット <c>auto</c> から Load）のボタン、このグラフの Graph Runner</item>
    /// <item>Story: Main Camera と、Yarn Spinner の Dialogue System（Yarn プロジェクト・Yarn Graph Bridge・Graph Variable Storage を入れたもの）</item>
    /// </list>
    /// </summary>
    internal static class AdvStarterScenes
    {
        /// <summary>「Continue」とオートセーブに使うスロット。</summary>
        internal const string AutoSaveSlot = "auto";

        /// <summary>「New Game」で Raise するイベント名（ひな形のグラフと同じ）。</summary>
        internal const string NewGameEvent = "NewGame";

        /// <summary>Title のシーンが入る Scene ノードのタイトル。</summary>
        internal const string TitleSceneNode = "Title";

        /// <summary>Story のシーンが入る Scene ノードのタイトル。</summary>
        internal const string StorySceneNode = "Story";

        // DialogueRunner の Yarn Project のフィールド名（Yarn Spinner 3.x）
        internal const string YarnProjectPropertyName = "yarnProject";

        // Yarn Spinner の Dialogue System のプレハブ（GameObject > Yarn Spinner > Dialogue System が置くもの）
        private const string DialogueSystemPrefabGuid = "52571f68872914e24837210513edea1d";

        /// <summary>
        /// Story に置く Dialogue System のプレハブ（テストで差し替える。null なら Yarn Spinner のもの）。
        /// Yarn Spinner のものは TextMesh Pro を使い、Essential Resources が無いとその読み込みのウィンドウを開くので、バッチモードのテストでは使わない。
        /// </summary>
        internal static GameObject DialogueSystemPrefabOverride;

        /// <summary>Yarn Spinner の Dialogue System のプレハブ。見つからなければ null。</summary>
        internal static GameObject LoadYarnDialogueSystemPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(DialogueSystemPrefabGuid));

        /// <summary>今シーンを作れない理由（未保存の無題のシーンが開いている・Play 中）。作れるなら null。</summary>
        internal static string GetProblem() => ScenePicker.GetCreateProblem();

        /// <summary>
        /// <paramref name="folder"/> に Title と Story のシーンを作って保存し、<paramref name="graph"/>（保存済みのアセット）の
        /// 同じタイトルの Scene ノードに入れて、グラフを保存する。作れないとき（<see cref="GetProblem"/>）は呼ばないこと。
        /// どちらかを作れなければ、作ったシーンを消して例外にする（片方だけのシーンを残さない。グラフの Scene ノードは空のまま）。
        /// </summary>
        internal static void Create(NodeGraphAsset graph, string folder, YarnProject project)
        {
            var created = new List<string>();
            try
            {
                var titlePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Title.unity");
                CreateScene(titlePath, scene => BuildTitle(scene, graph));
                created.Add(titlePath);
                var storyPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Story.unity");
                CreateScene(storyPath, scene => BuildStory(scene, graph, project));
                created.Add(storyPath);

                SetScene(graph, TitleSceneNode, titlePath);
                SetScene(graph, StorySceneNode, storyPath);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
            }
            catch
            {
                foreach (var path in created)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                // グラフを保存するところで失敗したときも、消したシーンを指したまま残さない（Scene ノードは空のまま）
                ClearScene(graph, TitleSceneNode);
                ClearScene(graph, StorySceneNode);
                throw;
            }
        }

        /// <summary>Title: Main Camera、EventSystem、見出しと「New Game」「Continue」のボタン、このグラフの Graph Runner。</summary>
        internal static void BuildTitle(Scene scene, NodeGraphAsset graph)
        {
            AddCamera(scene);
            AddEventSystem(scene);
            var canvas = AddCanvas(scene);
            var heading = AddText(canvas, "Heading", graph.name, 72, Color.white);
            heading.rectTransform.sizeDelta = new Vector2(1200f, 160f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            AddButton(canvas, "New Game", new Vector2(0f, -40f), graph, button =>
            {
                button.Action = GraphEventButtonAction.Raise;
                button.EventName = NewGameEvent;
            });
            AddButton(canvas, "Continue", new Vector2(0f, -140f), graph, button =>
            {
                button.Action = GraphEventButtonAction.Load;
                button.Slot = AutoSaveSlot;
            });
            PlaySetup.CreateRunner(scene, graph);
        }

        /// <summary>
        /// Story: Main Camera と、Yarn Spinner の Dialogue System（<see cref="ConnectToGraph"/> で繋いだもの）。
        /// プレハブが見つからなければ、Camera だけにして警告を出す。
        /// </summary>
        internal static void BuildStory(Scene scene, NodeGraphAsset graph, YarnProject project)
        {
            AddCamera(scene);
            var prefab = DialogueSystemPrefabOverride != null ? DialogueSystemPrefabOverride : LoadYarnDialogueSystemPrefab();
            var system = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject : null;
            var runner = system != null ? system.GetComponent<DialogueRunner>() : null;
            if (runner == null)
            {
                Debug.LogWarning("[VisualNodeEditor] ADV Starter could not find Yarn Spinner's Dialogue System prefab, so the Story scene has no dialogue UI. " +
                                 "Add one with GameObject > Yarn Spinner > Dialogue System, then add a Yarn Graph Bridge to its Dialogue Runner.");
                return;
            }

            ConnectToGraph(runner, graph, project);
        }

        /// <summary>
        /// <paramref name="runner"/> をグラフに繋ぐ: Yarn プロジェクトを入れ、Yarn Graph Bridge（このグラフ、オートセーブ <see cref="AutoSaveSlot"/>）を足し、
        /// Variable Storage を Graph Variable Storage にする（Yarn の変数を Blackboard と共有する）。
        /// 組み立ててすぐ保存して閉じるシーンのためのものなので、Undo には残さない（閉じたシーンの物を指す Undo が残らないように）。
        /// </summary>
        internal static YarnGraphBridge ConnectToGraph(DialogueRunner runner, NodeGraphAsset graph, YarnProject project)
        {
            var serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty(YarnProjectPropertyName).objectReferenceValue = project;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();

            var bridge = runner.gameObject.AddComponent<YarnGraphBridge>();
            bridge.Graph = graph;
            bridge.AutoSaveSlot = AutoSaveSlot;
            YarnBridgeSetup.ShareVariables(runner, false);
            return bridge;
        }

        private static void CreateScene(string path, Action<Scene> build)
        {
            if (!ScenePicker.CreateSceneFile(path, NewSceneSetup.EmptyScene, build))
            {
                throw new InvalidOperationException($"ADV Starter could not save the scene '{path}'.");
            }
        }

        private static void SetScene(NodeGraphAsset graph, string nodeTitle, string scenePath)
        {
            foreach (var node in graph.Nodes.OfType<SceneNode>().Where(node => node.Title == nodeTitle))
            {
                node.Scene = new SceneReference(AssetDatabase.AssetPathToGUID(scenePath), scenePath);
            }
        }

        private static void ClearScene(NodeGraphAsset graph, string nodeTitle)
        {
            foreach (var node in graph.Nodes.OfType<SceneNode>().Where(node => node.Title == nodeTitle))
            {
                node.Scene = new SceneReference();
            }
        }

        private static void AddCamera(Scene scene)
        {
            var cameraObject = CreateRoot(scene, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
            cameraObject.AddComponent<AudioListener>();
        }

        // 入力モジュールは uGUI の GameObject > UI > Event System と同じもの（プロジェクトが Input System パッケージを使っていれば、それが差し替える）
        private static void AddEventSystem(Scene scene)
        {
            var eventSystem = CreateRoot(scene, "EventSystem");
            eventSystem.AddComponent<EventSystem>();
            InputModuleComponentFactory.AddInputModule(eventSystem);
        }

        private static RectTransform AddCanvas(Scene scene)
        {
            var canvasObject = CreateRoot(scene, "Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return (RectTransform)canvasObject.transform;
        }

        private static void AddButton(RectTransform canvas, string label, Vector2 position, NodeGraphAsset graph, Action<GraphEventButton> configure)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(canvas, false);
            rect.sizeDelta = new Vector2(360f, 72f);
            rect.anchoredPosition = position;
            var image = buttonObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            buttonObject.AddComponent<Button>();

            var text = AddText(rect, "Text", label, 32, new Color(0.2f, 0.2f, 0.2f));
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            // ボタンのクリックには、有効になったときに自分で繋ぐ（Graph Event Button）
            var sender = buttonObject.AddComponent<GraphEventButton>();
            sender.Graph = graph;
            configure(sender);
        }

        // TextMeshPro は Essential Resources を読み込むまで使えないので、uGUI の Text（組み込みのフォント）にする
        private static Text AddText(RectTransform parent, string name, string content, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            return text;
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }
    }
}
