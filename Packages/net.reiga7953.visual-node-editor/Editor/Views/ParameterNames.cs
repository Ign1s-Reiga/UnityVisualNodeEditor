using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>Blackboard のパラメータ名の決め方（重複しない名前の生成と、改名の可否）。</summary>
    public static class ParameterNames
    {
        /// <summary>
        /// <paramref name="existingNames"/> と重複しない名前を返す。<paramref name="baseName"/> が空いていればそのまま、
        /// 埋まっていれば末尾に 1, 2, … を付ける（例: "Bool", "Bool1", "Bool2"）。
        /// </summary>
        public static string MakeUnique(IEnumerable<string> existingNames, string baseName)
        {
            var taken = new HashSet<string>(existingNames.Where(n => n != null), StringComparer.Ordinal);
            if (!taken.Contains(baseName))
            {
                return baseName;
            }

            for (var i = 1; ; i++)
            {
                var candidate = baseName + i;
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// 改名後の名前として使えるか。空白だけの名前と、ほかのパラメータ（<paramref name="otherNames"/>）と同じ名前は使えない。
        /// 前後の空白は取り除いてから判定する。
        /// </summary>
        public static bool IsValidName(IEnumerable<string> otherNames, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            return !otherNames.Any(n => string.Equals(n, trimmed, StringComparison.Ordinal));
        }
    }
}
