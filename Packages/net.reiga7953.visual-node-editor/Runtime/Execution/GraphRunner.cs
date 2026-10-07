using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフをシーン遷移表・ステートマシンとして実行する。MonoBehaviour に依存しない純粋な C# なので、テストから直接動かせる。
    /// <list type="bullet">
    /// <item>待機ノード（Scene・State など）: 入ると止まる。Scene に入るとシーンを読み込む</item>
    /// <item>通過ノード（Entry・Event）: 入ったら止まらずに次へ進む</item>
    /// </list>
    /// 遷移先が複数あるときは、アセット内のエッジの順で最初のものを使う。
    /// </summary>
    public sealed class GraphRunner
    {
        private static readonly List<GraphRunner> _running = new();

        private readonly ISceneLoader _sceneLoader;
        private readonly Queue<Action> _pending = new();
        private readonly Dictionary<string, Action> _eventHandlers = new(StringComparer.Ordinal);
        private bool _isMoving;

        /// <param name="graph">実行するグラフ。</param>
        /// <param name="sceneLoader">Scene ノードに入ったときに使う。null ならシーンを読み込まない。</param>
        public GraphRunner(NodeGraphAsset graph, ISceneLoader sceneLoader = null)
        {
            Query = new GraphQuery(graph);
            _sceneLoader = sceneLoader;
        }

        /// <summary>実行中のすべての Runner（エディタの強調表示などが使う）。</summary>
        public static IReadOnlyList<GraphRunner> Running => _running;

        /// <summary>いずれかの Runner が開始したとき。</summary>
        public static event Action<GraphRunner> Started;

        /// <summary>いずれかの Runner が停止したとき。</summary>
        public static event Action<GraphRunner> Stopped;

        /// <summary>ノードに入ったとき（通過ノードも含む）。</summary>
        public event Action<NodeData> NodeEntered;

        /// <summary>ノードから出たとき。</summary>
        public event Action<NodeData> NodeExited;

        /// <summary>
        /// イベントが発生したとき（Event ノードを通過した、または出力エッジの無い Event が <see cref="Raise"/> された）。引数はイベント名。
        /// </summary>
        public event Action<string> EventTriggered;

        /// <summary>特定のイベント名が発生したときに <paramref name="handler"/> を呼ぶ。</summary>
        public void On(string eventName, Action handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
            {
                return;
            }

            _eventHandlers[eventName] = _eventHandlers.TryGetValue(eventName, out var existing) ? existing + handler : handler;
        }

        /// <summary><see cref="On"/> で登録した <paramref name="handler"/> を外す。</summary>
        public void Off(string eventName, Action handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null || !_eventHandlers.TryGetValue(eventName, out var existing))
            {
                return;
            }

            var remaining = existing - handler;
            if (remaining == null)
            {
                _eventHandlers.Remove(eventName);
            }
            else
            {
                _eventHandlers[eventName] = remaining;
            }
        }

        /// <summary>グラフの読み取り用ビュー。</summary>
        public GraphQuery Query { get; }

        /// <summary>実行しているグラフ。</summary>
        public NodeGraphAsset Graph => Query.Graph;

        /// <summary>現在のノード。停止中は null。</summary>
        public NodeData Current { get; private set; }

        /// <summary>実行中か。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Entry から実行を始める。すでに実行中なら何もしない。</summary>
        /// <exception cref="InvalidOperationException">グラフに Entry が無い。</exception>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            var entry = Query.Entry;
            if (entry == null)
            {
                throw new InvalidOperationException($"Graph '{Graph.name}' has no Entry node.");
            }

            IsRunning = true;
            _running.Add(this);
            Started?.Invoke(this);
            Run(() => MoveTo(entry));
        }

        /// <summary>実行を止める。現在のノードから出たことを通知する。</summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _pending.Clear();
            var current = Current;
            Current = null;
            if (current != null)
            {
                NodeExited?.Invoke(current);
            }

            IsRunning = false;
            _running.Remove(this);
            Stopped?.Invoke(this);
        }

        /// <summary>
        /// 現在のノードから、イベント名が一致する Event ノードを経由して次へ進む。
        /// 遷移が見つかれば true（ノードの出入りの通知中に呼んだ場合は、その処理の後に実行し true を返す）。
        /// </summary>
        public bool Raise(string eventName)
        {
            if (!IsRunning)
            {
                return false;
            }

            if (_isMoving)
            {
                _pending.Enqueue(() => RaiseNow(eventName));
                return true;
            }

            var raised = false;
            Run(() => raised = RaiseNow(eventName));
            return raised;
        }

        /// <summary>
        /// 現在のノードから、Event 以外のノードへの最初のエッジに沿って進む。
        /// 進めれば true（ノードの出入りの通知中に呼んだ場合は、その処理の後に実行し true を返す）。
        /// </summary>
        public bool Advance()
        {
            if (!IsRunning)
            {
                return false;
            }

            if (_isMoving)
            {
                _pending.Enqueue(() => AdvanceNow());
                return true;
            }

            var advanced = false;
            Run(() => advanced = AdvanceNow());
            return advanced;
        }

        private bool RaiseNow(string eventName)
        {
            var eventNode = Current != null ? Query.FindEventTransition(Current.Id, eventName) : null;
            if (eventNode == null)
            {
                return false;
            }

            // 出力エッジの無い Event は通知だけで、現在のノードは変わらない
            if (Query.GetNext(eventNode.Id).Count == 0)
            {
                TriggerEvent(eventNode.EventName);
                return true;
            }

            MoveTo(eventNode);
            return true;
        }

        private bool AdvanceNow()
        {
            var next = Current != null ? Query.GetFirstNonEventNext(Current.Id) : null;
            if (next == null)
            {
                return false;
            }

            MoveTo(next);
            return true;
        }

        /// <summary>通知中に Raise / Advance が呼ばれても、いま進めている遷移を壊さないよう順番に実行する。</summary>
        private void Run(Action action)
        {
            _isMoving = true;
            try
            {
                action();
                while (IsRunning && _pending.Count > 0)
                {
                    _pending.Dequeue()();
                }
            }
            finally
            {
                _isMoving = false;
            }
        }

        private void MoveTo(NodeData target)
        {
            var visited = new HashSet<string>();
            var node = target;
            while (node != null && IsRunning)
            {
                if (!visited.Add(node.Id))
                {
                    Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': Entry/Event nodes form a loop at '{node.Title}'. Stopped there.");
                    return;
                }

                Enter(node);
                node = node switch
                {
                    EventNode eventNode => Query.GetNext(eventNode.Id).FirstOrDefault(),
                    EntryNode entryNode => Query.GetFirstNonEventNext(entryNode.Id),
                    _ => null,
                };
            }
        }

        private void Enter(NodeData node)
        {
            var previous = Current;
            if (previous != null)
            {
                NodeExited?.Invoke(previous);
            }

            Current = node;
            NodeEntered?.Invoke(node);

            switch (node)
            {
                case SceneNode sceneNode when !sceneNode.Scene.IsEmpty:
                    _sceneLoader?.LoadScene(sceneNode.Scene);
                    break;
                case EventNode eventNode:
                    TriggerEvent(eventNode.EventName);
                    break;
            }
        }

        private void TriggerEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }

            EventTriggered?.Invoke(eventName);
            if (_eventHandlers.TryGetValue(eventName, out var handler))
            {
                handler();
            }
        }

        // 「Enter Play Mode Options」でドメインリロードを切っても、前回の Play の Runner が残らないようにする。
        // Started / Stopped はエディタ側の購読なので消さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRunningList() => _running.Clear();
    }
}
