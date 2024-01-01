// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;

namespace TestInfrastructure;

[TestClass]
[TestCategory("Infrastructure")]
public class StringHelpers
{

    static ExpandoObject TestMacros =>
        new()
        {
            { "Id", "123" },
            { "Name", "Name" },
        };

    [TestMethod]
    public void ResolveMacros()
    {
        var macros = TestMacros;

        Assert.AreEqual("123", "$(Id)".ResolveMacros(macros));
        Assert.AreEqual("Name = 123", "$(Name) = $(Id)".ResolveMacros(macros));
    }
}