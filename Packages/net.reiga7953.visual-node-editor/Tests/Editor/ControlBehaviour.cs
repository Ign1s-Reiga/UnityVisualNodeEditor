using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の振る舞い。設定に応じて、振る舞いの中から Runner を操作する。</summary>
    [Serializable]
    internal sealed class ControlBehaviour : NodeBehaviour
    {
        [SerializeField] private string _raiseOnUpdate;
        [SerializeField] private bool _stopOnEnter;
        [SerializeField] private bool _stopOnExit;

        public string RaiseOnUpdate
        {
            get => _raiseOnUpdate;
            set => _raiseOnUpdate = value;
        }

        public bool StopOnEnter
        {
            get => _stopOnEnter;
            set => _stopOnEnter = value;
        }

        public bool StopOnExit
        {
            get => _stopOnExit;
            set => _stopOnExit = value;
        }

        public override void OnEnter()
        {
            if (_stopOnEnter)
            {
                Runner.Stop();
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!string.IsNullOrEmpty(_raiseOnUpdate))
            {
                Runner.Raise(_raiseOnUpdate);
            }
        }

        public override void OnExit()
        {
            if (_stopOnExit)
            {
                Runner.Stop();
            }
        }
    }
}
