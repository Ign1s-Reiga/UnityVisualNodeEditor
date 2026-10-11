using System;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// グラフの Runner と Yarn Spinner の <see cref="DialogueRunner"/> を繋ぐコンポーネント（DialogueRunner と同じ GameObject に付ける）。
    /// <list type="bullet">
    /// <item>Runner が <see cref="DialogueNode"/> に入ったら、その Yarn ノードで会話を始め、終わったら次へ進める（完了イベント → Advance）</item>
    /// <item>Dialogue ノードを離れたら、流している会話を止める</item>
    /// <item>Yarn のコマンド <c>&lt;&lt;graph_event 名前&gt;&gt;</c>（すぐ Raise）と <c>&lt;&lt;graph_next 名前&gt;&gt;</c>（会話が終わったときに Raise）を登録する</item>
    /// </list>
    /// Runner は <see cref="GraphRunner.Running"/> から探すので、Graph Runner への参照は要らない（別のシーンに残っている Runner にも繋がる）。
    /// Yarn の変数をグラフの Blackboard と共有するには、DialogueRunner の Variable Storage に <see cref="GraphVariableStorage"/> を設定する
    /// （エディタでこのコンポーネントを足すと自動で設定される）。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Yarn Graph Bridge")]
    [DisallowMultipleComponent]
    public sealed class YarnGraphBridge : MonoBehaviour
    {
        /// <summary>すぐ Raise する Yarn のコマンド名（<c>&lt;&lt;graph_event 名前&gt;&gt;</c>）。</summary>
        public const string EventCommand = "graph_event";

        /// <summary>会話が終わったときに Raise する名前を決める Yarn のコマンド名（<c>&lt;&lt;graph_next 名前&gt;&gt;</c>）。</summary>
        public const string NextCommand = "graph_next";

        [Tooltip("会話を流す Dialogue Runner。空なら同じ GameObject のもの。")]
        [SerializeField] private DialogueRunner _dialogueRunner;

        [Tooltip("このグラフを実行中の Graph Runner に繋ぐ。空なら実行中のどれか。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Dialogue ノードに入るたびに保存するスロット（空なら保存しない）。タイトル画面の「続きから」用。")]
        [SerializeField] private string _autoSaveSlot = string.Empty;

        private DialogueFlow _flow;
        private DialogueRunner _hooked;

        /// <summary>会話を流す Dialogue Runner。</summary>
        public DialogueRunner DialogueRunner => _dialogueRunner;

        /// <summary>繋ぐグラフ。null なら実行中のどれか。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>Dialogue ノードに入るたびに保存するスロット。空なら保存しない。</summary>
        public string AutoSaveSlot
        {
            get => _autoSaveSlot;
            set
            {
                _autoSaveSlot = value ?? string.Empty;
                if (_flow != null)
                {
                    _flow.AutoSaveSlot = _autoSaveSlot;
                }
            }
        }

        /// <summary>繋いでいる Runner。無ければ null。</summary>
        public GraphRunner Runner => _flow?.Runner;

        private void OnEnable()
        {
            if (_dialogueRunner == null)
            {
                _dialogueRunner = GetComponent<DialogueRunner>();
            }

            if (_dialogueRunner == null)
            {
                Debug.LogWarning($"[VisualNodeEditor] '{name}': Yarn Graph Bridge needs a Dialogue Runner (on this GameObject or assigned).", this);
                return;
            }

            _flow ??= new DialogueFlow(new DialogueRunnerPlayer(_dialogueRunner));
            _flow.AutoSaveSlot = _autoSaveSlot;
            Hook(_dialogueRunner);
            GraphRunner.Started += OnRunnerStarted;
            GraphRunner.Stopped += OnRunnerStopped;
            _flow.Attach(GraphRunner.FindRunning(_graph));
        }

        private void OnDisable()
        {
            GraphRunner.Started -= OnRunnerStarted;
            GraphRunner.Stopped -= OnRunnerStopped;
            if (_flow != null)
            {
                // 流している会話も止める（外した後の Tick で止まる）
                _flow.Detach();
                _flow.Tick();
            }

            Unhook();
        }

        private void Update() => _flow?.Tick();

        private void Hook(DialogueRunner runner)
        {
            Unhook();
            _hooked = runner;
            runner.onDialogueComplete ??= new UnityEvent();
            runner.onDialogueComplete.AddListener(OnDialogueComplete);
            runner.AddCommandHandler(EventCommand, (Action<string>)OnEventCommand);
            runner.AddCommandHandler(NextCommand, (Action<string>)OnNextCommand);
        }

        private void Unhook()
        {
            if (_hooked == null)
            {
                return;
            }

            _hooked.onDialogueComplete?.RemoveListener(OnDialogueComplete);
            _hooked.RemoveCommandHandler(EventCommand);
            _hooked.RemoveCommandHandler(NextCommand);
            _hooked = null;
        }

        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_flow.Runner == null && (_graph == null || runner.Graph == _graph))
            {
                _flow.Attach(runner);
            }
        }

        private void OnRunnerStopped(GraphRunner runner)
        {
            if (_flow.Runner == runner)
            {
                _flow.Attach(GraphRunner.FindRunning(_graph));
            }
        }

        private void OnDialogueComplete() => _flow?.OnDialogueCompleted();

        private void OnEventCommand(string eventName) => _flow?.RaiseNow(eventName);

        private void OnNextCommand(string eventName) => _flow?.SetNext(eventName);
    }
}
