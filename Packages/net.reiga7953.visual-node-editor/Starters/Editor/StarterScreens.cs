using System.Security;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// ゲームの Starter の UI Toolkit 版の画面（UXML と USS）の中身。画面は UI Builder でそのまま開いて直せる、ふつうの UXML / USS として書き出す。
    /// UXML には <c>graph</c> を書かない（コントロールは実行中のグラフを使う）。画面はグラフのノードに付け、Graph Screen Host が重ねて出す。
    /// </summary>
    internal static class StarterScreens
    {
        /// <summary>どの画面も共有する USS（uGUI 版の見た目に合わせる）。</summary>
        internal const string Style =
            "/* Screens of a Visual Node Editor starter. Edit them in UI Builder. */\n" +
            "\n" +
            ".vne-starter-screen {\n" +
            "    flex-grow: 1;\n" +
            "    align-items: center;\n" +
            "    justify-content: center;\n" +
            "    background-color: rgb(31, 31, 41);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-heading {\n" +
            "    font-size: 96px;\n" +
            "    color: rgb(255, 255, 255);\n" +
            "    -unity-font-style: bold;\n" +
            "}\n" +
            "\n" +
            ".vne-starter-controls {\n" +
            "    margin: 16px 0 24px 0;\n" +
            "    font-size: 26px;\n" +
            "    color: rgb(166, 166, 179);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-hud {\n" +
            "    position: absolute;\n" +
            "    top: 30px;\n" +
            "    left: 60px;\n" +
            "    right: 60px;\n" +
            "    flex-direction: row;\n" +
            "    justify-content: space-between;\n" +
            "}\n" +
            "\n" +
            ".vne-starter-hud > Label {\n" +
            "    font-size: 44px;\n" +
            "    color: rgb(255, 255, 255);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-board {\n" +
            "    width: 820px;\n" +
            "    height: 820px;\n" +
            "    background-color: rgb(18, 20, 26);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-panel {\n" +
            "    position: absolute;\n" +
            "    left: 0;\n" +
            "    top: 0;\n" +
            "    right: 0;\n" +
            "    bottom: 0;\n" +
            "    align-items: center;\n" +
            "    justify-content: center;\n" +
            "    background-color: rgba(0, 0, 0, 0.6);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-panel--clear {\n" +
            "    justify-content: flex-start;\n" +
            "    padding-top: 160px;\n" +
            "    background-color: rgba(0, 0, 0, 0);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-panel__heading {\n" +
            "    margin-bottom: 24px;\n" +
            "    font-size: 72px;\n" +
            "    color: rgb(255, 255, 255);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-panel__text {\n" +
            "    margin-bottom: 16px;\n" +
            "    font-size: 44px;\n" +
            "    color: rgb(255, 255, 255);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-panel__hint {\n" +
            "    margin-top: 12px;\n" +
            "    font-size: 26px;\n" +
            "    color: rgb(255, 255, 255);\n" +
            "}\n" +
            "\n" +
            ".vne-starter-button {\n" +
            "    width: 360px;\n" +
            "    height: 72px;\n" +
            "    margin: 8px;\n" +
            "    font-size: 32px;\n" +
            "}\n";

        /// <summary>
        /// Title の画面: 見出し（グラフ名）・操作の説明・<paramref name="startEvent"/> を Raise する Start の Graph Button・（あれば）キーの案内 <paramref name="hint"/>。
        /// <paramref name="styleFile"/> は同じフォルダの USS のファイル名。
        /// </summary>
        internal static string Title(string heading, string controls, string startEvent, string styleFile, string hint = null) =>
            Screen(styleFile,
                $"        <ui:Label name=\"heading\" text=\"{Escape(heading)}\" class=\"vne-starter-heading\" />\n" +
                $"        <ui:Label name=\"controls\" text=\"{Escape(controls)}\" class=\"vne-starter-controls\" />\n" +
                $"        {Button("start", "Start", startEvent)}\n" +
                (hint != null ? $"        {Label(hint, "vne-starter-panel__hint")}\n" : string.Empty));

        /// <summary>画面の UXML: USS を読み、<c>screen</c> の要素の中に <paramref name="body"/>（行ごとに字下げ済みの UXML）を置く。</summary>
        internal static string Screen(string styleFile, string body) => Document(styleFile, "class=\"vne-starter-screen\"", body);

        /// <summary>
        /// 下の画面（Game）の上に重ねる画面の UXML: 画面いっぱいの <c>screen</c>（既定は下を暗くする <c>vne-starter-panel</c>）の中に <paramref name="body"/>。
        /// <paramref name="passClicks"/> なら枠はクリックを受けない（下の画面をふさがない。暗くしない案内だけの画面に使う）。
        /// </summary>
        internal static string Overlay(string styleFile, string body, string cssClass = "vne-starter-panel", bool passClicks = false) =>
            Document(styleFile, $"class=\"{cssClass}\"" + (passClicks ? " picking-mode=\"Ignore\"" : string.Empty), body);

        private static string Document(string styleFile, string screenAttributes, string body) =>
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:vne=\"Reiga.VisualNodeEditor\" editor-extension-mode=\"False\">\n" +
            $"    <Style src=\"{Escape(styleFile)}\" />\n" +
            $"    <ui:VisualElement name=\"screen\" {screenAttributes}>\n" +
            body +
            "    </ui:VisualElement>\n" +
            "</ui:UXML>\n";

        /// <summary>
        /// イベントを Raise する Graph Button（1 行）。フォーカスを取らない（同じキーを Graph Event Key で送るので、フォーカスしたボタンが Enter / Space でもう一度送らないように）。
        /// </summary>
        internal static string Button(string name, string text, string eventName) =>
            $"<vne:GraphButton name=\"{Escape(name)}\" text=\"{Escape(text)}\" event-name=\"{Escape(eventName)}\" focusable=\"false\" class=\"vne-starter-button\" />";

        /// <summary>パラメータを書式で出す Graph Parameter Label（1 行）。シーンに置く文字は値 0 のときのもの。</summary>
        internal static string ParameterLabel(string name, string parameter, string format, string initial, string cssClass = null) =>
            $"<vne:GraphParameterLabel name=\"{Escape(name)}\" parameter=\"{Escape(parameter)}\" format=\"{Escape(format)}\" text=\"{Escape(initial)}\"" +
            (cssClass != null ? $" class=\"{cssClass}\"" : string.Empty) + " />";

        /// <summary>文字（1 行）。</summary>
        internal static string Label(string text, string cssClass) => $"<ui:Label text=\"{Escape(text)}\" class=\"{cssClass}\" />";

        /// <summary>XML の属性値に入れられる形にする。</summary>
        internal static string Escape(string text) => SecurityElement.Escape(text ?? string.Empty);
    }
}
