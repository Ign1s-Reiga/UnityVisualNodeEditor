using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト用: 内容から決まる名前と、ノード自身の検証を持つノード（アドオンのノード型の代わり）。</summary>
    [Serializable]
    public sealed class ProbeNode : NodeData
    {
        public string Content { get; set; }

        public override string ContentTitle => Content;

        public override IEnumerable<GraphIssue> GetIssues()
        {
            if (string.IsNullOrEmpty(Content))
            {
                yield return new GraphIssue(GraphIssueSeverity.Warning, "Probe has no content.", Id);
            }
        }

        protected override string DefaultTitle => "Probe";
    }
}
