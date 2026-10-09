using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// イベント名の打ち間違いの検出。<see cref="GraphRunner.Raise"/> は完全一致で探すので、
    /// 大文字小文字や前後の空白だけが違う名前（"StartGame" と "startGame "）は別のイベントとして扱われ、気付きにくい。
    /// </summary>
    public static class EventNameCheck
    {
        /// <summary>表記ゆれを比べるためのキー（前後の空白を除いて小文字にしたもの）。</summary>
        public static string Normalize(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>前後に空白があるか。</summary>
        public static bool HasSurroundingSpaces(string name) => !string.IsNullOrEmpty(name) && name.Trim().Length != name.Length;

        /// <summary>
        /// <paramref name="names"/> のうち、先に出てきた別の名前とキー（<see cref="Normalize"/>）が同じで綴りが違うものを、
        /// その先の名前と組にして順に返す。完全に同じ名前の繰り返しは表記ゆれではないので返さない。空の名前は無視する。
        /// </summary>
        public static List<(string Name, string LooksLike)> FindNearDuplicates(IEnumerable<string> names)
        {
            var result = new List<(string, string)>();
            var firstByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in names ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var key = Normalize(name);
                if (!firstByKey.TryGetValue(key, out var first))
                {
                    firstByKey.Add(key, name);
                }
                else if (!string.Equals(first, name, StringComparison.Ordinal))
                {
                    result.Add((name, first));
                }
            }

            return result;
        }
    }
}
