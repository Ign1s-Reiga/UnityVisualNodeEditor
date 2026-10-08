using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Behaviours
{
    /// <summary>ノードに追加できる <see cref="NodeBehaviour"/> の型と、そのスクリプトを探す。</summary>
    public static class NodeBehaviourCatalog
    {
        /// <summary>
        /// Add Behaviour メニューに出す型（<see cref="IsListed"/>）を、メニューのパス順で返す。
        /// </summary>
        public static List<Type> GetTypes() =>
            TypeCache.GetTypesDerivedFrom<NodeBehaviour>()
                .Where(IsListed)
                .OrderBy(GetMenuPath, StringComparer.Ordinal)
                .ToList();

        /// <summary>ノードに付けられる型か（抽象・ジェネリック・引数なしのコンストラクタが無いものは不可）。</summary>
        public static bool IsCreatable(Type type) =>
            type != null
            && typeof(NodeBehaviour).IsAssignableFrom(type)
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && type.GetConstructor(Type.EmptyTypes) != null;

        /// <summary>
        /// Add Behaviour メニューに出すか。付けられる public な型だけを出す（internal なテスト用の型などは出さない）。
        /// </summary>
        public static bool IsListed(Type type) => IsCreatable(type) && type.IsVisible;

        /// <summary><c>[Serializable]</c> が付いているか。無いとグラフアセットに保存できない。</summary>
        public static bool IsSaveable(Type type) => type != null && type.IsSerializable;

        /// <summary>メニューのパス。名前空間を階層にする（例: "MyGame/Stage/PlayerMovement"）。</summary>
        public static string GetMenuPath(Type type) =>
            string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace.Replace('.', '/') + "/" + type.Name;

        /// <summary>型を定義しているスクリプト。ファイル名がクラス名と違うなどで見つからなければ null。</summary>
        public static MonoScript FindScript(Type type) =>
            type == null
                ? null
                : MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(script => script != null && script.GetClass() == type);
    }
}
