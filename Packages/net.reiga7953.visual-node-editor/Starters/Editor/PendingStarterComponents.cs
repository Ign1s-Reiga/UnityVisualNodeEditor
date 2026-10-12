using System;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// Starter が書き出したスクリプトを、コンパイルの後でシーンの盤に付ける。作ったばかりのスクリプトはコンパイルが終わるまで付けられないので、
    /// 予定を <see cref="SessionState"/> に覚えておき、ドメインリロード（<see cref="DidReloadScriptsAttribute"/>）の後で付ける。
    /// クラスがまだ無ければ（コンパイルエラー中など）次のドメインリロードで試し直す。Play 中なら Play を終えてから付ける。
    /// </summary>
    internal static class PendingStarterComponents
    {
        private const string PendingKey = "Reiga.VisualNodeEditor.PendingStarterComponents";
        private const string GraphPropertyName = "_graph";

        /// <summary>付ける予定を足す。</summary>
        internal static void Add(PendingStarterComponent pending)
        {
            var list = Load();
            list.Items.Add(pending);
            Save(list);
        }

        /// <summary>付ける予定の数。</summary>
        internal static int Count => Load().Items.Count;

        /// <summary>付ける予定をすべて忘れる（テスト用）。</summary>
        internal static void Clear() => SessionState.EraseString(PendingKey);

        /// <summary>
        /// 付けられるものを付け、付けた・付けられないと分かったものを予定から外す。クラスがまだ無いもの・今はシーンを開けないものは残す。付けた数を返す。
        /// </summary>
        internal static int CompleteAll()
        {
            var list = Load();
            if (list.Items.Count == 0)
            {
                return 0;
            }

            var attached = 0;
            var problem = ScenePicker.GetCreateProblem();
            foreach (var pending in list.Items.ToList())
            {
                var type = FindComponentType(pending.ClassName);
                if (type == null || problem != null)
                {
                    // まだコンパイルされていない（エラー中など）・今はシーンを開けない。次のドメインリロードで試し直す
                    Debug.LogWarning($"[VisualNodeEditor] '{pending.ClassName}' will be added to '{pending.ObjectPath}' in '{pending.ScenePath}' " +
                                     (type == null ? "once the scripts compile. " : $"later ({problem}). ") +
                                     $"If you restart Unity first, add it yourself: select '{pending.ObjectPath}', Add Component > {pending.ClassName}, and set its Graph.");
                    continue;
                }

                list.Items.Remove(pending);
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(pending.GraphGuid));
                var failure = Attach(pending.ScenePath, pending.ObjectPath, type, graph);
                if (failure == null)
                {
                    attached++;
                }
                else
                {
                    Debug.LogWarning($"[VisualNodeEditor] Could not add '{pending.ClassName}' to '{pending.ObjectPath}' in '{pending.ScenePath}': {failure}. " +
                                     $"Add it yourself with Add Component > {pending.ClassName}, and set its Graph.");
                }
            }

            Save(list);
            return attached;
        }

        /// <summary>
        /// <paramref name="scenePath"/> のシーンの <paramref name="objectPath"/> に <paramref name="type"/> を付け（既にあればそれを使う）、
        /// <c>_graph</c> があれば <paramref name="graph"/> を入れる。シーンが開いていればそこに付けて未保存にし、開いていなければ追加で開いて保存して閉じる。
        /// できなければ理由を返す。できたら null。
        /// </summary>
        internal static string Attach(string scenePath, string objectPath, Type type, NodeGraphAsset graph)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return "the scene is gone";
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                var target = Find(scene, objectPath);
                if (target == null)
                {
                    return $"there is no '{objectPath}' in the scene";
                }

                // GetComponent はエディタで「無い」ことを表す偽の null を返すことがあるので、?? ではなく == で確かめる
                var component = target.GetComponent(type);
                if (component == null)
                {
                    component = target.AddComponent(type);
                }

                var serializedComponent = new SerializedObject(component);
                var graphProperty = serializedComponent.FindProperty(GraphPropertyName);
                if (graphProperty != null && graphProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    graphProperty.objectReferenceValue = graph;
                    serializedComponent.ApplyModifiedPropertiesWithoutUndo();
                }

                if (opened)
                {
                    EditorSceneManager.SaveScene(scene);
                }
                else
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                return null;
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary><paramref name="scene"/> の、一番上からのパス（例: <c>Canvas/Board</c>）の GameObject。無ければ null。</summary>
        internal static GameObject Find(Scene scene, string objectPath)
        {
            var parts = objectPath.Split(new[] { '/' }, 2);
            var root = scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == parts[0]);
            if (root == null || parts.Length == 1)
            {
                return root;
            }

            var child = root.transform.Find(parts[1]);
            return child != null ? child.gameObject : null;
        }

        /// <summary>完全名が <paramref name="className"/> のコンポーネント（MonoBehaviour）の型。無ければ null。</summary>
        internal static Type FindComponentType(string className) =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(type => type.FullName == className && !type.IsAbstract);

        private static PendingStarterComponentList Load()
        {
            var json = SessionState.GetString(PendingKey, string.Empty);
            return json.Length == 0 ? new PendingStarterComponentList() : JsonUtility.FromJson<PendingStarterComponentList>(json) ?? new PendingStarterComponentList();
        }

        private static void Save(PendingStarterComponentList list)
        {
            if (list.Items.Count == 0)
            {
                SessionState.EraseString(PendingKey);
            }
            else
            {
                SessionState.SetString(PendingKey, JsonUtility.ToJson(list));
            }
        }

        // コンパイル後のドメインリロードで呼ばれる。Play 中なら終えてから、そうでなければエディタが落ち着いてから付ける
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (Count == 0)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += CompleteAfterPlay;
                return;
            }

            EditorApplication.delayCall += () => CompleteAll();
        }

        private static void CompleteAfterPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= CompleteAfterPlay;
            CompleteAll();
        }
    }
}
