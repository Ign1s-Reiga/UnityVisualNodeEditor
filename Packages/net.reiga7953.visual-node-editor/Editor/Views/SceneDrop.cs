using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>キャンバスへドロップしたシーンアセットから Scene ノードを作るときの、シーンの取り出しと並べ方。</summary>
    public static class SceneDrop
    {
        /// <summary>並べて作る Scene ノードどうしの横の間隔。</summary>
        public const float Spacing = 260f;

        private const string SceneExtension = ".unity";

        /// <summary>
        /// アセットのパスからシーン（<c>.unity</c>）だけを、重複を除いて渡された順に <see cref="SceneReference"/> にする。
        /// GUID は <paramref name="pathToGuid"/> で引く。
        /// </summary>
        public static List<SceneReference> FromPaths(IEnumerable<string> paths, Func<string, string> pathToGuid)
        {
            var scenes = new List<SceneReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in paths ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(path) || !path.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase) || !seen.Add(path))
                {
                    continue;
                }

                scenes.Add(new SceneReference(pathToGuid?.Invoke(path) ?? string.Empty, path));
            }

            return scenes;
        }

        /// <summary><paramref name="index"/> 番目に作る Scene ノードの位置（落とした位置から右へ並べる）。</summary>
        public static Vector2 GetPosition(Vector2 dropPosition, int index) => dropPosition + new Vector2(Spacing * index, 0f);
    }
}
