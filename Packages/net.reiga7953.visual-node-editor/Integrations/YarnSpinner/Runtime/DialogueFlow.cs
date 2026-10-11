using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// グラフの Runner と会話（<see cref="IDialoguePlayer"/>）を合わせる判定。Yarn に依存しない純粋な部分で、<see cref="YarnGraphBridge"/> が毎フレーム <see cref="Tick"/> する。
    /// <list type="bullet">
    /// <item>Runner が Dialogue ノードに入ったら、その Yarn ノードで会話を始める（流している会話があれば先に止め、止まってから始める）</item>
    /// <item>会話が終わったら、まだその Dialogue ノードにいれば、<c>graph_next</c> の名前 → 完了イベント → Advance の順に試して次へ進める</item>
    /// <item>Dialogue ノードを離れたら（別のノードへ遷移・Runner の停止）、流している会話を止める</item>
    /// </list>
    /// 同じ Dialogue ノードに入り直したときも会話をやり直せるよう、入るたびに番号を振って見分ける。
    /// </summary>
    internal sealed class DialogueFlow
    {
        private readonly IDialoguePlayer _player;
        private int _tokens;

        // 今のノードが Dialogue ノードなら、その入りの番号（0 なら会話は要らない）
        private int _wantedToken;
        private DialogueNode _wantedNode;

        // 流している会話の番号（0 なら流していない）と、止めるよう頼んだか
        private int _runningToken;
        private bool _stopping;

        // 終わった会話の番号（次の Tick で次へ進める）と、次へ進めようとした会話の番号（進めなくても、同じ入りでは流し直さない）
        private int _completedToken;
        private int _finishedToken;

        // 会話の中の <<graph_next 名前>>
        private string _nextEvent;

        public DialogueFlow(IDialoguePlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>繋いでいる Runner。無ければ null。</summary>
        public GraphRunner Runner { get; private set; }

        /// <summary>Dialogue ノードに入るたびに保存するスロット。空なら保存しない。</summary>
        public string AutoSaveSlot { get; set; }

        /// <summary>警告の出し先（テストで差し替える）。</summary>
        public Action<string> Warn { get; set; } = message => Debug.LogWarning("[VisualNodeEditor] " + message);

        /// <summary>
        /// <paramref name="runner"/> に繋ぐ（null なら外すだけ）。Runner がすでに Dialogue ノードにいれば、その会話を流す
        /// （シーンの読み込みが終わる前に Dialogue ノードへ進んでいた場合）。
        /// </summary>
        public void Attach(GraphRunner runner)
        {
            if (runner == Runner)
            {
                return;
            }

            Detach();
            Runner = runner;
            if (runner == null)
            {
                return;
            }

            runner.NodeEntered += OnNodeEntered;
            if (runner.IsRunning && runner.Current is DialogueNode node)
            {
                Want(node);
            }
        }

        /// <summary>Runner から外す。流している会話は次の <see cref="Tick"/> で止める。</summary>
        public void Detach()
        {
            if (Runner != null)
            {
                Runner.NodeEntered -= OnNodeEntered;
            }

            Runner = null;
            _wantedToken = 0;
            _wantedNode = null;
            _completedToken = 0;
            _nextEvent = null;
        }

        /// <summary>
        /// Runner から外し、流している会話を止めて、その会話のことを忘れる（<see cref="YarnGraphBridge"/> が無効になるとき）。
        /// 止まったことの知らせはもう受け取らないので、待たずに「流していない」状態に戻す（また有効になったら、すぐ次の会話を始められる）。
        /// </summary>
        public void Shutdown()
        {
            Detach();
            if (_runningToken != 0 && !_stopping)
            {
                _player.Stop();
            }

            _runningToken = 0;
            _stopping = false;
            _finishedToken = 0;
        }

        /// <summary>会話が終わった（Yarn の <c>onDialogueComplete</c>。止めたときも呼ばれる）。</summary>
        public void OnDialogueCompleted()
        {
            if (_stopping)
            {
                // こちらから止めたことによる終わり。次へは進めない
                _stopping = false;
                _runningToken = 0;
                return;
            }

            if (_runningToken != 0)
            {
                _completedToken = _runningToken;
                _runningToken = 0;
            }
        }

        /// <summary><c>&lt;&lt;graph_next 名前&gt;&gt;</c>: 会話が終わったときに Raise する名前を決める。</summary>
        public void SetNext(string eventName) => _nextEvent = eventName;

        /// <summary><c>&lt;&lt;graph_event 名前&gt;&gt;</c>: すぐ Raise する。遷移が無ければ警告を出して false。</summary>
        public bool RaiseNow(string eventName)
        {
            if (Runner == null || !Runner.IsRunning)
            {
                Warn($"<<graph_event {eventName}>>: no Graph Runner is running, so nothing happened.");
                return false;
            }

            if (Runner.Raise(eventName))
            {
                return true;
            }

            Warn($"<<graph_event {eventName}>>: '{eventName}' does not lead anywhere from '{Runner.Current?.Title}' (or its conditions do not hold).");
            return false;
        }

        /// <summary>終わった会話を次へ進め、流している会話を今のノードに合わせる。毎フレーム呼ぶ。</summary>
        public void Tick()
        {
            if (_completedToken != 0)
            {
                var token = _completedToken;
                _completedToken = 0;
                if (token == _wantedToken && Runner != null && Runner.IsRunning)
                {
                    _finishedToken = token;
                    Finish(_wantedNode);
                }
            }

            if (_runningToken == _wantedToken)
            {
                return;
            }

            // 別のノードへ移ったのに前の会話が流れていれば、止まるのを待ってから次を始める
            if (_runningToken != 0)
            {
                if (!_stopping)
                {
                    _stopping = true;
                    _player.Stop();
                }

                return;
            }

            if (_wantedToken != 0 && _wantedToken != _finishedToken)
            {
                Start(_wantedNode, _wantedToken);
            }
        }

        private void OnNodeEntered(NodeData node)
        {
            _nextEvent = null;
            if (node is DialogueNode dialogue)
            {
                Want(dialogue);
                AutoSave();
            }
            else
            {
                _wantedToken = 0;
                _wantedNode = null;
            }
        }

        private void Want(DialogueNode node)
        {
            _wantedToken = ++_tokens;
            _wantedNode = node;
        }

        private void Start(DialogueNode node, int token)
        {
            if (string.IsNullOrWhiteSpace(node.YarnNode))
            {
                Warn($"'{node.Title}' has no Yarn node, so it shows no dialogue.");
                _finishedToken = token;
                return;
            }

            // 始められなければ（Yarn にそのノードが無いなど）終わりの知らせは来ないので、待たずに次へ進める（その Dialogue ノードで止まらないように）
            var yarnNode = node.YarnNode.Trim();
            _runningToken = token;
            if (!_player.Start(yarnNode))
            {
                _runningToken = 0;
                _finishedToken = token;
                Warn($"'{node.Title}' could not start the Yarn node '{yarnNode}' (the Dialogue Runner has no Yarn Project, " +
                     "or the project has no node with that name). Moved on as if the dialogue had finished.");
                Finish(node);
            }
        }

        // graph_next の名前 → 完了イベント → Advance の順に試す
        private void Finish(DialogueNode node)
        {
            var next = _nextEvent;
            _nextEvent = null;
            if (!string.IsNullOrEmpty(next))
            {
                if (Runner.Raise(next))
                {
                    return;
                }

                Warn($"<<graph_next {next}>>: '{next}' does not lead anywhere from '{node.Title}' (or its conditions do not hold).");
            }

            if (!string.IsNullOrEmpty(node.CompleteEvent) && Runner.Raise(node.CompleteEvent))
            {
                return;
            }

            if (Runner.Advance())
            {
                return;
            }

            Warn($"The dialogue of '{node.Title}' finished, but nothing leads on from it: " +
                 $"no '{node.CompleteEvent}' Event whose conditions hold, and no next node.");
        }

        // Dialogue ノードに入るたびに保存する（入る前の値で保存される。IRestartOnLoad）
        private void AutoSave()
        {
            if (string.IsNullOrEmpty(AutoSaveSlot) || Runner == null)
            {
                return;
            }

            if (!GraphSaveSlots.IsValidSlotName(AutoSaveSlot))
            {
                Warn($"'{AutoSaveSlot}' is not a usable slot name, so nothing was auto-saved. Use letters, digits, spaces, '-' and '_'.");
                return;
            }

            try
            {
                GraphSaveSlots.Save(Runner, AutoSaveSlot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
