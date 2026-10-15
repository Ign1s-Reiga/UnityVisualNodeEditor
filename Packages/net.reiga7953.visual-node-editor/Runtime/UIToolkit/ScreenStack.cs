using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 画面（UXML）を 1 つの根の要素に重ねて出す（<see cref="GraphScreenHost"/> が UIDocument の根に使う）。
    /// 画面は UXML から一度だけ作って覚え、同じ画面はずっと同じ要素を使う（HUD の文字・盤の中身が残る）。外した画面は根から外すだけ。
    /// 根にある、ここで作ったもの以外の要素（UIDocument 自身の画面など）には触らず、その上に重ねる。
    /// </summary>
    public sealed class ScreenStack
    {
        /// <summary>重ねた画面の根の要素の USS クラス。</summary>
        public const string ScreenUssClassName = "vne-screen";

        private readonly Dictionary<VisualTreeAsset, TemplateContainer> _instances = new Dictionary<VisualTreeAsset, TemplateContainer>();
        private readonly List<VisualTreeAsset> _shown = new List<VisualTreeAsset>();

        /// <summary>今出している画面（下から上の順）。</summary>
        public IReadOnlyList<VisualTreeAsset> Shown => _shown;

        /// <summary><paramref name="screen"/> から作った要素（まだ作っていなければ null）。</summary>
        public TemplateContainer InstanceOf(VisualTreeAsset screen) =>
            screen != null && _instances.TryGetValue(screen, out var instance) ? instance : null;

        /// <summary>
        /// <paramref name="root"/> に <paramref name="screens"/> を下から上の順に重ねる（null と 2 度目のものは飛ばす）。
        /// 外して付け直すと中のコントロールが聞き直し、フォーカスも外れるので、今の並びと下から同じところまでは付けたままにし、
        /// 違うところから上だけを付け直す（並びがすべて同じなら何もしない）。根の一番上に今の並びがそろっていなければ
        /// （UIDocument が根を作り直した・ほかの要素が上に足された）、ここで作った要素を一度外して並べ直す。
        /// </summary>
        public void Show(VisualElement root, IReadOnlyList<VisualTreeAsset> screens)
        {
            if (root == null)
            {
                return;
            }

            var wanted = new List<VisualTreeAsset>();
            foreach (var screen in screens)
            {
                if (screen != null && !wanted.Contains(screen))
                {
                    wanted.Add(screen);
                }
            }

            var keep = 0;
            if (IsStackedOnTop(root))
            {
                while (keep < wanted.Count && keep < _shown.Count && wanted[keep] == _shown[keep])
                {
                    keep++;
                }

                if (keep == wanted.Count && keep == _shown.Count)
                {
                    return;
                }
            }
            else
            {
                foreach (var instance in _instances.Values)
                {
                    instance.RemoveFromHierarchy();
                }

                _shown.Clear();
            }

            // 違うところから上を外して、欲しい並びで付け直す
            for (var i = _shown.Count - 1; i >= keep; i--)
            {
                InstanceOf(_shown[i])?.RemoveFromHierarchy();
                _shown.RemoveAt(i);
            }

            for (var i = keep; i < wanted.Count; i++)
            {
                root.Add(GetOrCreate(wanted[i]));
                _shown.Add(wanted[i]);
            }
        }

        /// <summary>出している画面が、どれも <paramref name="root"/> に付いているか（何も出していなければ true）。</summary>
        public bool IsShownIn(VisualElement root)
        {
            foreach (var screen in _shown)
            {
                if (root == null || InstanceOf(screen)?.parent != root)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>出している画面をすべて外す（作った要素は覚えたまま）。</summary>
        public void Clear()
        {
            foreach (var instance in _instances.Values)
            {
                instance.RemoveFromHierarchy();
            }

            _shown.Clear();
        }

        // 今出している画面が、出した順のまま、その根の一番上に並んでいるか（UIDocument が根を作り直したら、並んでいない）
        private bool IsStackedOnTop(VisualElement root)
        {
            if (root.childCount < _shown.Count)
            {
                return false;
            }

            var first = root.childCount - _shown.Count;
            for (var i = 0; i < _shown.Count; i++)
            {
                if (root[first + i] != InstanceOf(_shown[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // 画面の要素は根いっぱいに広げ、クリックは中の要素だけが受ける（上の画面の空いた所から、下の画面のボタンを押せるように）
        private TemplateContainer GetOrCreate(VisualTreeAsset screen)
        {
            if (_instances.TryGetValue(screen, out var instance))
            {
                return instance;
            }

            instance = screen.Instantiate();
            instance.name = screen.name;
            instance.AddToClassList(ScreenUssClassName);
            instance.pickingMode = PickingMode.Ignore;
            instance.style.position = Position.Absolute;
            instance.style.left = 0f;
            instance.style.top = 0f;
            instance.style.right = 0f;
            instance.style.bottom = 0f;
            _instances.Add(screen, instance);
            return instance;
        }
    }
}
