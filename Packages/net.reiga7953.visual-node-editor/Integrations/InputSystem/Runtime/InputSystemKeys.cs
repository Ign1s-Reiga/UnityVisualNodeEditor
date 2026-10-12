using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reiga.VisualNodeEditor.InputSystem
{
    /// <summary>
    /// <see cref="KeyCode"/> を Input System のキーボードで読む。Player 設定で Input System が有効なら、
    /// Play の始めに <see cref="GraphInput.KeyDownSource"/> に入れる（Graph Event Key と、Starter が作るゲームのスクリプトがそれで読む）。
    /// <see cref="KeyCode"/> と Input System の <see cref="Key"/> は名前で対応させ、名前の違うものは表で持つ。マウス・ジョイスティックは扱わない。
    /// </summary>
    public static class InputSystemKeys
    {
        // 名前の違うもの（大文字小文字だけの違い、BackQuote → Backquote・Numlock → NumLock などは、名前で探すときに吸収する）
        private static readonly Dictionary<KeyCode, Key> Renamed = new Dictionary<KeyCode, Key>
        {
            { KeyCode.Return, Key.Enter },
            { KeyCode.Alpha0, Key.Digit0 },
            { KeyCode.Alpha1, Key.Digit1 },
            { KeyCode.Alpha2, Key.Digit2 },
            { KeyCode.Alpha3, Key.Digit3 },
            { KeyCode.Alpha4, Key.Digit4 },
            { KeyCode.Alpha5, Key.Digit5 },
            { KeyCode.Alpha6, Key.Digit6 },
            { KeyCode.Alpha7, Key.Digit7 },
            { KeyCode.Alpha8, Key.Digit8 },
            { KeyCode.Alpha9, Key.Digit9 },
            { KeyCode.Keypad0, Key.Numpad0 },
            { KeyCode.Keypad1, Key.Numpad1 },
            { KeyCode.Keypad2, Key.Numpad2 },
            { KeyCode.Keypad3, Key.Numpad3 },
            { KeyCode.Keypad4, Key.Numpad4 },
            { KeyCode.Keypad5, Key.Numpad5 },
            { KeyCode.Keypad6, Key.Numpad6 },
            { KeyCode.Keypad7, Key.Numpad7 },
            { KeyCode.Keypad8, Key.Numpad8 },
            { KeyCode.Keypad9, Key.Numpad9 },
            { KeyCode.KeypadPeriod, Key.NumpadPeriod },
            { KeyCode.KeypadDivide, Key.NumpadDivide },
            { KeyCode.KeypadMultiply, Key.NumpadMultiply },
            { KeyCode.KeypadMinus, Key.NumpadMinus },
            { KeyCode.KeypadPlus, Key.NumpadPlus },
            { KeyCode.KeypadEnter, Key.NumpadEnter },
            { KeyCode.KeypadEquals, Key.NumpadEquals },
            { KeyCode.LeftControl, Key.LeftCtrl },
            { KeyCode.RightControl, Key.RightCtrl },
            { KeyCode.Print, Key.PrintScreen },
            { KeyCode.Menu, Key.ContextMenu },
        };

        /// <summary>
        /// <paramref name="keyCode"/> に当たる Input System のキー。キーボードのキーでない（マウス・ジョイスティックなど）・当たるものが無ければ false。
        /// </summary>
        public static bool TryGetKey(KeyCode keyCode, out Key key)
        {
            if (Renamed.TryGetValue(keyCode, out key))
            {
                return true;
            }

            // マウス・ジョイスティックは Key に同じ名前が無いので、ここで外れる
            if (keyCode != KeyCode.None && Enum.TryParse(keyCode.ToString(), true, out key) && key != Key.None && Enum.IsDefined(typeof(Key), key))
            {
                return true;
            }

            key = Key.None;
            return false;
        }

        /// <summary><paramref name="keyCode"/> がこのフレームに押されたか（Input System のキーボードで読む）。キーボードが無い・対応しないキーなら false。</summary>
        public static bool WasPressedThisFrame(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && TryGetKey(keyCode, out var key) && keyboard[key].wasPressedThisFrame;
        }

        // Player 設定で Input System が有効なら、Graph Event Key などのキーを Input System で読む（両方有効でも Input System）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
#if ENABLE_INPUT_SYSTEM
            GraphInput.KeyDownSource = WasPressedThisFrame;
#endif
        }
    }
}
