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
        /// <paramref name="root"/> に <paramref name="screens"/> を下から上の順に重ねる。もう出ている画面で、並びも同じなら触らない
        /// （外して付け直すと、中のコントロールが聞き直すため）。並びが違えば、ここで作った要素を一度外して並べ直す。
        /// </summary>
        public void Show(VisualElement root, IReadOnlyList<VisualTreeAsset> screens)
        {
            if (root == null)
            {
                return;
            }

            if (IsShowing(root, screens))
            {
                return;
            }

            foreach (var instance in _instances.Values)
            {
                instance.RemoveFromHierarchy();
            }

            _shown.Clear();
            foreach (var screen in screens)
            {
                if (screen == null || _shown.Contains(screen))
                {
                    continue;
                }

                root.Add(GetOrCreate(screen));
                _shown.Add(screen);
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

        // 同じ画面が同じ並びで、今もその根の一番上に並んでいるか（UIDocument が根を作り直したら、並んでいない）
        private bool IsShowing(VisualElement root, IReadOnlyList<VisualTreeAsset> screens)
        {
            var wanted = new List<VisualTreeAsset>();
            foreach (var screen in screens)
            {
                if (screen != null && !wanted.Contains(screen))
                {
                    wanted.Add(screen);
                }
            }

            if (wanted.Count != _shown.Count || root.childCount < wanted.Count)
            {
                return false;
            }

            var first = root.childCount - wanted.Count;
            for (var i = 0; i < wanted.Count; i++)
            {
                if (wanted[i] != _shown[i] || root[first + i] != InstanceOf(wanted[i]))
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
