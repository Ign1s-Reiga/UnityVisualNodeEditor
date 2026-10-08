using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の振る舞い。呼ばれた順に "Tag.Enter" などを <see cref="Log"/> に書く。</summary>
    [Serializable]
    internal sealed class RecordingBehaviour : NodeBehaviour
    {
        /// <summary>全インスタンス共通の呼び出し記録（Runner ごとの複製からも書けるように static）。</summary>
        public static readonly List<string> Log = new();

        [SerializeField] private string _tag = "Recording";

        public string Tag
        {
            get => _tag;
            set => _tag = value;
        }

        public override void OnEnter() => Log.Add(_tag + ".Enter");

        public override void OnUpdate(float deltaTime) =>
            Log.Add($"{_tag}.Update({deltaTime.ToString(CultureInfo.InvariantCulture)})");

        public override void OnFixedUpdate(float fixedDeltaTime) =>
            Log.Add($"{_tag}.FixedUpdate({fixedDeltaTime.ToString(CultureInfo.InvariantCulture)})");

        public override void OnExit() => Log.Add(_tag + ".Exit");
    }
}
