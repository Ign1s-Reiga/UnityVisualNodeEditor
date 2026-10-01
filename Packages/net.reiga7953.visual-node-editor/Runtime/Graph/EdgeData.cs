using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ノード間の接続。ポート名で接続先を識別する。</summary>
    [Serializable]
    public sealed class EdgeData
    {
        [SerializeField] private string _fromNodeId;
        [SerializeField] private string _fromPort;
        [SerializeField] private string _toNodeId;
        [SerializeField] private string _toPort;

        public EdgeData(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            _fromNodeId = fromNodeId;
            _fromPort = fromPort;
            _toNodeId = toNodeId;
            _toPort = toPort;
        }

        public string FromNodeId => _fromNodeId;
        public string FromPort => _fromPort;
        public string ToNodeId => _toNodeId;
        public string ToPort => _toPort;
    }
}
