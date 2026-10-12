using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// キー入力の窓口。旧 Input Manager でも Input System でも同じ書き方で読める（<see cref="GraphEventKey"/> と、Starter が作るゲームのスクリプトが使う）。
    /// <para>
    /// <see cref="KeyDownSource"/> があればそれを使う（Input System 連携のアセンブリが、Player 設定で Input System が有効なときに入れる）。
    /// 無ければ、旧 Input Manager が有効なら <c>Input.GetKeyDown</c>。どちらも無ければ false を返し、1 度だけ警告を出す。
    /// </para>
    /// </summary>
    public static class GraphInput
    {
        private static bool _warnedNoInput;

        /// <summary>
        /// キーがこのフレームに押されたかを返す関数。null なら旧 Input Manager を使う。
        /// Input System 連携（<c>Reiga.VisualNodeEditor.InputSystem</c>）が入れる。テストで差し替えてもよい。
        /// </summary>
        public static Func<KeyCode, bool> KeyDownSource { get; set; }

        /// <summary><paramref name="key"/> がこのフレームに押されたか（<c>Input.GetKeyDown</c> と同じ）。<see cref="KeyCode.None"/> は常に false。</summary>
        public static bool GetKeyDown(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return false;
            }

            var source = KeyDownSource;
            if (source != null)
            {
                return source(key);
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            WarnNoInput();
            return false;
#endif
        }

        private static void WarnNoInput()
        {
            if (_warnedNoInput)
            {
                return;
            }

            _warnedNoInput = true;
            Debug.LogWarning("[VisualNodeEditor] Keys cannot be read: the old Input Manager is off and the Input System package is not installed. " +
                             "Install the Input System package, or set Project Settings > Player > Active Input Handling to include the Input Manager.");
        }

        // ドメインリロードを切っていても、前の Play で警告した印が残らないように（KeyDownSource は連携が入れ直すので触らない）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarning() => _warnedNoInput = false;
    }
}
