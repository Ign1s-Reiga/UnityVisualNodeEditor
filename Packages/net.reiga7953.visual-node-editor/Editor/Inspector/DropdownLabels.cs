using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// ドロップダウンの表示を重ならないようにする。<c>DropdownField</c> は表示の文字から選んだ位置を決めるので、
    /// 同じ文字が 2 つあると、後ろのものを選んでも最初のものになる（同じタイトルのノード・名前の無いパラメータなど）。
    /// </summary>
    public static class DropdownLabels
    {
        /// <summary>
        /// 2 つ目以降の同じ表示に番号を付けた一覧（例: <c>Playing</c>, <c>Playing (2)</c>）。順と数は変えない。付けた番号の表示が既にあれば、その次の番号にする。
        /// </summary>
        public static List<string> MakeUnique(IEnumerable<string> labels)
        {
            var source = new List<string>(labels);
            var used = new HashSet<string>(source, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(source.Count);
            foreach (var label in source)
            {
                if (seen.Add(label))
                {
                    result.Add(label);
                    continue;
                }

                var number = 2;
                var candidate = $"{label} ({number})";
                while (used.Contains(candidate))
                {
                    candidate = $"{label} ({++number})";
                }

                used.Add(candidate);
                seen.Add(candidate);
                result.Add(candidate);
            }

            return result;
        }
    }
}
