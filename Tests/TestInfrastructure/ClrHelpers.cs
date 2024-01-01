// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace TestInfrastructure;

[TestClass]
[TestCategory("Infrastructure")]
public class ClrHelpersTest
{

	[TestMethod]
	public void IsClrPath()
    {
        const String pathOk = "clr-type:Type.Name;assembly=Assembly.Name";
        const String pathFail = "clr-type,Type;asss+Test";
        Assert.AreEqual(true, ClrHelpers.IsClrPath(pathOk));
		Assert.AreEqual(false, ClrHelpers.IsClrPath(pathFail));

		var (assembly, type) = ClrHelpers.ParseClrType(pathOk);
		Assert.AreEqual("Type.Name", type);
		Assert.AreEqual("Assembly.Name", assembly);

		Assert.ThrowsException<ArgumentException>(() =>
		{
			ClrHelpers.ParseClrType(pathFail);
		});
	}
}