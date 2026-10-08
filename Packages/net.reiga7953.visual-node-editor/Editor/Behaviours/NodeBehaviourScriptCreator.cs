using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Behaviours
{
    /// <summary>
    /// Create Script…: 振る舞いの骨組みを書き出して IDE で開き、コンパイル（ドメインリロード）の後で元のノードに追加する。
    /// 追加待ちはドメインリロードを越えて残るよう <see cref="SessionState"/> に覚えておく。
    /// </summary>
    public static class NodeBehaviourScriptCreator
    {
        private const string PendingKey = "Reiga.VisualNodeEditor.PendingBehaviourScript";

        /// <summary>
        /// コンパイル後に振る舞いを追加したとき（追加先のアセットとノード ID）。開いているグラフウィンドウが表示を更新する。
        /// </summary>
        public static event Action<NodeGraphAsset, string> BehaviourAdded;

        /// <summary>
        /// 保存先を尋ねて骨組みを書き出し、IDE で開く。コンパイル後に、そのクラスを <paramref name="nodeId"/> のノードへ追加する。
        /// </summary>
        public static void CreateScript(NodeGraphAsset asset, string nodeId)
        {
            var assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Create Script", "Save the graph asset first.", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("Create Node Behaviour Script",
                NodeBehaviourScriptTemplate.DefaultClassName, "cs",
                "Choose where to save the new behaviour script. The file name becomes the class name.",
                Path.GetDirectoryName(assetPath));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Unity はファイル名とクラス名が同じでないとスクリプトとクラスを結び付けられない
            var className = NodeBehaviourScriptTemplate.ToClassName(Path.GetFileNameWithoutExtension(path));
            path = (Path.GetDirectoryName(path) ?? string.Empty).Replace('\\', '/') + "/" + className + ".cs";
            if (File.Exists(path) || IsClassNameTaken(className))
            {
                EditorUtility.DisplayDialog("Create Script", $"A script or class named '{className}' already exists.", "OK");
                return;
            }

            File.WriteAllText(path, NodeBehaviourScriptTemplate.Generate(className));
            SetPending(new PendingBehaviourScript(AssetDatabase.AssetPathToGUID(assetPath), nodeId, className));
            AssetDatabase.ImportAsset(path);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null)
            {
                AssetDatabase.OpenAsset(script);
            }
        }

        /// <summary>追加待ちのスクリプトがあるか（Create Script… の後、まだコンパイルが終わっていない）。</summary>
        public static bool HasPending => SessionState.GetString(PendingKey, string.Empty).Length > 0;

        /// <summary>
        /// 追加待ちのクラスがコンパイルされていれば、元のノードに追加する。成否にかかわらず追加待ちは消す
        /// （ドメインリロードが起きた = コンパイルは通っているので、クラスが無ければ改名などで見つからない）。
        /// </summary>
        internal static bool CompletePending()
        {
            var pending = GetPending();
            if (pending == null)
            {
                return false;
            }

            SessionState.EraseString(PendingKey);
            var asset = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(pending.AssetGuid));
            var type = FindType(pending.ClassName);
            if (NodeBehaviourEditing.Add(asset, pending.NodeId, type) == null)
            {
                Debug.LogWarning($"[VisualNodeEditor] Could not add '{pending.ClassName}' to its node. Add it from the node's inspector.");
                return false;
            }

            BehaviourAdded?.Invoke(asset, pending.NodeId);
            return true;
        }

        /// <summary>
        /// 読み込まれているどこかのアセンブリに、完全名が <paramref name="className"/> の型があるか。
        /// 骨組みは名前空間の無い public クラスなので、<see cref="NodeBehaviour"/> 以外の同名クラスとも衝突してコンパイルが通らなくなる。
        /// </summary>
        internal static bool IsClassNameTaken(string className)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(className, false) != null)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // 読めないアセンブリ（動的に作られたものなど）は飛ばす
                }
            }

            return false;
        }

        /// <summary>クラス名（名前空間を含む完全名）から振る舞いの型を探す。無ければ null。</summary>
        internal static Type FindType(string className) =>
            TypeCache.GetTypesDerivedFrom<NodeBehaviour>().FirstOrDefault(type => type.FullName == className);

        internal static void SetPending(PendingBehaviourScript pending) =>
            SessionState.SetString(PendingKey, pending == null ? string.Empty : JsonUtility.ToJson(pending));

        private static PendingBehaviourScript GetPending()
        {
            var json = SessionState.GetString(PendingKey, string.Empty);
            return json.Length == 0 ? null : JsonUtility.FromJson<PendingBehaviourScript>(json);
        }

        // コンパイル後のドメインリロードで呼ばれる。ウィンドウが作り直されるのを待ってから追加する
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (HasPending)
            {
                EditorApplication.delayCall += () => CompletePending();
            }
        }
    }
}
