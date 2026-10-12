using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Play;
using Reiga.VisualNodeEditor.Editor.Scenes;
using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string StarterName = "ADV Starter";

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
                StarterScenes.CreateScene(titlePath, scene => BuildTitle(scene, graph), StarterName);
                created.Add(titlePath);
                var storyPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Story.unity");
                StarterScenes.CreateScene(storyPath, scene => BuildStory(scene, graph, project), StarterName);
                created.Add(storyPath);

                StarterScenes.SetScene(graph, TitleSceneNode, titlePath);
                StarterScenes.SetScene(graph, StorySceneNode, storyPath);
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
                StarterScenes.ClearScene(graph, TitleSceneNode);
                StarterScenes.ClearScene(graph, StorySceneNode);
                throw;
            }
        }

        /// <summary>Title: Main Camera、EventSystem、見出しと「New Game」「Continue」のボタン、このグラフの Graph Runner。</summary>
        internal static void BuildTitle(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            StarterScenes.AddEventSystem(scene);
            var canvas = StarterScenes.AddCanvas(scene);
            var heading = StarterScenes.AddText(canvas, "Heading", graph.name, 72, Color.white);
            heading.rectTransform.sizeDelta = new Vector2(1200f, 160f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            StarterScenes.AddButton(canvas, "New Game", new Vector2(0f, -40f), graph, button =>
            {
                button.Action = GraphEventButtonAction.Raise;
                button.EventName = NewGameEvent;
            });
            StarterScenes.AddButton(canvas, "Continue", new Vector2(0f, -140f), graph, button =>
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
            StarterScenes.AddCamera(scene);
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
    }
}
