using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP の JSON の値をパラメータの型の値にする（<see cref="McpValues"/>）。範囲外の数を黙って変えない。</summary>
    public sealed class McpValuesTests
    {
        [Test]
        public void Ints_MustBeWholeAndInRange()
        {
            Assert.That(McpValues.ToParameterValue(5L, GraphParameterType.Int), Is.EqualTo(5));
            Assert.That(McpValues.ToParameterValue(3.0, GraphParameterType.Int), Is.EqualTo(3), "a whole number written as 3.0");
            Assert.That(McpValues.ToParameterValue(1.5, GraphParameterType.Int), Is.Null);
            Assert.That(McpValues.ToParameterValue(3000000000L, GraphParameterType.Int), Is.Null, "would wrap around as an int");
            Assert.That(McpValues.ToParameterValue(16777217L, GraphParameterType.Int), Is.EqualTo(16777217), "no float round-trip");
        }

        [Test]
        public void Floats_MustBeFinite()
        {
            Assert.That(McpValues.ToParameterValue(2L, GraphParameterType.Float), Is.EqualTo(2f));
            Assert.That(McpValues.ToParameterValue(0.5, GraphParameterType.Float), Is.EqualTo(0.5f));
            Assert.That(McpValues.ToParameterValue(double.NaN, GraphParameterType.Float), Is.Null);
            Assert.That(McpValues.ToParameterValue(1e300, GraphParameterType.Float), Is.Null, "beyond float");
        }

        [Test]
        public void BoolsAndStrings_KeepTheirType()
        {
            Assert.That(McpValues.ToParameterValue(true, GraphParameterType.Bool), Is.EqualTo(true));
            Assert.That(McpValues.ToParameterValue(1L, GraphParameterType.Bool), Is.Null);
            Assert.That(McpValues.ToParameterValue("Alice", GraphParameterType.String), Is.EqualTo("Alice"));
            Assert.That(McpValues.ToParameterValue(3L, GraphParameterType.String), Is.Null);
        }
    }
}
