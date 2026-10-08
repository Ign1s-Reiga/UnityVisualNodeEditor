using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノード追加メニューでの表示パスを指定する。例: <c>[NodeMenu("Flow/Scene")]</c>
    /// パスの先頭の区切りはカテゴリ（ノードの色分け）にも使う。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeMenuAttribute : Attribute
    {
        public NodeMenuAttribute(string path) => Path = path;

        public string Path { get; }

        /// <summary>
        /// Create Node メニューに出さない（カテゴリの色分けだけに使う）。
        /// エディタが自動で作るノード（コンテナの Entry など）に付ける。
        /// </summary>
        public bool Hidden { get; set; }
    }
}
