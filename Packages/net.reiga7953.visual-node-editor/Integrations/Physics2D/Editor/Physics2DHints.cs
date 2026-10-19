using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Physics2D.Editor
{
    /// <summary>Graph Trigger 2D・Graph Spawn Point 2D のインスペクタに出す注意（次に何をすればよいか）を決める。</summary>
    public static class Physics2DHints
    {
        /// <summary>Graph Trigger 2D の、物理まわりの注意（パラメータ・イベントの注意は Graph Destroy Trigger と共通）。問題が無ければ空。</summary>
        /// <param name="hasCollider">触れたことが届く Collider 2D があるか（同じ GameObject。Rigidbody 2D があれば子も）。</param>
        /// <param name="tag">反応するタグ。</param>
        public static List<string> ForTrigger(bool hasCollider, string tag)
        {
            var hints = new List<string>();
            if (!hasCollider)
            {
                hints.Add("Add a Collider 2D to this GameObject (turn on Is Trigger for things the player passes through, such as goals and coins).");
            }

            if (string.IsNullOrEmpty(tag))
            {
                hints.Add("No tag: anything that touches it sets it off. Choose the player's tag to react only to the player.");
            }

            return hints;
        }

        /// <summary>Graph Spawn Point 2D の注意。問題が無ければ空。</summary>
        /// <param name="parameterFound">グラフにその名前のパラメータがあるか（グラフ未設定なら無視）。</param>
        /// <param name="parameterType">そのパラメータの型（見つかったときだけ使う）。</param>
        /// <param name="hasGraph">グラフが入っているか。</param>
        /// <param name="parameter">名前のパラメータ。</param>
        /// <param name="playerTag">プレイヤーのタグ。</param>
        public static List<string> ForSpawnPoint(bool hasGraph, bool parameterFound, GraphParameterType parameterType, string parameter, string playerTag)
        {
            var hints = new List<string>();
            if (string.IsNullOrEmpty(parameter))
            {
                hints.Add("Choose the String parameter that holds the name of the point to spawn at.");
            }
            else if (hasGraph && !parameterFound)
            {
                hints.Add($"Add a String parameter '{parameter}' to the graph's Blackboard. Set it with Set Parameter or Graph Trigger 2D (checkpoints, doors).");
            }
            else if (hasGraph && parameterType != GraphParameterType.String)
            {
                hints.Add($"'{parameter}' is {parameterType}. Use a String parameter that holds the point's name.");
            }

            if (string.IsNullOrEmpty(playerTag))
            {
                hints.Add("Choose the player's tag.");
            }

            return hints;
        }
    }
}
