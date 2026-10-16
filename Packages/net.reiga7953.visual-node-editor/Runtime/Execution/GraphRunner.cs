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

        // パラメータの ID → 名前（Event の条件は ID でパラメータを指す）
        private readonly Dictionary<string, string> _parameterNamesById = new(StringComparer.Ordinal);
        private bool _isMoving;

        // 1 回の操作の中で続けて行える自動遷移の数（A → B → A … と輪になったときに止める）
        private const int MaxAutomaticSteps = 100;
        private int _automaticSteps;

        // 自動の Event を調べる処理を、遷移の後に積んであるか（何度値が変わっても 1 回だけ積む）
        private bool _automaticCheckQueued;

        // IRestartOnLoad のノードに入る直前の状態と、そのときの入った回数（そのノードにいる間の CaptureState はこれを返す）
        private GraphRunnerState _restartState;
        private int _restartStateEntry = -1;

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
        public void ResetParameters() => SetParameterValues(null);

        /// <summary>
        /// パラメータを既定値にし、<paramref name="saved"/> があれば、そのうち同じ名前・型のものの値を入れる（セーブからの再開）。
        /// 値が変わったパラメータは、すべて入れ終えてから通知する。
        /// </summary>
        private void SetParameterValues(IReadOnlyList<GraphStateValue> saved)
        {
            var previous = new Dictionary<string, object>(_parameterValues, StringComparer.Ordinal);
            _parameterTypes.Clear();
            _parameterValues.Clear();
            _parameterNamesById.Clear();
            foreach (var parameter in Graph.Parameters)
            {
                if (parameter != null && !string.IsNullOrEmpty(parameter.Name) && !_parameterTypes.ContainsKey(parameter.Name))
                {
                    _parameterTypes.Add(parameter.Name, parameter.Type);
                    _parameterValues.Add(parameter.Name, parameter.DefaultValue);
                    if (!string.IsNullOrEmpty(parameter.Id))
                    {
                        _parameterNamesById.TryAdd(parameter.Id, parameter.Name);
                    }
                }
            }

            // セーブの後にパラメータを消したり型を変えたりしていたら、そのパラメータは既定値のまま
            foreach (var value in saved ?? Array.Empty<GraphStateValue>())
            {
                if (value != null && value.Key != null && _parameterTypes.TryGetValue(value.Key, out var type) && type == value.Type)
                {
                    _parameterValues[value.Key] = value.Value;
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

            if (changed.Count > 0)
            {
                RequestAutomaticCheck();
            }
        }

        /// <summary>
        /// その Event の条件をすべて満たしているか（条件が無ければ true）。パラメータが無い・型の合わない条件は満たさない。
        /// </summary>
        public bool AreConditionsMet(EventNode eventNode)
        {
            if (eventNode == null)
            {
                return false;
            }

            foreach (var condition in eventNode.Conditions)
            {
                if (!IsConditionMet(condition))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>条件 1 つを今の値で満たしているか。パラメータが無い・型の合わない条件は満たさない。</summary>
        public bool IsConditionMet(TransitionCondition condition) =>
            condition != null
            && condition.ParameterId != null
            && _parameterNamesById.TryGetValue(condition.ParameterId, out var name)
            && condition.IsMet(_parameterTypes[name], _parameterValues[name]);

        /// <summary>
        /// 今のノードで <see cref="Raise"/> したときに選ばれる Event（名前が一致し、トリガーが Raise で、条件を満たす最初のもの）。無ければ null。
        /// </summary>
        public EventNode FindRaiseTarget(string eventName) =>
            Current != null ? Query.FindEventTransition(Current.Id, eventName, AreConditionsMet) : null;

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
            RequestAutomaticCheck();
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

        /// <summary><paramref name="graph"/> を実行中の Runner（null なら実行中のどれか）。無ければ null。</summary>
        public static GraphRunner FindRunning(NodeGraphAsset graph = null) =>
            _running.FirstOrDefault(runner => runner.IsRunning && (graph == null || runner.Graph == graph));

        /// <summary>その名前のパラメータの今の値（bool / int / float / string）。無ければ false。</summary>
        public bool TryGetParameter(string name, out object value)
        {
            value = null;
            return name != null && _parameterValues.TryGetValue(name, out value);
        }

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

        /// <summary>
        /// パラメータ（Blackboard）に無い値の置き場（Yarn Spinner の変数など、アドオンが使う）。
        /// <see cref="Start"/> で空になり、セーブ（<see cref="CaptureState"/>）に含まれる。
        /// </summary>
        public GraphExtras Extras { get; } = new GraphExtras();

        /// <summary>
        /// 最後に読み込みを頼んだシーン（Scene ノードに入ったとき、またはセーブから再開したとき）。無ければ空の参照。
        /// シーンを持たない待機ノードで保存しても、ロードで同じシーンに戻れるようにセーブに含める。
        /// </summary>
        public SceneReference LastScene { get; private set; } = new SceneReference();

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
            Extras.Clear();
            LastScene = new SceneReference();
            _restartState = null;
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
            _automaticCheckQueued = false;
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

        /// <summary>
        /// 今の状態（今いるノード、最後に読み込みを頼んだシーン、パラメータ、<see cref="Extras"/>）をセーブにする。
        /// 今のノードが <see cref="IRestartOnLoad"/> なら、そのノードに入る直前の値で保存する（ロードするとノードの最初からやり直すため）。
        /// </summary>
        /// <exception cref="InvalidOperationException">実行中でない。</exception>
        public GraphRunnerState CaptureState()
        {
            if (!IsRunning || Current == null)
            {
                throw new InvalidOperationException($"Graph '{Graph.name}' is not running, so there is nothing to save.");
            }

            return Current is IRestartOnLoad && _restartState != null && _restartStateEntry == _entryCount
                ? _restartState
                : Snapshot(Current);
        }

        /// <summary>
        /// セーブから再開する。実行中なら今のノードから出て、止まっていれば実行を始め（<see cref="Started"/>）、
        /// パラメータ（同じ名前・型のもの。無ければ既定値）と <see cref="Extras"/> を戻し、保存したシーンを読み込んで、保存したノードに入る。
        /// 保存したノードがグラフに無ければ（グラフが変わった）、何も変えずに false。
        /// ノードの出入りの通知中に呼んだ場合は、その処理の後に行い true を返す。
        /// </summary>
        public bool RestoreState(GraphRunnerState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var node = Query.FindNode(state.NodeId);
            if (node == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(state.GraphName) && state.GraphName != Graph.name)
            {
                Debug.LogWarning($"[VisualNodeEditor] Loading a save of '{state.GraphName}' into '{Graph.name}'.");
            }

            if (_isMoving)
            {
                _pending.Enqueue(() => RestoreNow(state, node));
                return true;
            }

            Run(() => RestoreNow(state, node));
            return true;
        }

        private void RestoreNow(GraphRunnerState state, NodeData node)
        {
            if (!IsRunning)
            {
                _activeBehaviours.Clear();
                IsRunning = true;
                _generation++;
                _running.Add(this);
                Started?.Invoke(this);
            }

            var generation = _generation;
            var previous = Current;
            if (previous != null)
            {
                // 振る舞いの OnExit → NodeExited の順（遷移と同じ）。どちらの中で止められても、それ以上は進めない
                ExitBehaviours();
                if (!IsCurrentGeneration(generation))
                {
                    return;
                }

                NodeExited?.Invoke(previous);
                if (!IsCurrentGeneration(generation))
                {
                    return;
                }

                Current = null;
            }

            // Start() と同じく、振る舞いはアセットの値から複製し直す
            _behaviours.Clear();
            _restartState = null;
            SetParameterValues(state.Parameters);
            Extras.ReplaceWith(state.Extras);
            if (!IsCurrentGeneration(generation))
            {
                return;
            }

            // シーンを持たない待機ノードで保存していても、保存したときのシーンに戻る（Scene ノードなら Enter が読み込む）
            LastScene = state.Scene;
            if (!(node is SceneNode sceneNode && !sceneNode.Scene.IsEmpty) && !LastScene.IsEmpty)
            {
                _sceneLoader?.LoadScene(LastScene);
            }

            if (Enter(node, generation))
            {
                RequestAutomaticCheck();
            }
        }

        // 今の値でセーブを作る（今いるノードを nodeId に）
        private GraphRunnerState Snapshot(NodeData node)
        {
            var parameters = _parameterValues.Select(p => new GraphStateValue(p.Key, p.Value)).ToList();
            return new GraphRunnerState(Graph.name, node.Id, LastScene, parameters, Extras.ToStateValues());
        }

        private bool RaiseNow(string eventName)
        {
            var eventNode = FindRaiseTarget(eventName);
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

        /// <summary>
        /// 今のノードから出ている自動の Event を調べるよう頼む（待機ノードに入り終えたとき・パラメータが変わったとき）。
        /// 遷移の途中なら、その遷移が終わってから調べる。
        /// </summary>
        private void RequestAutomaticCheck()
        {
            if (!IsRunning || Current == null)
            {
                return;
            }

            if (_isMoving)
            {
                if (!_automaticCheckQueued)
                {
                    _automaticCheckQueued = true;
                    _pending.Enqueue(CheckAutomaticNow);
                }

                return;
            }

            Run(CheckAutomaticNow);
        }

        /// <summary>今のノードから出ている自動の Event を、エッジの順に調べ、条件を満たす最初のものへ進む。</summary>
        private void CheckAutomaticNow()
        {
            _automaticCheckQueued = false;
            if (!IsRunning || Current == null)
            {
                return;
            }

            var target = Query.GetAutomaticTransitions(Current.Id).FirstOrDefault(AreConditionsMet);
            if (target == null)
            {
                return;
            }

            // A に入ると B へ、B に入ると A へ… と続く自動遷移は、1 回の操作の中で上限を超えたら止める
            _automaticSteps++;
            if (_automaticSteps > MaxAutomaticSteps)
            {
                if (_automaticSteps == MaxAutomaticSteps + 1)
                {
                    Debug.LogWarning($"[VisualNodeEditor] '{Graph.name}': automatic Events moved more than {MaxAutomaticSteps} times in a row. Stopped at '{Current.Title}'.");
                }

                return;
            }

            MoveTo(target);
        }

        /// <summary>通知中に Raise / Advance が呼ばれても、いま進めている遷移を壊さないよう順番に実行する。</summary>
        private void Run(Action action)
        {
            // 入れ子で呼ばれても、外側の Run が終わるまでは「遷移中」のままにする
            var wasMoving = _isMoving;
            _isMoving = true;
            if (!wasMoving)
            {
                _automaticSteps = 0;
            }

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
                    _automaticCheckQueued = false;
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
                if (Current == null && path.Count > 0 && IsCurrentGeneration(generation) && Enter(path[0], generation))
                {
                    RequestAutomaticCheck();
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

            // 待機ノードに入り終えたら、そこから出ている自動の Event を調べる（この遷移の後に）
            RequestAutomaticCheck();
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

            // ロードするとノードの最初からやり直すノードでは、入る直前（OnEnter より前）の状態を覚えておき、ここにいる間のセーブに使う
            if (node is IRestartOnLoad)
            {
                _restartState = Snapshot(node);
                _restartStateEntry = _entryCount + 1;
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
                    // Reload: このグラフがもうこのシーンにいれば、読み込み直す（開始したときは LastScene が空なので、Play を押したシーンを読み直さない）
                    var reload = sceneNode.Reload && LastScene.IsSameSceneAs(sceneNode.Scene);
                    LastScene = sceneNode.Scene;
                    if (reload)
                    {
                        _sceneLoader?.ReloadScene(sceneNode.Scene);
                    }
                    else
                    {
                        _sceneLoader?.LoadScene(sceneNode.Scene);
                    }

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
