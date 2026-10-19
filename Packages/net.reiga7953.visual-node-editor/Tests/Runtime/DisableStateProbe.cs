using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// テスト用: <c>OnDisable</c> が呼ばれたときの状態（コンポーネントが有効か・activeSelf・activeInHierarchy）を記録する。
    /// Graph Destroy Trigger がそこから「倒した」かを決める前提（Unity は壊すとき有効なまま、隠すときは無効にしてから呼ぶ）を確かめる。
    /// エディタでも呼ばれるように <c>[ExecuteAlways]</c>。Editor だけのアセンブリのコンポーネントは GameObject に付けられないので、ここ（EditMode テストが参照する）に置く。
    /// </summary>
    [ExecuteAlways]
    public sealed class DisableStateProbe : MonoBehaviour
    {
        /// <summary>呼ばれた順の状態。</summary>
        public static readonly List<(bool Enabled, bool ActiveSelf, bool ActiveInHierarchy)> Seen =
            new List<(bool Enabled, bool ActiveSelf, bool ActiveInHierarchy)>();

        private void OnDisable() => Seen.Add((enabled, gameObject.activeSelf, gameObject.activeInHierarchy));
    }
}
