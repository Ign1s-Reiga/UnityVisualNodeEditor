using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノード追加メニューでの表示パスを指定する。例: <c>[NodeMenu("Flow/Scene")]</c>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeMenuAttribute : Attribute
    {
        public NodeMenuAttribute(string path) => Path = path;

        public string Path { get; }
    }
}
