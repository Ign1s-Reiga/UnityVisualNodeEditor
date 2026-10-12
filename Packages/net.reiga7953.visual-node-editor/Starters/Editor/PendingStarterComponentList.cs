using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary><see cref="PendingStarterComponent"/> の一覧（JsonUtility はリストをそのまま書けないので包む）。</summary>
    [Serializable]
    internal sealed class PendingStarterComponentList
    {
        [SerializeField] private List<PendingStarterComponent> _items = new List<PendingStarterComponent>();

        /// <summary>付ける予定（古いものから）。</summary>
        public List<PendingStarterComponent> Items => _items;
    }
}
