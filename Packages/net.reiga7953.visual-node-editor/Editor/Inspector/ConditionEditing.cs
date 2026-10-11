using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Event ノードの条件の追加・パラメータや比較の変更・削除。どれも 1 回の Undo で戻せる。
    /// 比べる値の変更はインスペクタの入力欄のバインディングで行う（ここでは扱わない）。
    /// </summary>
    public static class ConditionEditing
    {
        /// <summary>
        /// 条件を末尾に足して返す。パラメータは Blackboard の最初のもの、比較はその型の最初のもの。
        /// パラメータが 1 つも無ければ何もせず null（条件はパラメータが無いと書けない）。
        /// </summary>
        public static TransitionCondition Add(NodeGraphAsset asset, EventNode eventNode)
        {
            var parameter = asset?.Parameters.FirstOrDefault(p => p != null);
            if (eventNode == null || parameter == null)
            {
                return null;
            }

            var condition = new TransitionCondition(parameter.Id, TransitionCondition.GetOperators(parameter.Type)[0]);
            Undo.RecordObject(asset, "Add Condition");
            eventNode.AddCondition(condition);
            EditorUtility.SetDirty(asset);
            return condition;
        }

        /// <summary>
        /// 条件のパラメータを変える。比較が新しいパラメータの型に合わなければ、その型の最初の比較にする。
        /// パラメータ・条件が無い、または同じなら何もせず false。
        /// </summary>
        public static bool SetParameter(NodeGraphAsset asset, EventNode eventNode, int index, string parameterId)
        {
            var condition = GetCondition(eventNode, index);
            var parameter = asset?.FindParameter(parameterId);
            if (condition == null || parameter == null || condition.ParameterId == parameterId)
            {
                return false;
            }

            Undo.RecordObject(asset, "Change Condition Parameter");
            condition.ParameterId = parameterId;
            if (!TransitionCondition.Supports(parameter.Type, condition.Operator))
            {
                condition.Operator = TransitionCondition.GetOperators(parameter.Type)[0];
            }

            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>条件の比較を変える。条件が無い・同じなら何もせず false。</summary>
        public static bool SetOperator(NodeGraphAsset asset, EventNode eventNode, int index, ConditionOperator op)
        {
            var condition = GetCondition(eventNode, index);
            if (asset == null || condition == null || condition.Operator == op)
            {
                return false;
            }

            Undo.RecordObject(asset, "Change Condition Operator");
            condition.Operator = op;
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>条件を外す。無ければ false。</summary>
        public static bool Remove(NodeGraphAsset asset, EventNode eventNode, int index)
        {
            if (asset == null || GetCondition(eventNode, index) == null)
            {
                return false;
            }

            Undo.RecordObject(asset, "Remove Condition");
            eventNode.RemoveConditionAt(index);
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static TransitionCondition GetCondition(EventNode eventNode, int index) =>
            eventNode != null && index >= 0 && index < eventNode.Conditions.Count ? eventNode.Conditions[index] : null;
    }
}
