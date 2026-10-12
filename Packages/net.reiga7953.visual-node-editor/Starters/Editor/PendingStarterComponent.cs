using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// Starter が書き出したスクリプトを、コンパイルの後でシーンに付ける予定（どのシーンの、どの GameObject に、どのクラスを付け、どのグラフを入れるか）。
    /// ドメインリロードを越えて残すため、JSON にして <c>SessionState</c> に置く（<see cref="PendingStarterComponents"/>）。
    /// </summary>
    [Serializable]
    internal sealed class PendingStarterComponent
    {
        [SerializeField] private string _scenePath;
        [SerializeField] private string _objectPath;
        [SerializeField] private string _className;
        [SerializeField] private string _graphGuid;

        /// <summary>JsonUtility が読み戻すときに使う。</summary>
        public PendingStarterComponent()
        {
        }

        public PendingStarterComponent(string scenePath, string objectPath, string className, string graphGuid)
        {
            _scenePath = scenePath;
            _objectPath = objectPath;
            _className = className;
            _graphGuid = graphGuid;
        }

        /// <summary>付けるシーンのパス。</summary>
        public string ScenePath => _scenePath;

        /// <summary>付ける GameObject の、シーンの一番上からのパス（例: <c>Canvas/Board</c>）。</summary>
        public string ObjectPath => _objectPath;

        /// <summary>付けるクラスの完全名（名前空間無し）。</summary>
        public string ClassName => _className;

        /// <summary>そのコンポーネントの <c>_graph</c> に入れるグラフの GUID。</summary>
        public string GraphGuid => _graphGuid;
    }
}
