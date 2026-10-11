// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Services;

namespace TestServices;

/* Layers, last wins: what the client sent (the query, then the posted data of an invoke) →
 * model.json 'parameters' → the url's Id → the user. A declared parameter binds the address.
 */
[TestClass]
[TestCategory("Parameters")]
public class ParameterLayersTests
{
	private static ExpandoObject Expando(params (String Key, Object? Value)[] values)
	{
		var eo = new ExpandoObject();
		foreach (var (k, v) in values)
			eo.Set(k, v);
		return eo;
	}

	private static void TheUser(ExpandoObject prms) => prms.Set("UserId", 1L);

	private static ModelJson Model() => new()
	{
		Schema = "doc",
		Model = "Document",
		Actions = new(StringComparer.OrdinalIgnoreCase) { ["index"] = new ModelJsonView() { Index = true, Parameters = Expando(("Operation", "invoice")) } },
		Commands = new(StringComparer.OrdinalIgnoreCase) { ["run"] = new ModelJsonCommand() { Procedure = "Run", Parameters = Expando(("Operation", "invoice")) } },
	};

	[TestMethod]
	public void The_query_does_not_override_a_declared_parameter()
	{
		var url = new PlatformUrl("_page/document/invoice/index/0?Operation=waybill&UserId=99&Fragment=abc");
		var rm = Model();
		rm.OnEndInit(url);

		var prms = rm.GetAction("index").CreateParameters(url, null, TheUser);

		Assert.AreEqual("invoice", prms.Get<String>("Operation"));
		Assert.AreEqual(1L, prms.Get<Int64>("UserId"));
		Assert.AreEqual("abc", prms.Get<String>("Fragment"));
	}

	[TestMethod]
	public void The_posted_data_of_an_invoke_override_the_query_and_nothing_declared()
	{
		var url = new PlatformUrl("_page/document/invoice/index/0?Fragment=abc");
		var rm = Model();
		rm.OnEndInit(url);
		url.AppendPosted(Expando(("Operation", "waybill"), ("UserId", 99L), ("Fragment", "xyz"), ("Id", 5L)));

		var prms = rm.GetCommand("run").CreateParameters(url, null, TheUser, IModelBase.ParametersFlags.SkipId);

		Assert.AreEqual("invoice", prms.Get<String>("Operation"));
		Assert.AreEqual(1L, prms.Get<Int64>("UserId"));
		Assert.AreEqual("xyz", prms.Get<String>("Fragment"));
		Assert.AreEqual(5L, prms.Get<Int64>("Id"));
	}

	[TestMethod]
	public void A_report_keeps_what_it_declares()
	{
		var report = new ModelJsonReport() { Parameters = Expando(("Kind", "A")), Variables = Expando(("Title", "T")) };
		var query = Expando(("Kind", "B"), ("Title", "X"), ("Rep", "r"), ("UserId", 99L), ("From", "2026-01-01"));

		var prms = report.CreateParameters(query, TheUser);
		var vars = report.CreateVariables(query, TheUser);

		Assert.AreEqual("A", prms.Get<String>("Kind"));
		Assert.AreEqual(1L, prms.Get<Int64>("UserId"));
		Assert.AreEqual("2026-01-01", prms.Get<String>("From"));
		Assert.IsFalse(((IDictionary<String, Object?>)prms).ContainsKey("Rep"));
		Assert.AreEqual("A", vars.Get<String>("Kind"));
		Assert.AreEqual("T", vars.Get<String>("Title"));
	}
}
