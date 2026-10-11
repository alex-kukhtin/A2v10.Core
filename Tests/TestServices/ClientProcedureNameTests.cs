// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Services;

namespace TestServices;

/* A name the client sends lands in a procedure name: [Model.<prop>] of a lazy load,
 * [Model.<prop>.Delete] of a removal, [Model.<key>.Load] of an image. It is an identifier and
 * never a suffix the platform composes its own procedures from.
 */
[TestClass]
[TestCategory("ProcedureName")]
public class ClientProcedureNameTests
{
	private static ModelJson Model()
	{
		var rm = new ModelJson()
		{
			Schema = "cat",
			Model = "Agent",
			Actions = new(StringComparer.OrdinalIgnoreCase) { ["edit"] = new ModelJsonView() }
		};
		rm.OnEndInit(new PlatformUrl("/_page/catalog/agent/edit/1"));
		return rm;
	}

	private static readonly String[] Refused = ["delete", "Delete", "DELETE", "update", "Load", "index", "expand",
		"Copy", "Report", "Export", "Rows]", "Rows.Delete", "x;drop", "", "Ro ws"];

	[TestMethod]
	public void Lazy_property_is_an_identifier_of_the_application()
	{
		var view = Model().GetAction("edit");

		Assert.AreEqual("[cat].[Agent.Rows]", view.LoadLazyProcedure("Rows"));
		foreach (var name in Refused)
			Assert.ThrowsExactly<ModelJsonException>(() => view.LoadLazyProcedure(name), name);
	}

	[TestMethod]
	public void Removed_property_is_an_identifier_of_the_application()
	{
		var view = Model().GetAction("edit");

		Assert.AreEqual("[cat].[Agent.Delete]", view.DeleteProcedure(null));
		Assert.AreEqual("[cat].[Agent.Rows.Delete]", view.DeleteProcedure("Rows"));
		foreach (var name in Refused.Where(n => n.Length > 0))
			Assert.ThrowsExactly<ModelJsonException>(() => view.DeleteProcedure(name), name);
	}

	[TestMethod]
	public void Image_key_is_an_identifier_of_the_application()
	{
		var rm = Model();

		Assert.AreEqual("[cat].[Agent.Photo.Load]", rm.GetBlob("photo").LoadBlobProcedure());
		foreach (var name in Refused)
			Assert.ThrowsExactly<ModelJsonException>(() => rm.GetBlob(name), name);
	}
}
