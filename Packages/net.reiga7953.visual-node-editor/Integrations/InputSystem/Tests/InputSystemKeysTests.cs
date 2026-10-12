using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reiga.VisualNodeEditor.InputSystem.Tests
{
    /// <summary>KeyCode から Input System のキーへの対応（<see cref="InputSystemKeys"/>）。</summary>
    public sealed class InputSystemKeysTests
    {
        [TestCase(KeyCode.A, Key.A)]
        [TestCase(KeyCode.Space, Key.Space)]
        [TestCase(KeyCode.Escape, Key.Escape)]
        [TestCase(KeyCode.UpArrow, Key.UpArrow)]
        [TestCase(KeyCode.F5, Key.F5)]
        [TestCase(KeyCode.LeftShift, Key.LeftShift)]
        [TestCase(KeyCode.Return, Key.Enter)]
        [TestCase(KeyCode.Alpha1, Key.Digit1)]
        [TestCase(KeyCode.Keypad5, Key.Numpad5)]
        [TestCase(KeyCode.KeypadEnter, Key.NumpadEnter)]
        [TestCase(KeyCode.LeftControl, Key.LeftCtrl)]
        [TestCase(KeyCode.BackQuote, Key.Backquote)]
        [TestCase(KeyCode.Numlock, Key.NumLock)]
        public void KeyboardKeys_MapToTheSameKey(KeyCode keyCode, Key expected)
        {
            Assert.That(InputSystemKeys.TryGetKey(keyCode, out var key), Is.True);
            Assert.That(key, Is.EqualTo(expected));
        }

        [TestCase(KeyCode.None)]
        [TestCase(KeyCode.Mouse0)]
        [TestCase(KeyCode.JoystickButton0)]
        public void NonKeyboardKeys_DoNotMap(KeyCode keyCode)
        {
            Assert.That(InputSystemKeys.TryGetKey(keyCode, out var key), Is.False);
            Assert.That(key, Is.EqualTo(Key.None));
        }

        [Test]
        public void WithoutAKeyboard_NothingIsPressed()
        {
            // バッチモードのテストにはキーボードが無いことがある。あっても、押していないキーは押されていない
            Assert.That(InputSystemKeys.WasPressedThisFrame(KeyCode.Space), Is.False);
        }
    }
}
