using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Play
{
    /// <summary>
    /// シーンにあるプレハブのインスタンスが参照するもの（<see cref="RunnerUsage.GetPrefabReferences"/> が集める）。
    /// プレハブの中の Graph Runner はシーンファイルに書き出されないので、これを手がかりに探す。
    /// </summary>
    public sealed class PrefabReferences
    {
        /// <summary>インスタンスの元になったプレハブの GUID。</summary>
        public HashSet<string> SourcePrefabGuids { get; } = new(StringComparer.Ordinal);

        /// <summary>インスタンスで上書きしたアセット参照（グラフなど）の GUID。</summary>
        public HashSet<string> OverriddenGuids { get; } = new(StringComparer.Ordinal);
    }
}
