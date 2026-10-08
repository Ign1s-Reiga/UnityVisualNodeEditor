using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// コンテナの中の開始点。コンテナごとにちょうど 1 つ置き、ルート階層には置かない。
    /// コンテナと一緒にエディタが自動で作るので Create Node メニューには出さない（<see cref="NodeMenuAttribute.Hidden"/>）。
    /// ルート階層の開始点は <see cref="EntryNode"/>（別の型）。
    /// </summary>
    [Serializable]
    [NodeMenu("Container/Entry", Hidden = true)]
    public sealed class ContainerEntryNode : NodeData
    {
        protected override string DefaultTitle => "Entry";
    }
}
