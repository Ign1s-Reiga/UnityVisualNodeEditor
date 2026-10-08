using System.Text;

namespace Reiga.VisualNodeEditor.Editor.Behaviours
{
    /// <summary>Create Script… で書き出す <see cref="NodeBehaviour"/> の骨組み。</summary>
    public static class NodeBehaviourScriptTemplate
    {
        /// <summary>ファイル名から作れない（空など）ときのクラス名。</summary>
        public const string DefaultClassName = "NewNodeBehaviour";

        /// <summary>
        /// ファイル名からクラス名を作る。英数字以外で区切った語の先頭を大文字にしてつなげる（"player movement" → "PlayerMovement"）。
        /// 数字で始まるなら先頭に "_" を付ける。何も残らなければ <see cref="DefaultClassName"/>。
        /// </summary>
        public static string ToClassName(string fileName)
        {
            var builder = new StringBuilder();
            var startOfWord = true;
            foreach (var c in fileName ?? string.Empty)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    startOfWord = true;
                    continue;
                }

                builder.Append(startOfWord ? char.ToUpperInvariant(c) : c);
                startOfWord = false;
            }

            if (builder.Length == 0)
            {
                return DefaultClassName;
            }

            return char.IsDigit(builder[0]) ? "_" + builder : builder.ToString();
        }

        /// <summary><paramref name="className"/> の骨組み（OnEnter / OnUpdate / OnFixedUpdate / OnExit が空のクラス）。</summary>
        public static string Generate(string className) =>
            "using Reiga.VisualNodeEditor;\n"
            + "using UnityEngine;\n"
            + "\n"
            + "// Runs while the Graph Runner stays on the State / Scene node this is added to.\n"
            + "// Public and [SerializeField] fields are edited in the node's inspector and saved in the graph.\n"
            + "[System.Serializable]\n"
            + $"public class {className} : NodeBehaviour\n"
            + "{\n"
            + "    public override void OnEnter()\n"
            + "    {\n"
            + "    }\n"
            + "\n"
            + "    public override void OnUpdate(float deltaTime)\n"
            + "    {\n"
            + "    }\n"
            + "\n"
            + "    public override void OnFixedUpdate(float fixedDeltaTime)\n"
            + "    {\n"
            + "    }\n"
            + "\n"
            + "    public override void OnExit()\n"
            + "    {\n"
            + "    }\n"
            + "}\n";
    }
}
