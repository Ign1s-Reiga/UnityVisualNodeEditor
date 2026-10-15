using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// ノードの画面: グラフの今の場所で出す画面の並び（<see cref="GraphScreens"/>）と、それを重ねて出す <see cref="ScreenStack"/>。
    /// Root: Entry → Title(画面 T) ─Start→ Round(画面 H)
    ///   Round: Entry → Play(画面なし) ─Pause→ Paused(画面 P) ─Resume→ Play
    /// </summary>
    public sealed class NodeScreenTests
    {
        private NodeGraphAsset _graph;
        private VisualTreeAsset _title;
        private VisualTreeAsset _hud;
        private VisualTreeAsset _paused;
        private StateNode _titleNode;
        private ContainerNode _round;
        private StateNode _pausedNode;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _title = CreateScreen("Title");
            _hud = CreateScreen("HUD");
            _paused = CreateScreen("Paused");

            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var entry = Add(new EntryNode(), string.Empty);
            _titleNode = Add(new StateNode { Title = "Title", Screen = _title }, string.Empty);
            _round = Add(new ContainerNode { Title = "Round", Screen = _hud }, string.Empty);
            var start = Add(new EventNode { EventName = "Start" }, string.Empty);
            Connect(entry, "out", _titleNode);
            Connect(_titleNode, "out", start);
            Connect(start, "out", _round);

            var roundEntry = Add(new ContainerEntryNode(), _round.Id);
            var play = Add(new StateNode { Title = "Play" }, _round.Id);
            _pausedNode = Add(new StateNode { Title = "Paused", Screen = _paused }, _round.Id);
            var pause = Add(new EventNode { EventName = "Pause" }, _round.Id);
            var resume = Add(new EventNode { EventName = "Resume" }, _round.Id);
            Connect(roundEntry, "out", play);
            Connect(play, "out", pause);
            Connect(pause, "out", _pausedNode);
            Connect(_pausedNode, "out", resume);
            Connect(resume, "out", play);

            _runner = new GraphRunner(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_graph);
            Object.DestroyImmediate(_title);
            Object.DestroyImmediate(_hud);
            Object.DestroyImmediate(_paused);
        }

        [Test]
        public void At_IsEmpty_WithoutARunningRunner()
        {
            Assert.That(GraphScreens.At(null), Is.Empty);
            Assert.That(GraphScreens.At(_runner), Is.Empty, "not started");
        }

        [Test]
        public void At_StacksTheContainersUnderTheNode()
        {
            _runner.Start();
            Assert.That(GraphScreens.At(_runner), Is.EqualTo(new[] { _title }));

            _runner.Raise("Start");
            Assert.That(GraphScreens.At(_runner), Is.EqualTo(new[] { _hud }), "Play has no screen: only the container's");

            _runner.Raise("Pause");
            Assert.That(GraphScreens.At(_runner), Is.EqualTo(new[] { _hud, _paused }), "the node's screen on top");
        }

        [Test]
        public void At_ShowsAScreenOnce()
        {
            _pausedNode.Screen = _hud;
            _runner.Start();
            _runner.Raise("Start");
            _runner.Raise("Pause");

            Assert.That(GraphScreens.At(_runner), Is.EqualTo(new[] { _hud }));
        }

        [Test]
        public void AnyIn_IsTrueOnlyWithAScreen()
        {
            Assert.That(GraphScreens.AnyIn(_graph), Is.True);

            _titleNode.Screen = null;
            _round.Screen = null;
            _pausedNode.Screen = null;
            Assert.That(GraphScreens.AnyIn(_graph), Is.False);
            Assert.That(GraphScreens.AnyIn(null), Is.False);
        }

        [Test]
        public void Show_StacksScreensOverWhatTheRootHas()
        {
            var root = new VisualElement();
            var own = new VisualElement();
            root.Add(own);
            var stack = new ScreenStack();

            stack.Show(root, new[] { _hud, null, _paused, _hud });

            Assert.That(stack.Shown, Is.EqualTo(new[] { _hud, _paused }), "nulls and repeats are skipped");
            Assert.That(root.Children(), Is.EqualTo(new[] { own, stack.InstanceOf(_hud), stack.InstanceOf(_paused) }),
                "the document's own screen stays at the bottom");
            var hud = stack.InstanceOf(_hud);
            Assert.That(hud.pickingMode, Is.EqualTo(PickingMode.Ignore), "empty parts let clicks through to the screens under it");
            Assert.That(hud.ClassListContains(ScreenStack.ScreenUssClassName), Is.True);
            Assert.That(stack.IsShownIn(root), Is.True);
            Assert.That(stack.IsShownIn(new VisualElement()), Is.False);
        }

        [Test]
        public void Show_KeepsTheSameElementForAScreen()
        {
            var root = new VisualElement();
            var stack = new ScreenStack();
            stack.Show(root, new[] { _hud, _paused });
            var hud = stack.InstanceOf(_hud);
            var paused = stack.InstanceOf(_paused);

            stack.Show(root, new[] { _hud });
            Assert.That(root.Children(), Is.EqualTo(new[] { hud }));
            Assert.That(paused.parent, Is.Null, "taken off, not destroyed");

            stack.Show(root, new[] { _title, _hud, _paused });
            Assert.That(root.Children(), Is.EqualTo(new[] { stack.InstanceOf(_title), hud, paused }), "made once, reused in the new order");

            stack.Clear();
            Assert.That(root.childCount, Is.Zero);
            Assert.That(stack.Shown, Is.Empty);
            Assert.That(stack.IsShownIn(root), Is.True, "nothing to show");
        }

        [Test]
        public void Show_PutsTheScreensBackOnTop()
        {
            // UIDocument が根を作り直したり、別の物が上に足されたりしたら、出し直すと画面が一番上に戻る
            var root = new VisualElement();
            var stack = new ScreenStack();
            stack.Show(root, new[] { _hud });
            var other = new VisualElement();
            root.Add(other);

            stack.Show(root, new[] { _hud });

            Assert.That(root.Children(), Is.EqualTo(new[] { other, stack.InstanceOf(_hud) }));
        }

        [Test]
        public void Show_LeavesTheScreensUnderneathAttached_WhenAnOverlayComesAndGoes()
        {
            // 外して付け直すと、中のコントロールが聞き直し、フォーカスも外れる。Paused を重ねる・外すだけなら、Game の画面は付けたまま
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var gameObject = new GameObject("Screens");
            try
            {
                var document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = settings;
                var root = document.rootVisualElement;
                Assume.That(root?.panel, Is.Not.Null, "the UI Document has a panel in edit mode");
                var stack = new ScreenStack();
                stack.Show(root, new[] { _hud });
                var hud = stack.InstanceOf(_hud);
                var detached = 0;
                hud.RegisterCallback<DetachFromPanelEvent>(_ => detached++);

                stack.Show(root, new[] { _hud, _paused });
                stack.Show(root, new[] { _hud, _title });
                stack.Show(root, new[] { _hud });

                Assert.That(detached, Is.Zero);
                Assert.That(stack.InstanceOf(_paused).panel, Is.Null, "the overlays are taken off");
                Assert.That(stack.InstanceOf(_title).panel, Is.Null);
                Assert.That(stack.Shown, Is.EqualTo(new[] { _hud }));

                stack.Show(root, new[] { _title });
                Assert.That(detached, Is.EqualTo(1), "a screen that is no longer wanted is taken off");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Show_PutsBackOnlyFromTheFirstChange()
        {
            var root = new VisualElement();
            var stack = new ScreenStack();
            stack.Show(root, new[] { _title, _hud, _paused });

            stack.Show(root, new[] { _title, _paused, _hud });

            Assert.That(root.Children(), Is.EqualTo(new[] { stack.InstanceOf(_title), stack.InstanceOf(_paused), stack.InstanceOf(_hud) }));
            Assert.That(stack.Shown, Is.EqualTo(new[] { _title, _paused, _hud }));
        }

        [Test]
        public void TheHost_KeepsTrying_UntilTheUIDocumentHasARoot()
        {
            // UIDocument の根が無い（Panel Settings が無い・無効）間に場所が替わっても、根ができたら出す
            var gameObject = new GameObject("Host");
            try
            {
                var host = gameObject.AddComponent<GraphScreenHost>();

                host.RefreshIn(null);
                Assert.That(host.NeedsRefresh, Is.True, "tried again next frame");

                host.RefreshIn(new VisualElement());
                Assert.That(host.NeedsRefresh, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static VisualTreeAsset CreateScreen(string name)
        {
            var screen = ScriptableObject.CreateInstance<VisualTreeAsset>();
            screen.name = name;
            return screen;
        }

        private T Add<T>(T node, string parentId) where T : NodeData
        {
            node.ParentId = parentId;
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, string port, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, port, to.Id, "in"));
    }
}
