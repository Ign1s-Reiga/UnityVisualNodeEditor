using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// <see cref="NodeBehaviour"/> の Add Behaviour メニューでのパス（例: <c>"Visual Node Editor/Set Parameter"</c>）。
    /// 最後の区切りの後ろが、インスペクタとノードに出す名前になる。付けなければ、名前空間を階層にしたクラス名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BehaviourMenuAttribute : Attribute
    {
        /// <param name="path">メニューのパス（'/' で区切る）。</param>
        public BehaviourMenuAttribute(string path)
        {
            Path = path ?? string.Empty;
        }

        /// <summary>メニューのパス。</summary>
        public string Path { get; }

        /// <summary>パスの最後の部分（表示名）。</summary>
        public string Name
        {
            get
            {
                var slash = Path.LastIndexOf('/');
                return slash < 0 ? Path : Path.Substring(slash + 1);
            }
        }
    }
}
