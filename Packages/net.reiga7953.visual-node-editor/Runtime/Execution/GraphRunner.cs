using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフをシーン遷移表・ステートマシンとして実行する。MonoBehaviour に依存しない純粋な C# なので、テストから直接動かせる。
    /// <list type="bullet">
    /// <item>待機ノード（Scene・State など）: 入ると止まる。Scene に入るとシーンを読み込む。
    /// 付いている <see cref="NodeBehaviour"/> は、入ると OnEnter、<see cref="Update"/> / <see cref="FixedUpdate"/> のたびに OnUpdate / OnFixedUpdate、出ると OnExit が呼ばれる</item>
    /// <item>通過ノード（Entry・Event・コンテナとその Entry / Exit）: 入ったら止まらずに次へ進む。コンテナに入ると中の Entry から、Exit で出ると親の階層の対応する出力ポートから続く</item>
    /// </list>
    /// 遷移先が複数あるときは、アセット内のエッジの順で最初のものを使う。
    /// </summary>
    public sealed class GraphRunner
    {
        private static readonly List<GraphRunner> _running = new();

        private readonly ISceneLoader _sceneLoader;
        private readonly Queue<Action> _pending = new();
        private readonly Dictionary<string, Action> _eventHandlers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GraphParameterType> _parameterTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _parameterValues = new(StringComparer.Ordinal);
        private bool _isMoving;

        // Start / Stop のたびに進める。通知の中で止めたり再開したりされたら、古い遷移はこれを見て打ち切る
        private int _generation;

        // ノード ID → この Runner 用に複製した振る舞い。Start() のたびに作り直す
        private readonly Dictionary<string, List<NodeBehaviour>> _behaviours = new(StringComparer.Ordinal);

        // OnEnter を呼んだ（まだ OnExit を呼んでいない）振る舞い。OnExit はこれにだけ、1 回ずつ呼ぶ
        private readonly List<NodeBehaviour> _activeBehaviours = new();

        // ノードに入るたびに進める。同じノードへ戻る遷移でも変わるので、振る舞いの更新の打ち切りに使う
        private int _entryCount;

        /// <summary>
        /// 振る舞いで出た例外の扱い。既定はログに出すだけ（次の振る舞いへ進む）。テストで差し替える。
        /// </summary>
        internal static Action<Exception> BehaviourExceptionHandler = Debug.LogException;

        /// <param name="graph">実行するグラフ。</param>
        /// <param name="sceneLoader">Scene ノードに入ったときに使う。null ならシーンを読み込まない。</param>
        public GraphRunner(NodeGraphAsset graph, ISceneLoader sceneLoader = null)
        {
            Query = new GraphQuery(graph);
            _sceneLoader = sceneLoader;
            ResetParameters();
        }

        /// <summary>パラメータの値が <see cref="SetBool"/> などで変わったとき。引数はパラメータ名。</summary>
        public event Action<string> ParameterChanged;

        /// <summary>その名前のパラメータがあるか。</summary>
        public bool HasParameter(string name) => name != null && _parameterTypes.ContainsKey(name);

        /// <summary>その名前と型のパラメータがあるか（ある名前でも型が違えば false。<see cref="GetBool"/> などは型が違うと例外になる）。</summary>
        public bool HasParameter(string name, GraphParameterType type) =>
            name != null && _parameterTypes.TryGetValue(name, out var actual) && actual == type;

        public bool GetBool(string name) => (bool)GetParameter(name, GraphParameterType.Bool);

        public int GetInt(string name) => (int)GetParameter(name, GraphParameterType.Int);

        public float GetFloat(string name) => (float)GetParameter(name, GraphParameterType.Float);

        public string GetString(string name) => (string)GetParameter(name, GraphParameterType.String);

        public void SetBool(string name, bool value) => SetParameter(name, GraphParameterType.Bool, value);

        public void SetInt(string name, int value) => SetParameter(name, GraphParameterType.Int, value);

        public void SetFloat(string name, float value) => SetParameter(name, GraphParameterType.Float, value);

        public void SetString(string name, string value) => SetParameter(name, GraphParameterType.String, value ?? string.Empty);

        /// <summary>
        /// パラメータの値をアセットの既定値に戻す（<see cref="Start"/> のたびにも行う）。
        /// 値が変わったパラメータは <see cref="ParameterChanged"/> で通知する（表示などが古い値のまま残らないように）。
        /// 名前が空・重複するパラメータは、検証で Error になる状態なので最初のものだけを使う。
        /// </summary>
        public void ResetParameters()
        {
            var previous = new Dictionary<string, object>(_parameterValues, StringComparer.Ordinal);
            _parameterTypes.Clear();
            _parameterValues.Clear();
            foreach (var parameter in Graph.Parameters)
            {
                if (parameter != null && !string.IsNullOrEmpty(parameter.Name) && !_parameterTypes.ContainsKey(parameter.Name))
                {
                    _parameterTypes.Add(parameter.Name, parameter.Type);
                    _parameterValues.Add(parameter.Name, parameter.DefaultValue);
                }
            }

            // 通知は値をすべて戻し終えてから行う（通知の中で他のパラメータを読んでも既定値が返るように）
            var changed = _parameterValues
                .Where(p => !previous.TryGetValue(p.Key, out var old) || !Equals(old, p.Value))
                .Select(p => p.Key)
                .ToList();
            foreach (var name in changed)
            {
                ParameterChanged?.Invoke(name);
            }
        }

        private object GetParameter(string name, GraphParameterType requested)
        {
            CheckParameter(name, requested);
            return _parameterValues[name];
        }

        private void SetParameter(string name, GraphParameterType requested, object value)
        {
            CheckParameter(name, requested);
            if (Equals(_parameterValues[name], value))
            {
                return;
            }

            _parameterValues[name] = value;
            ParameterChanged?.Invoke(name);
        }

        private void CheckParameter(string name, GraphParameterType requested)
        {
            if (name == null || !_parameterTypes.TryGetValue(name, out var actual))
            {
                throw new KeyNotFoundException($"Graph '{Graph.name}' has no parameter named '{name}'.");
            }

            if (actual != requested)
            {
                throw new InvalidOperationException($"Parameter '{name}' is {actual}, not {requested}.");
            }
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

        /// <summary>この Runner を動かしているコンポーネント。<see cref="GraphRunner"/> を直接使っている場合は null。</summary>
        public GraphRunnerBehaviour Host { get; internal set; }

        /// <summary>
        /// 現在のノードの振る舞いの <see cref="NodeBehaviour.OnUpdate"/> を順に呼ぶ。<see cref="GraphRunnerBehaviour"/> が毎フレーム呼ぶ
        /// （Runner を直接使う場合は自分で呼ぶ）。途中の振る舞いが遷移・停止したら、残りは呼ばない。
        /// </summary>
        public void Update(float deltaTime) => TickBehaviours(b => b.OnUpdate(deltaTime));

        /// <summary>
        /// 現在のノードの振る舞いの <see cref="NodeBehaviour.OnFixedUpdate"/> を順に呼ぶ。<see cref="GraphRunnerBehaviour"/> が物理の更新ごとに呼ぶ。
        /// </summary>
        public void FixedUpdate(float fixedDeltaTime) => TickBehaviours(b => b.OnFixedUpdate(fixedDeltaTime));

        /// <summary>
        /// この Runner がノードで動かす振る舞い（アセットの値から複製したもの。<see cref="Start"/> のたびに作り直す）。
        /// 振る舞いを持たないノードなら空。読めなかった振る舞い（null）は含まない。
        /// </summary>
        public IReadOnlyList<NodeBehaviour> GetBehaviours(NodeData node)
        {
            if (!(node is IBehaviourHost host) || host.Behaviours.Count == 0)
            {
                return Array.Empty<NodeBehaviour>();
            }

            if (!_behaviours.TryGetValue(node.Id, out var instances))
            {
                instances = new List<NodeBehaviour>();
                foreach (var template in host.Behaviours)
                {
                    // 型が削除・改名されて読めなかったものは飛ばす（検証で Warning になる）
                    if (template == null)
                    {
                        continue;
                    }

                    var instance = (NodeBehaviour)JsonUtility.FromJson(JsonUtility.ToJson(template), template.GetType());
                    instance.Bind(this, node);
                    instances.Add(instance);
                }

                _behaviours[node.Id] = instances;
            }

            return instances;
        }

        private void TickBehaviours(Action<NodeBehaviour> call)
        {
            if (!IsRunning || _activeBehaviours.Count == 0)
            {
                return;
            }

            var entry = _entryCount;
            var generation = _generation;
            foreach (var behaviour in _activeBehaviours.ToArray())
            {
                InvokeBehaviour(behaviour, call);

                // 振る舞いの中で遷移・停止したら、残りは呼ばない。同じノードへ戻った場合も、
                // 入り直した振る舞いは OnEnter を済ませたばかりなので、更新は次の Update から
                if (!IsCurrentGeneration(generation) || _entryCount != entry)
                {
                    return;
                }
            }
        }

        /// <summary>ノードの振る舞いの OnEnter を順に呼ぶ。途中で止められた・再開されたら false。</summary>
        private bool EnterBehaviours(NodeData node, int generation)
        {
            foreach (var behaviour in GetBehaviours(node))
            {
                _activeBehaviours.Add(behaviour);
                InvokeBehaviour(behaviour, b => b.OnEnter());
                if (!IsCurrentGeneration(generation))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>OnEnter を呼んだ振る舞いの OnExit を、1 回ずつ呼ぶ（中で Stop() されても二重には呼ばない）。</summary>
        private void ExitBehaviours()
        {
            if (_activeBehaviours.Count == 0)
            {
                return;
            }

            var active = _activeBehaviours.ToArray();
            _activeBehaviours.Clear();
            foreach (var behaviour in active)
            {
                InvokeBehaviour(behaviour, b => b.OnExit());
            }
        }

        private static void InvokeBehaviour(NodeBehaviour behaviour, Action<NodeBehaviour> call)
        {
            // 1 つの振る舞いの不具合で Runner 全体が止まらないよう、ログに出して次へ進む
            try
            {
                call(behaviour);
            }
            catch (Exception exception)
            {
                BehaviourExceptionHandler?.Invoke(exception);
            }
        }

        /// <summary>
        /// 現在のノードを囲むコンテナ（外側から順）。ルート階層にいれば空。
        /// コンテナに入るたびに積み、Exit で出るたびに下ろすスタックと同じ内容を、現在のノードの親の連なりから求める。
        /// </summary>
        public IReadOnlyList<ContainerNode> ContainerPath => Query.GetContainerPath(Current);

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

            // 毎回の実行をアセットの既定値から始める（振る舞いも、入るときにアセットの値から複製し直す）
            ResetParameters();
            _behaviours.Clear();
            _activeBehaviours.Clear();
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
                ExitBehaviours();
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
            var path = ResolvePath(target, out var endsInDeadEnd);

            // 通過ノードの連鎖が行き止まり（出力の無い Event、Entry の無い・Entry からエッジの出ていないコンテナ、繋がっていない出口）で終わるなら、
            // どこにも入らず途中の Event の通知だけ行い、いまの待機ノードに留まる
            // （入ってしまうと出口が無く、以後どの Raise / Advance でも動けなくなる）
            if (endsInDeadEnd)
            {
                foreach (var eventNode in path.OfType<EventNode>())
                {
                    if (!IsCurrentGeneration(generation))
                    {
                        return;
                    }

                    TriggerEvent(eventNode.EventName);
                }

                // 開始直後（まだどこにもいない）なら、Entry に入ってそこに留まる（Entry の先が無いときと同じ）
                if (Current == null && path.Count > 0 && IsCurrentGeneration(generation))
                {
                    Enter(path[0], generation);
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
        /// 行き止まり（出力の無い Event、Entry の無い・Entry からエッジの出ていないコンテナ、繋がっていない出口）に行き着いたら <paramref name="endsInDeadEnd"/> を true にする
        /// （そこまでのノードも返し、途中の Event の通知に使う）。コンテナ・Entry・Exit も通過ノードとしてたどる。
        /// 通過ノードが輪になっていたら、警告を出して輪に入る手前までで止める。
        /// </summary>
        private List<NodeData> ResolvePath(NodeData target, out bool endsInDeadEnd)
        {
            endsInDeadEnd = false;
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
                        endsInDeadEnd = true;
                        break;
                    }

                    node = next;
                    continue;
                }

                path.Add(node);
                switch (node)
                {
                    // コンテナに着いたら中に入り、中の Entry から続ける
                    case ContainerNode container:
                        node = Query.GetContainerEntry(container);
                        if (node == null)
                        {
                            Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': container '{container.Title}' has no Entry. Stayed before it.");
                            endsInDeadEnd = true;
                        }

                        break;

                    // Exit に着いたらコンテナを出て、出力ポートのうち ID が同じものの先へ続ける
                    case ContainerExitNode exitNode:
                        node = Query.GetExitTarget(exitNode);
                        if (node == null)
                        {
                            // 繋がっていないときの扱いは未決事項。当面は出口の無い Event と同じく、直前の待機ノードに留まる
                            var exitName = Query.GetParentContainer(exitNode)?.FindExit(exitNode.ExitId)?.Name ?? exitNode.ExitId;
                            Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': exit '{exitName}' of '{Query.GetParentContainer(exitNode)?.Title}' is not connected. Stayed before it.");
                            endsInDeadEnd = true;
                        }

                        break;

                    // 1 本もエッジが出ていないコンテナの Entry は、Entry の無いコンテナと同じく行き止まり。
                    // 入ると通過ノードの Entry で止まり、以後どの Raise / Advance でも動けなくなる
                    case ContainerEntryNode containerEntry when Query.GetNext(containerEntry.Id).Count == 0:
                        Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': the Entry of container '{Query.GetParentContainer(containerEntry)?.Title}' is not connected. Stayed before it.");
                        endsInDeadEnd = true;
                        node = null;
                        break;

                    case EntryNode _:
                    case ContainerEntryNode _:
                        node = Query.GetFirstNonEventNext(node.Id);
                        break;

                    default:
                        node = null;
                        break;
                }
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
                // 振る舞いの OnExit → NodeExited の順。どちらの中で止められても、それ以上は進めない
                ExitBehaviours();
                if (!IsCurrentGeneration(generation))
                {
                    return false;
                }

                NodeExited?.Invoke(previous);
                if (!IsCurrentGeneration(generation))
                {
                    return false;
                }
            }

            Current = node;
            _entryCount++;
            if (!EnterBehaviours(node, generation))
            {
                return false;
            }

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
