using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// ゲーム向けのコンポーネントのインスペクタの共通部分: 見た目（GameComponentInspector.uss）と、注意（HelpBox）の欄。
    /// </summary>
    public abstract class GameComponentEditor : UnityEditor.Editor
    {
        private const string StyleSheetPath = "VisualNodeEditor/GameComponentInspector";

        private VisualElement _hints;

        /// <summary>見た目を読み込んだ空のルート。</summary>
        protected static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.AddToClassList("vne-game-component-inspector");
            var styleSheet = Resources.Load<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            return root;
        }

        /// <summary>注意の欄を <paramref name="root"/> の末尾に足し、値が変わるたびに出し直す。</summary>
        protected void AddHints(VisualElement root)
        {
            _hints = new VisualElement();
            _hints.AddToClassList("vne-game-component-inspector__hints");
            root.Add(_hints);
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshHints());
            RefreshHints();
        }

        /// <summary>今の設定の注意（問題が無ければ空）。</summary>
        protected abstract List<string> GetHints();

        private void RefreshHints()
        {
            if (_hints == null || target == null)
            {
                return;
            }

            serializedObject.Update();
            _hints.Clear();
            foreach (var hint in GetHints())
            {
                _hints.Add(new HelpBox(hint, HelpBoxMessageType.Info));
            }
        }
    }
}
