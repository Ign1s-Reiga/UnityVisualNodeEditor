using NUnit.Framework;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>Event の条件 1 つの判定と表示（<see cref="TransitionCondition"/>）。</summary>
    public sealed class TransitionConditionTests
    {
        [Test]
        public void Operators_DependOnTheParameterType()
        {
            Assert.That(TransitionCondition.GetOperators(GraphParameterType.Bool),
                Is.EqualTo(new[] { ConditionOperator.IsTrue, ConditionOperator.IsFalse }));
            Assert.That(TransitionCondition.GetOperators(GraphParameterType.String),
                Is.EqualTo(new[] { ConditionOperator.Equal, ConditionOperator.NotEqual }));
            Assert.That(TransitionCondition.GetOperators(GraphParameterType.Int).Count, Is.EqualTo(6));
            Assert.That(TransitionCondition.Supports(GraphParameterType.Float, ConditionOperator.GreaterOrEqual), Is.True);
            Assert.That(TransitionCondition.Supports(GraphParameterType.Bool, ConditionOperator.Greater), Is.False);
            Assert.That(TransitionCondition.Supports(GraphParameterType.String, ConditionOperator.Less), Is.False);
        }

        [TestCase(ConditionOperator.Equal, 5, true)]
        [TestCase(ConditionOperator.NotEqual, 5, false)]
        [TestCase(ConditionOperator.Less, 6, true)]
        [TestCase(ConditionOperator.LessOrEqual, 5, true)]
        [TestCase(ConditionOperator.Greater, 5, false)]
        [TestCase(ConditionOperator.GreaterOrEqual, 5, true)]
        [TestCase(ConditionOperator.GreaterOrEqual, 6, false)]
        public void Int_ComparesNumbers(ConditionOperator op, int target, bool expected)
        {
            var condition = new TransitionCondition("id", op) { IntValue = target };

            Assert.That(condition.IsMet(GraphParameterType.Int, 5), Is.EqualTo(expected));
        }

        [Test]
        public void Float_ComparesExactly()
        {
            var condition = new TransitionCondition("id", ConditionOperator.Equal) { FloatValue = 0.5f };

            Assert.That(condition.IsMet(GraphParameterType.Float, 0.5f), Is.True);
            Assert.That(condition.IsMet(GraphParameterType.Float, 0.5001f), Is.False, "no rounding");
        }

        [Test]
        public void Bool_UsesTheOperatorOnly()
        {
            Assert.That(new TransitionCondition("id", ConditionOperator.IsTrue).IsMet(GraphParameterType.Bool, true), Is.True);
            Assert.That(new TransitionCondition("id", ConditionOperator.IsFalse).IsMet(GraphParameterType.Bool, true), Is.False);
        }

        [Test]
        public void String_IsCaseSensitive()
        {
            var condition = new TransitionCondition("id", ConditionOperator.Equal) { StringValue = "Alice" };

            Assert.That(condition.IsMet(GraphParameterType.String, "Alice"), Is.True);
            Assert.That(condition.IsMet(GraphParameterType.String, "alice"), Is.False);
            Assert.That(new TransitionCondition("id", ConditionOperator.NotEqual) { StringValue = "Alice" }.IsMet(GraphParameterType.String, "Bob"), Is.True);
        }

        [Test]
        public void WrongTypeOrOperator_IsNotMet()
        {
            Assert.That(new TransitionCondition("id", ConditionOperator.Greater).IsMet(GraphParameterType.Bool, true), Is.False);
            Assert.That(new TransitionCondition("id", ConditionOperator.Equal).IsMet(GraphParameterType.Int, "5"), Is.False);
        }

        [Test]
        public void Describe_ReadsLikeTheGraph()
        {
            var affection = new GraphParameter("Affection", GraphParameterType.Int);
            var met = new GraphParameter("Met", GraphParameterType.Bool);
            var name = new GraphParameter("Name", GraphParameterType.String);
            var speed = new GraphParameter("Speed", GraphParameterType.Float);

            Assert.That(new TransitionCondition(affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 5 }.Describe(affection), Is.EqualTo("Affection >= 5"));
            Assert.That(new TransitionCondition(met.Id, ConditionOperator.IsTrue).Describe(met), Is.EqualTo("Met"));
            Assert.That(new TransitionCondition(met.Id, ConditionOperator.IsFalse).Describe(met), Is.EqualTo("not Met"));
            Assert.That(new TransitionCondition(name.Id, ConditionOperator.Equal) { StringValue = "Alice" }.Describe(name), Is.EqualTo("Name == \"Alice\""));
            Assert.That(new TransitionCondition(speed.Id, ConditionOperator.Less) { FloatValue = 1.5f }.Describe(speed), Is.EqualTo("Speed < 1.5"));
            Assert.That(new TransitionCondition("gone", ConditionOperator.Equal).Describe(null), Is.EqualTo("(missing)"));
        }
    }
}
