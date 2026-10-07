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

        // Start / Stop のたびに進める。通知の中で止めたり再開したりされたら、古い遷移はこれを見て打ち切る
        private int _generation;

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
            _generation++;
            _running.Add(this);
            Started?.Invoke(this);

            // 通知の中から呼ばれた（Stop → Start で再開された）場合は、いまの通知が終わってから始める
            if (_isMoving)
            {
                _pending.Enqueue(() => MoveTo(entry));
            }
            else
            {
                Run(() => MoveTo(entry));
            }
        }

        /// <summary>実行を止める。現在のノードから出たことを通知する。</summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            // 通知より先に停止状態にする。NodeExited の中で Raise / Advance されても積まずに false を返し、
            // 次の Start() の後に古い操作が実行されないようにする
            _pending.Clear();
            _generation++;
            IsRunning = false;
            _running.Remove(this);

            var current = Current;
            Current = null;
            if (current != null)
            {
                NodeExited?.Invoke(current);
            }

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

            // 出力エッジの無い Event（通知だけのイベント）の扱いは MoveTo が行う
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
            // 入れ子で呼ばれても、外側の Run が終わるまでは「遷移中」のままにする
            var wasMoving = _isMoving;
            _isMoving = true;
            try
            {
                action();
                while (IsRunning && _pending.Count > 0)
                {
                    _pending.Dequeue()();
                }
            }
            catch
            {
                // 通知の中で例外が出たら、この遷移の間に積まれた Raise / Advance を捨てる。
                // 残すと、後の無関係な Raise / Advance のついでに実行されてしまう
                if (!wasMoving)
                {
                    _pending.Clear();
                }

                throw;
            }
            finally
            {
                _isMoving = wasMoving;
            }
        }

        private void MoveTo(NodeData target)
        {
            var generation = _generation;
            var path = ResolvePath(target, out var endsInDeadEndEvent);

            // 通過ノードの連鎖が出力の無い Event で終わるなら、どこにも入らず通知だけ行い、いまの待機ノードに留まる
            // （入ってしまうと出口が無く、以後どの Raise / Advance でも動けなくなる）
            if (endsInDeadEndEvent)
            {
                foreach (var eventNode in path.OfType<EventNode>())
                {
                    if (!IsCurrentGeneration(generation))
                    {
                        return;
                    }

                    TriggerEvent(eventNode.EventName);
                }

                return;
            }

            foreach (var node in path)
            {
                if (!IsCurrentGeneration(generation) || !Enter(node, generation))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// <paramref name="target"/> から通過ノード（Entry・Event）をたどり、入るノードを順に返す。
        /// 待機ノードに着くか、Entry の先に Event 以外が無ければそこで終わる。
        /// 出力の無い Event に行き着いたら <paramref name="endsInDeadEndEvent"/> を true にする（その Event も含めて返し、通知に使う）。
        /// 通過ノードが輪になっていたら、警告を出して輪に入る手前までで止める。
        /// </summary>
        private List<NodeData> ResolvePath(NodeData target, out bool endsInDeadEndEvent)
        {
            endsInDeadEndEvent = false;
            var path = new List<NodeData>();
            var visited = new HashSet<string>();
            var node = target;
            while (node != null)
            {
                if (!visited.Add(node.Id))
                {
                    Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': Entry/Event nodes form a loop at '{node.Title}'. Stopped there.");
                    break;
                }

                if (node is EventNode eventNode)
                {
                    var next = Query.GetNext(eventNode.Id).FirstOrDefault();
                    path.Add(node);
                    if (next == null)
                    {
                        endsInDeadEndEvent = true;
                        break;
                    }

                    node = next;
                    continue;
                }

                path.Add(node);
                node = node is EntryNode entryNode ? Query.GetFirstNonEventNext(entryNode.Id) : null;
            }

            return path;
        }

        /// <summary>
        /// ノードに入る。通知の中で止められた・再開された（世代が変わった）ら、残りの処理をせず false を返す。
        /// </summary>
        private bool Enter(NodeData node, int generation)
        {
            var previous = Current;
            if (previous != null)
            {
                NodeExited?.Invoke(previous);
                if (!IsCurrentGeneration(generation))
                {
                    return false;
                }
            }

            Current = node;
            NodeEntered?.Invoke(node);
            if (!IsCurrentGeneration(generation))
            {
                return false;
            }

            switch (node)
            {
                case SceneNode sceneNode when !sceneNode.Scene.IsEmpty:
                    _sceneLoader?.LoadScene(sceneNode.Scene);
                    break;
                case EventNode eventNode:
                    TriggerEvent(eventNode.EventName);
                    break;
            }

            return IsCurrentGeneration(generation);
        }

        private bool IsCurrentGeneration(int generation) => IsRunning && generation == _generation;

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
