using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Physics2D
{
    /// <summary>
    /// プレイヤーを出す地点。シーンが始まったら、String パラメータ（既定 <c>SpawnPoint</c>）がこの地点の名前を指していれば、
    /// タグ（既定 <c>Player</c>）の GameObject をここへ置く（Rigidbody 2D があれば速度も 0 にする）。
    /// どの地点も指していなければ（空・別のシーンの名前）、<see cref="IsDefault"/> の地点に置く。
    /// チェックポイント・扉は Graph Trigger 2D でパラメータを変えて作る（<c>SpawnPoint = "Checkpoint 2"</c>）。パラメータなので、セーブ・ロードにも残る。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Spawn Point 2D")]
    public sealed class GraphSpawnPoint2D : MonoBehaviour
    {
        [Tooltip("The graph whose String parameter names the spawn point. Empty: any running graph.")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("The String parameter that holds the name of the point to spawn at (set it with Set Parameter or Graph Trigger 2D).")]
        [SerializeField, GraphParameterName] private string _parameter = "SpawnPoint";

        [Tooltip("This point's name, which the parameter holds to spawn here. Empty: the GameObject's name.")]
        [SerializeField] private string _pointName = string.Empty;

        [Tooltip("The tag of the player to put here.")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Spawn here when the parameter names no point in this scene (empty at the start of a stage, or the name of a point in another scene).")]
        [SerializeField] private bool _default;

        private static readonly HashSet<string> _warnedTags = new HashSet<string>();

        // 有効な地点（シーンが始まったとき、ほかの地点と比べて選ぶため）
        private static readonly List<GraphSpawnPoint2D> _enabled = new List<GraphSpawnPoint2D>();

        /// <summary>名前のパラメータを持つグラフ。null なら実行中のどれか。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>地点の名前を入れる String パラメータの名前。</summary>
        public string Parameter
        {
            get => _parameter;
            set => _parameter = value ?? string.Empty;
        }

        /// <summary>この地点の名前（空なら GameObject の名前）。</summary>
        public string PointName
        {
            get => string.IsNullOrEmpty(_pointName) ? name : _pointName;
            set => _pointName = value ?? string.Empty;
        }

        /// <summary>プレイヤーのタグ。</summary>
        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value ?? string.Empty;
        }

        /// <summary>パラメータがこのシーンのどの地点も指していないときに使う地点か。</summary>
        public bool IsDefault
        {
            get => _default;
            set => _default = value;
        }

        /// <summary>
        /// 地点の並び（名前と Default か）から、<paramref name="wanted"/> の名前で出す地点を選ぶ: 名前が合う最初の地点、無ければ最初の Default。どれも無ければ -1。
        /// </summary>
        public static int Choose(IReadOnlyList<(string Name, bool IsDefault)> points, string wanted)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (!string.IsNullOrEmpty(wanted) && string.Equals(points[i].Name, wanted, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].IsDefault)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary><paramref name="player"/> をこの地点へ置く（奥行き z はそのまま）。Rigidbody 2D があれば、位置を移して速度と角速度を 0 にする。</summary>
        public void Spawn(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            var target = transform.position;
            var position = new Vector3(target.x, target.y, player.transform.position.z);
            if (player.TryGetComponent<Rigidbody2D>(out var body))
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            player.transform.position = position;
        }

        /// <summary>タグのプレイヤー（無い・タグが未定義なら null。未定義なら 1 度だけ警告を出す）。</summary>
        public GameObject FindPlayer()
        {
            if (string.IsNullOrEmpty(_playerTag))
            {
                return null;
            }

            try
            {
                return GameObject.FindGameObjectWithTag(_playerTag);
            }
            catch (UnityException)
            {
                if (_warnedTags.Add(_playerTag))
                {
                    Debug.LogWarning($"[VisualNodeEditor] Graph Spawn Point 2D: the tag '{_playerTag}' is not defined. Add it in Tags and Layers.", this);
                }

                return null;
            }
        }

        // シーンが始まったら選ぶ。Runner がまだ始まっていなければ（同じフレームに Graph Runner の Start が後で来る）、次のフレームにもう一度探す
        private IEnumerator Start()
        {
            var runner = GraphRunner.FindRunning(_graph);
            if (runner == null)
            {
                yield return null;
                runner = GraphRunner.FindRunning(_graph);
            }

            var wanted = runner != null && runner.HasParameter(_parameter, GraphParameterType.String) ? runner.GetString(_parameter) : string.Empty;

            var points = Candidates(_enabled);
            var chosen = Choose(points.Select(point => (point.PointName, point._default)).ToList(), wanted);
            if (chosen >= 0 && points[chosen] == this)
            {
                Spawn(FindPlayer());
            }
        }

        /// <summary>
        /// <paramref name="points"/> のうち、この地点と比べて選ぶもの: 同じシーンの、同じグラフ・同じパラメータを見る地点（名前の順。Default が 2 つあれば名前が先のもの）。
        /// ほかのグラフの地点は、そのグラフのパラメータで選ばれるので混ぜない。グラフが空の地点は、実行中のグラフを見るものとして比べる
        /// （グラフを入れた地点と空の地点が混ざっていても、同じ Runner を見ていれば 1 つにまとめる）。テストからも呼ぶ。
        /// </summary>
        internal List<GraphSpawnPoint2D> Candidates(IEnumerable<GraphSpawnPoint2D> points)
        {
            var graph = Resolve(_graph);
            return points
                .Where(point => point != null && point.gameObject.scene == gameObject.scene && Resolve(point._graph) == graph && point._parameter == _parameter)
                .OrderBy(point => point.PointName, StringComparer.Ordinal)
                .ToList();
        }

        // 空のグラフは「実行中のどれか」なので、そのグラフとして比べる（実行中のものが無ければ空のまま）
        private static NodeGraphAsset Resolve(NodeGraphAsset graph) => graph != null ? graph : GraphRunner.FindRunning()?.Graph;

        private void OnEnable() => _enabled.Add(this);

        private void OnDisable() => _enabled.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _warnedTags.Clear();
            _enabled.Clear();
        }
    }
}
