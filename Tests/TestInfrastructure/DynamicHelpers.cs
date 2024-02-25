// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;

namespace TestInfrastructure;

[TestClass]
[TestCategory("Infrastructure")]
public class DynamicHelpers
{

    static ExpandoObject TestData =>
        new()
        {
            { "Id", 123 },
                { "Name", "Document name" },
                {
                "Agent", new ExpandoObject() {
                    {"Id", 77 },
                    {"Name", "Agent Name" }
                } },
                {
                "Rows", new List<ExpandoObject>() {
                    new() {
                        {"Item", new ExpandoObject()
                        {
                            { "Id", 99 },
                            { "Name", "Item 99"}
                        } }
                    },
                    new() {
                        {"Item", new ExpandoObject()
                        {
                            { "Id", 23 },
                            { "Name", "Item 23"}
                        }}
                    }
                } }
        };

    [TestMethod]
    public void EvalExpression()
    {
        var doc = TestData;

        Assert.AreEqual(123, doc.EvalExpression("Id"));
        Assert.AreEqual("Document name", doc.EvalExpression("Name"));
        Assert.AreEqual(77, doc.EvalExpression("Agent.Id"));
        Assert.AreEqual("Agent Name", doc.EvalExpression("Agent.Name"));
        Assert.AreEqual(99, doc.EvalExpression("Rows[0].Item.Id"));
        Assert.AreEqual("Item 99", doc.EvalExpression("Rows[0].Item.Name"));
        Assert.AreEqual(23, doc.EvalExpression("Rows[1].Item.Id"));
        Assert.AreEqual("Item 23", doc.EvalExpression("Rows[1].Item.Name"));
        Assert.IsNull(doc.EvalExpression("Test.Items[12].Text"));
    }

    [TestMethod]
    public void Resolve()
    {
        var doc = TestData;

        Assert.AreEqual("'123'", doc.Resolve("'{{Id}}'"));
        Assert.AreEqual("I am 'Document name'", doc.Resolve("I am '{{Name}}'"));
        Assert.AreEqual("Agent Id 77", doc.Resolve("Agent Id {{Agent.Id}}"));
        Assert.AreEqual("Agent Name", doc.Resolve("{{Agent.Name}}"));
        Assert.AreEqual("99", doc.Resolve("{{Rows[0].Item.Id}}"));
        Assert.AreEqual("Item 99 = 99", doc.Resolve("{{Rows[0].Item.Name}} = {{Rows[0].Item.Id}}"));
        Assert.AreEqual("Item 23 = 23", doc.Resolve("{{Rows[1].Item.Name}} = {{Rows[1].Item.Id}}"));
    }

    [TestMethod]
    public void ReplaceValue()
    {
        var x = new ExpandoObject()
        {
            { "Id", 23 },
            { "Name", "Item 23"}
        };
        x.ReplaceValue("Id", x => ((Int32)(x ?? 0) + 100));
        x.ReplaceValue("Name", s => s?.ToString()!.Replace("23", "77"));
		Assert.AreEqual(123, x.Get<Int32>("Id"));
		Assert.AreEqual("Item 77", x.Get<String>("Name"));
	}
}