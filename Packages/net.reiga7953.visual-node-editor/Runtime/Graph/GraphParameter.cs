using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフ単位のパラメータ（Blackboard の 1 項目）。名前はグラフ内で一意にする。
    /// ノードの処理を書くためのものではなく、ゲームから参照・更新する設定値・状態の置き場。
    /// 実行時の値は <see cref="GraphRunner"/> が持ち、ここに書かれているのは既定値。
    /// </summary>
    [Serializable]
    public sealed class GraphParameter
    {
        [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _name;
        [SerializeField] private GraphParameterType _type;
        [SerializeField] private bool _boolValue;
        [SerializeField] private int _intValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private string _stringValue = string.Empty;

        public GraphParameter()
        {
        }

        public GraphParameter(string name, GraphParameterType type)
        {
            _name = name;
            _type = type;
        }

        /// <summary>グラフ内で一意な ID（名前を変えても変わらない）。</summary>
        public string Id => _id;

        /// <summary>名前。<see cref="GraphRunner"/> の Get / Set で指定する。</summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>型。作成後は変えない。</summary>
        public GraphParameterType Type => _type;

        /// <summary>Bool 型の既定値。</summary>
        public bool BoolValue
        {
            get => _boolValue;
            set => _boolValue = value;
        }

        /// <summary>Int 型の既定値。</summary>
        public int IntValue
        {
            get => _intValue;
            set => _intValue = value;
        }

        /// <summary>Float 型の既定値。</summary>
        public float FloatValue
        {
            get => _floatValue;
            set => _floatValue = value;
        }

        /// <summary>String 型の既定値。</summary>
        public string StringValue
        {
            get => _stringValue;
            set => _stringValue = value ?? string.Empty;
        }

        /// <summary>型に応じた既定値（ボックス化される）。</summary>
        public object DefaultValue => _type switch
        {
            GraphParameterType.Bool => _boolValue,
            GraphParameterType.Int => _intValue,
            GraphParameterType.Float => _floatValue,
            _ => _stringValue ?? string.Empty,
        };
    }
}
