using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/>（Single）でシーンを読み込む。
    /// シーンは Build Settings に入っている必要がある。すでにアクティブなシーンと同じなら読み込まない。
    /// </summary>
    public sealed class SceneManagerSceneLoader : ISceneLoader
    {
        /// <inheritdoc />
        public void LoadScene(SceneReference scene)
        {
            var key = GetLoadKey(scene);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var active = SceneManager.GetActiveScene();
            if (IsSameScene(scene, active.path, active.name))
            {
                return;
            }

            SceneManager.LoadSceneAsync(key, LoadSceneMode.Single);
        }

        /// <summary><see cref="SceneManager"/> に渡す文字列。Path があれば Path（同名シーンを区別できる）、無ければ Name。</summary>
        public static string GetLoadKey(SceneReference scene)
        {
            if (scene == null || scene.IsEmpty)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(scene.Path) ? scene.Name : scene.Path;
        }

        /// <summary>
        /// 参照がアクティブなシーンと同じか。Play ボタンを押したシーンが最初の Scene ノードと同じ場合に二重に読み込まないために使う。
        /// </summary>
        public static bool IsSameScene(SceneReference scene, string activeScenePath, string activeSceneName)
        {
            if (scene == null || scene.IsEmpty)
            {
                return false;
            }

            return string.IsNullOrEmpty(scene.Path)
                ? scene.Name == activeSceneName
                : scene.Path == activeScenePath;
        }
    }
}
