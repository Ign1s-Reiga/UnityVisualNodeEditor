using System;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Play;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// Starter のシーンの組み立て（ゲームの Starter と ADV Starter が共有する）。カメラ・EventSystem・Canvas・文字・ボタン・パネル、
    /// それにゲーム向けのコンポーネント（ノードにいる間だけ表示・パラメータの表示・キー）を置く。
    /// 組み立てたシーンはすぐ保存して閉じるので、Undo には残さない。文字は uGUI の Text（TextMeshPro は Essential Resources を読み込むまで使えない）。
    /// </summary>
    internal static class StarterScenes
    {
        /// <summary>ボタンの大きさ。</summary>
        internal static readonly Vector2 ButtonSize = new Vector2(360f, 72f);

        /// <summary>
        /// <paramref name="path"/> に新しいシーンを追加で開いて <paramref name="build"/> で組み立て、保存して閉じる（今開いているシーンは切り替えない）。
        /// 保存できなければ例外（メッセージに <paramref name="starter"/> の名前を入れる）。
        /// </summary>
        internal static void CreateScene(string path, Action<Scene> build, string starter)
        {
            if (!ScenePicker.CreateSceneFile(path, NewSceneSetup.EmptyScene, build))
            {
                throw new InvalidOperationException($"{starter} could not save the scene '{path}'.");
            }
        }

        /// <summary><paramref name="graph"/> の、タイトルが <paramref name="nodeTitle"/> の Scene ノードに <paramref name="scenePath"/> のシーンを入れる。</summary>
        internal static void SetScene(NodeGraphAsset graph, string nodeTitle, string scenePath)
        {
            foreach (var node in graph.Nodes.OfType<SceneNode>().Where(node => node.Title == nodeTitle))
            {
                node.Scene = new SceneReference(AssetDatabase.AssetPathToGUID(scenePath), scenePath);
            }
        }

        /// <summary><paramref name="graph"/> の、タイトルが <paramref name="nodeTitle"/> の Scene ノードを空にする（作ったシーンを消したとき）。</summary>
        internal static void ClearScene(NodeGraphAsset graph, string nodeTitle)
        {
            foreach (var node in graph.Nodes.OfType<SceneNode>().Where(node => node.Title == nodeTitle))
            {
                node.Scene = new SceneReference();
            }
        }

        /// <summary>Main Camera（暗い単色の背景）と AudioListener。</summary>
        internal static Camera AddCamera(Scene scene)
        {
            var cameraObject = CreateRoot(scene, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        /// <summary>EventSystem。入力モジュールは uGUI の GameObject &gt; UI &gt; Event System と同じもの（プロジェクトの入力の設定に合ったもの）。</summary>
        internal static void AddEventSystem(Scene scene)
        {
            var eventSystem = CreateRoot(scene, "EventSystem");
            eventSystem.AddComponent<EventSystem>();
            InputModuleComponentFactory.AddInputModule(eventSystem);
        }

        /// <summary>画面に重ねる Canvas（1920 × 1080 を基準に拡大縮小する）。</summary>
        internal static RectTransform AddCanvas(Scene scene)
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

        /// <summary>中央揃えの文字（組み込みのフォント）。</summary>
        internal static Text AddText(RectTransform parent, string name, string content, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// <paramref name="position"/>（親の中央から）に置く文字。<paramref name="size"/> の枠に収める。
        /// </summary>
        internal static Text AddText(RectTransform parent, string name, string content, int fontSize, Color color, Vector2 position, Vector2 size)
        {
            var text = AddText(parent, name, content, fontSize, color);
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        /// <summary>
        /// ボタン（uGUI の Button と文字）と、クリックで送る Graph Event Button（このグラフ）。<paramref name="configure"/> で送るものを決める。
        /// </summary>
        internal static GameObject AddButton(RectTransform parent, string label, Vector2 position, NodeGraphAsset graph, Action<GraphEventButton> configure)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = ButtonSize;
            rect.anchoredPosition = position;
            var image = buttonObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            buttonObject.AddComponent<Button>();

            var text = AddText(rect, "Text", label, 32, new Color(0.2f, 0.2f, 0.2f));
            Stretch(text.rectTransform);

            // ボタンのクリックには、有効になったときに自分で繋ぐ（Graph Event Button）
            var sender = buttonObject.AddComponent<GraphEventButton>();
            sender.Graph = graph;
            configure(sender);
            return buttonObject;
        }

        /// <summary><paramref name="eventName"/> を Raise するボタン。</summary>
        internal static GameObject AddRaiseButton(RectTransform parent, string label, Vector2 position, NodeGraphAsset graph, string eventName) =>
            AddButton(parent, label, position, graph, button =>
            {
                button.Action = GraphEventButtonAction.Raise;
                button.EventName = eventName;
            });

        /// <summary>
        /// 画面いっぱいのパネル（<paramref name="dim"/> なら後ろを暗くし、クリックを止める）と、上のほうの見出し。非表示で作る
        /// （<see cref="AddActivator"/> でノードにいる間だけ出す。エディタでシーンを開いたときに重なって見えないように）。
        /// </summary>
        internal static RectTransform AddPanel(RectTransform canvas, string name, string heading, bool dim)
        {
            var panelObject = new GameObject(name, typeof(RectTransform));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvas, false);
            Stretch(panel);
            if (dim)
            {
                var background = panelObject.AddComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.6f);
            }

            AddText(panel, "Heading", heading, 72, Color.white, new Vector2(0f, 180f), new Vector2(1400f, 140f));
            panelObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// <paramref name="owner"/>（いつも有効な物。Canvas など）に Graph Node Activator を足し、グラフが <paramref name="nodeTitle"/> のノードにいる間だけ
        /// <paramref name="target"/> を出す。
        /// </summary>
        internal static GraphNodeActivator AddActivator(GameObject owner, NodeGraphAsset graph, string nodeTitle, GameObject target)
        {
            var node = graph.Nodes.FirstOrDefault(n => ActivatorNodeChoices.IsChoosable(n) && n.Title == nodeTitle)
                       ?? throw new InvalidOperationException($"The starter graph has no node named '{nodeTitle}'.");
            var activator = owner.AddComponent<GraphNodeActivator>();
            activator.Graph = graph;
            activator.NodeId = node.Id;
            activator.Targets.Add(target);
            return activator;
        }

        /// <summary>パラメータを書式で出す文字（Graph Parameter Text）。シーンに置く文字は、値 0 のときのもの。</summary>
        internal static Text AddParameterText(RectTransform parent, string name, NodeGraphAsset graph, string parameter, string format, int fontSize,
            Vector2 position, Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GraphParameterText.TryFormat(format, InitialValue(graph, parameter), out var initial);
            var text = AddText(parent, name, initial, fontSize, Color.white, position, size);
            text.alignment = alignment;
            var parameterText = text.gameObject.AddComponent<GraphParameterText>();
            parameterText.Graph = graph;
            parameterText.Parameter = parameter;
            parameterText.Format = format;
            return text;
        }

        /// <summary><paramref name="owner"/> に、<paramref name="key"/> で <paramref name="eventName"/> を Raise する Graph Event Key を足す。</summary>
        internal static GraphEventKey AddKey(GameObject owner, NodeGraphAsset graph, KeyCode key, string eventName)
        {
            var eventKey = owner.AddComponent<GraphEventKey>();
            eventKey.Graph = graph;
            eventKey.Key = key;
            eventKey.Action = GraphEventKeyAction.Raise;
            eventKey.EventName = eventName;
            return eventKey;
        }

        /// <summary>
        /// ゲームの Starter の Title: Main Camera、EventSystem、見出し（グラフ名）と説明、<paramref name="startEvent"/> を Raise する Start のボタン、
        /// Return / Space でも Start する Graph Event Key、このグラフの Graph Runner。
        /// </summary>
        internal static void BuildGameTitle(Scene scene, NodeGraphAsset graph, string controls, string startEvent)
        {
            AddCamera(scene);
            AddEventSystem(scene);
            var canvas = AddCanvas(scene);
            AddText(canvas, "Heading", graph.name, 96, Color.white, new Vector2(0f, 200f), new Vector2(1600f, 180f));
            AddText(canvas, "Controls", controls, 32, new Color(0.75f, 0.75f, 0.8f), new Vector2(0f, 60f), new Vector2(1600f, 80f));
            AddRaiseButton(canvas, "Start", new Vector2(0f, -100f), graph, startEvent);
            var keys = CreateRoot(scene, "Keys");
            AddKey(keys, graph, KeyCode.Return, startEvent);
            AddKey(keys, graph, KeyCode.Space, startEvent);
            PlaySetup.CreateRunner(scene, graph);
        }

        /// <summary>盤（ゲームのスクリプトを付ける、中央の正方形）。暗い背景の Image。</summary>
        internal static RectTransform AddBoard(RectTransform canvas, float size, Vector2 position)
        {
            var boardObject = new GameObject("Board", typeof(RectTransform));
            var board = (RectTransform)boardObject.transform;
            board.SetParent(canvas, false);
            board.sizeDelta = new Vector2(size, size);
            board.anchoredPosition = position;
            var background = boardObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.08f, 0.1f);
            background.raycastTarget = false;
            return board;
        }

        /// <summary>シーンの一番上に GameObject を作る。</summary>
        internal static GameObject CreateRoot(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        /// <summary>親いっぱいに広げる。</summary>
        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary><paramref name="transform"/> の、シーンの一番上からのパス（例: <c>Canvas/Board</c>）。</summary>
        internal static string GetPath(Transform transform) =>
            transform.parent == null ? transform.name : GetPath(transform.parent) + "/" + transform.name;

        // パラメータの既定値（シーンに置く文字に使う）。無ければ 0
        private static object InitialValue(NodeGraphAsset graph, string parameter) => graph.FindParameterByName(parameter)?.DefaultValue ?? 0;
    }
}
