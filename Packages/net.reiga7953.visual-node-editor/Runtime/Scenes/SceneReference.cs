using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// シーンへの参照。Runtime は文字列だけを持ち、エディタの SceneAsset との対応付けは
    /// Editor 側の PropertyDrawer が GUID を介して行う（シーンの移動・改名にも追従する）。
    /// </summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField] private string _guid;
        [SerializeField] private string _path;

        public SceneReference()
        {
        }

        public SceneReference(string guid, string path)
        {
            _guid = guid;
            _path = path;
        }

        /// <summary>シーンアセットの GUID。</summary>
        public string Guid => _guid;

        /// <summary>プロジェクト相対のシーンパス（例: "Assets/Scenes/Title.unity"）。</summary>
        public string Path => _path;

        /// <summary>シーン名（拡張子なしのファイル名）。<c>SceneManager.LoadScene</c> に渡せる。</summary>
        public string Name => string.IsNullOrEmpty(_path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(_path);

        /// <summary>シーンが設定されていないか。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_guid) && string.IsNullOrEmpty(_path);

        /// <summary>
        /// <paramref name="other"/> と同じシーンを指すか。どちらにも GUID があれば GUID で、無ければパスで比べる（改名・移動しても GUID は変わらない）。
        /// どちらかが空なら false。
        /// </summary>
        public bool IsSameSceneAs(SceneReference other)
        {
            if (other == null || IsEmpty || other.IsEmpty)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_guid) && !string.IsNullOrEmpty(other._guid))
            {
                return _guid == other._guid;
            }

            return !string.IsNullOrEmpty(_path) && _path == other._path;
        }
    }
}
