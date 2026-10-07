using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// ノードの整列・等間隔配置の計算。入力の矩形と同じ順で、新しい左上の位置を返す（大きさは変えない）。
    /// 描画に依存しない純粋な関数。
    /// </summary>
    public static class NodeAlignment
    {
        /// <summary><paramref name="rects"/> を <paramref name="mode"/> に従って揃えた位置。</summary>
        public static Vector2[] Align(IReadOnlyList<Rect> rects, AlignMode mode)
        {
            if (rects.Count == 0)
            {
                return new Vector2[0];
            }

            var left = rects.Min(r => r.xMin);
            var right = rects.Max(r => r.xMax);
            var top = rects.Min(r => r.yMin);
            var bottom = rects.Max(r => r.yMax);
            var centerX = (left + right) / 2f;
            var centerY = (top + bottom) / 2f;

            return rects.Select(r => mode switch
            {
                AlignMode.Left => new Vector2(left, r.y),
                AlignMode.Right => new Vector2(right - r.width, r.y),
                AlignMode.Top => new Vector2(r.x, top),
                AlignMode.Bottom => new Vector2(r.x, bottom - r.height),
                AlignMode.CenterHorizontally => new Vector2(centerX - r.width / 2f, r.y),
                _ => new Vector2(r.x, centerY - r.height / 2f),
            }).ToArray();
        }

        /// <summary>
        /// 両端のノードを動かさずに、隣り合うノードの間隔が等しくなる位置。並び順は今の位置（左上）で決める。
        /// 3 つ未満なら位置を変えない。
        /// </summary>
        public static Vector2[] Distribute(IReadOnlyList<Rect> rects, DistributeAxis axis)
        {
            var result = rects.Select(r => r.position).ToArray();
            if (rects.Count < 3)
            {
                return result;
            }

            var horizontal = axis == DistributeAxis.Horizontal;
            var order = Enumerable.Range(0, rects.Count)
                .OrderBy(i => horizontal ? rects[i].xMin : rects[i].yMin)
                .ToList();

            float Start(Rect r) => horizontal ? r.xMin : r.yMin;
            float Size(Rect r) => horizontal ? r.width : r.height;

            var first = rects[order[0]];
            var last = rects[order[order.Count - 1]];
            var span = Start(last) + Size(last) - Start(first);
            var gap = (span - order.Sum(i => Size(rects[i]))) / (order.Count - 1);

            var cursor = Start(first) + Size(first) + gap;
            for (var k = 1; k < order.Count - 1; k++)
            {
                var index = order[k];
                result[index] = horizontal
                    ? new Vector2(cursor, rects[index].y)
                    : new Vector2(rects[index].x, cursor);
                cursor += Size(rects[index]) + gap;
            }

            return result;
        }
    }
}
