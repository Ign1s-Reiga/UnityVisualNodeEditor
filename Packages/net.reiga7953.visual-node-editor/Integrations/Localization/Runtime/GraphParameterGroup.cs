using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Reiga.VisualNodeEditor.Localization
{
    /// <summary>
    /// グラフのパラメータ（Blackboard）全部を、Unity Localization の Smart Strings で読む変数のまとまり。
    /// Localized String の Local Variables（または Global Variables Group）に <c>graph</c> などの名前で足し、文字列に <c>{graph.Score}</c> と書く。
    /// <para>
    /// 名前ごとに <see cref="GraphParameterVariable"/> を返すので、値が変わる・Runner が始まる / 止まると文字列が作り直される。
    /// グラフを指定していれば、そのグラフに無い名前は返さない（Smart Strings の書式エラーになる）。
    /// </para>
    /// </summary>
    [Serializable]
    [DisplayName("Graph Parameters")]
    public sealed class GraphParameterGroup : IVariableGroup, IVariable
    {
        [Tooltip("The graph whose running Graph Runner gives the values (the start values while none runs). Empty: any running graph.")]
        [SerializeField] private NodeGraphAsset _graph;

        [NonSerialized] private Dictionary<string, GraphParameterVariable> _variables;

        /// <summary>空のまとまり（Local Variables の追加のメニューから作るとき）。</summary>
        public GraphParameterGroup()
        {
        }

        /// <summary><paramref name="graph"/> のパラメータを読むまとまり。</summary>
        /// <param name="graph">値を読むグラフ。null なら実行中のどれか 1 つ。</param>
        public GraphParameterGroup(NodeGraphAsset graph)
        {
            _graph = graph;
        }

        /// <summary>値を読むグラフ。null なら実行中のどれか 1 つ。替えると、使っている文字列を作り直させる。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                if (_variables == null)
                {
                    return;
                }

                foreach (var variable in _variables.Values)
                {
                    variable.Graph = value;
                }
            }
        }

        /// <inheritdoc/>
        public object GetSourceValue(ISelectorInfo selector) => this;

        /// <summary>
        /// その名前のパラメータの変数（同じ名前には同じもの）。グラフを指定していて、そのグラフに無い名前なら false。
        /// グラフが空なら、どの名前でも返す（Runner が始まるまで、あるか分からないため）。
        /// </summary>
        public bool TryGetValue(string key, out IVariable value)
        {
            value = null;
            if (string.IsNullOrEmpty(key) || (_graph != null && _graph.FindParameterByName(key) == null))
            {
                return false;
            }

            _variables ??= new Dictionary<string, GraphParameterVariable>();
            if (_variables.TryGetValue(key, out var variable))
            {
                // インスペクタでグラフを替えた（セッターを通らない）ときは、ここで合わせる。文字列を作っている最中なので知らせない
                if (variable.Graph != _graph)
                {
                    variable.UseGraph(_graph);
                }
            }
            else
            {
                variable = new GraphParameterVariable(_graph, key);
                _variables.Add(key, variable);
            }

            value = variable;
            return true;
        }
    }
}
