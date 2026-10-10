using System;
using System.Collections.Generic;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP の JSON の読み書き（依存を増やさないための自前の実装）。</summary>
    public sealed class McpJsonTests
    {
        [Test]
        public void Parse_ReadsObjectsArraysAndValues()
        {
            var value = (Dictionary<string, object>)McpJson.Parse(
                "{ \"id\": 7, \"ratio\": -2.5e1, \"ok\": true, \"none\": null, \"list\": [1, \"two\", false], " +
                "\"text\": \"a\\\"b\\\\c\\n\\u3042\", \"nested\": { \"x\": {} } }");

            Assert.That(value["id"], Is.EqualTo(7L));
            Assert.That(value["ratio"], Is.EqualTo(-25.0));
            Assert.That(value["ok"], Is.EqualTo(true));
            Assert.That(value["none"], Is.Null);
            Assert.That(value["list"], Is.EqualTo(new List<object> { 1L, "two", false }));
            Assert.That(value["text"], Is.EqualTo("a\"b\\c\nあ"));
            Assert.That(((Dictionary<string, object>)value["nested"])["x"], Is.Empty);
        }

        [TestCase("{")]
        [TestCase("[1,]")]
        [TestCase("{\"a\" 1}")]
        [TestCase("tru")]
        [TestCase("\"open")]
        [TestCase("1 2")]
        [TestCase("")]
        public void Parse_RejectsBrokenJson(string json)
        {
            Assert.Throws<FormatException>(() => McpJson.Parse(json));
        }

        [Test]
        public void Serialize_WritesValuesThatParseBack()
        {
            var original = new Dictionary<string, object>
            {
                ["text"] = "line\n\"quoted\"\t\u0001",
                ["number"] = 1.5f,
                ["integer"] = 42,
                ["flag"] = false,
                ["nothing"] = null,
                ["items"] = new[] { "a", "b" },
                ["kind"] = GraphIssueKind.MissingEntry,
            };

            var json = McpJson.Serialize(original);
            var parsed = (Dictionary<string, object>)McpJson.Parse(json);

            Assert.That(parsed["text"], Is.EqualTo("line\n\"quoted\"\t\u0001"));
            Assert.That(parsed["number"], Is.EqualTo(1.5));
            Assert.That(parsed["integer"], Is.EqualTo(42L));
            Assert.That(parsed["flag"], Is.EqualTo(false));
            Assert.That(parsed["nothing"], Is.Null);
            Assert.That(parsed["items"], Is.EqualTo(new List<object> { "a", "b" }));
            Assert.That(parsed["kind"], Is.EqualTo("MissingEntry"), "enums are written by name");
            Assert.That(McpJson.Serialize(double.NaN), Is.EqualTo("null"));
            Assert.That(McpJson.Serialize(float.PositiveInfinity), Is.EqualTo("null"));
            Assert.That(McpJson.Serialize(0.1f), Is.EqualTo("0.1"), "floats are written as floats, not widened to double");
            Assert.That(McpJson.Serialize(10.1f), Is.EqualTo("10.1"));
        }
    }
}
