using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>Yarn プロジェクト 1 つのノード名と変数の初期値（<see cref="YarnProjects"/> が集める）。</summary>
    internal sealed class YarnProjectInfo
    {
        public YarnProjectInfo(string path, string name, IReadOnlyList<string> nodeNames, IReadOnlyDictionary<string, IConvertible> variables)
        {
            Path = path;
            Name = name;
            NodeNames = nodeNames;
            Variables = variables;
        }

        /// <summary>アセットのパス。</summary>
        public string Path { get; }

        /// <summary>アセットの名前。</summary>
        public string Name { get; }

        /// <summary>ユーザーが書いたノードの名前（名前の順）。</summary>
        public IReadOnlyList<string> NodeNames { get; }

        /// <summary>変数（<c>$Name</c>）の初期値。型は float / bool / string。</summary>
        public IReadOnlyDictionary<string, IConvertible> Variables { get; }
    }
}
