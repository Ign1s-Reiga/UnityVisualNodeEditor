using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>グリッドへの吸着の計算。</summary>
    public static class GridSnap
    {
        /// <summary>
        /// 位置を最も近いグリッドの交点に丸める（x・y それぞれ <paramref name="spacing"/> の倍数）。
        /// 間隔が 0 以下なら変えない。
        /// </summary>
        public static Vector2 Snap(Vector2 position, float spacing)
        {
            if (spacing <= 0f)
            {
                return position;
            }

            return new Vector2(RoundHalfUp(position.x / spacing) * spacing, RoundHalfUp(position.y / spacing) * spacing);
        }

        // Mathf.Round は偶数への丸め（1.5 → 2、2.5 → 2）なので、ちょうど中間の位置で吸着の向きがばらつく。常に大きい方へ丸める
        private static float RoundHalfUp(float value) => Mathf.Floor(value + 0.5f);
    }
}
