// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

using A2v10.Platform.Web.Controllers;

namespace TestServices;

// a message of an exception may carry what the url said: it leaves as text, never as markup or code
[TestClass]
[TestCategory("ErrorOutput")]
public class ErrorOutputTests
{
	private const String Message = "File not found '<script>alert(1)</script>' `${alert(2)}` \"q\" & 'a'";

	private static async Task<String> BodyAsync(IActionResult result)
	{
		var http = new DefaultHttpContext();
		http.Response.Body = new MemoryStream();
		await result.ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));
		http.Response.Body.Position = 0;
		return await new StreamReader(http.Response.Body).ReadToEndAsync();
	}

	[TestMethod]
	public async Task The_image_error_is_text_of_the_svg()
	{
		var svg = await BodyAsync(BaseController.WriteImageException(new FileNotFoundException(Message)));

		XNamespace ns = "http://www.w3.org/2000/svg";
		var doc = XDocument.Parse(svg);
		Assert.AreEqual(Message, doc.Root!.Element(ns + "text")!.Value);
		Assert.IsFalse(doc.Descendants(ns + "script").Any());
	}

	[TestMethod]
	public async Task The_script_error_is_a_string_literal()
	{
		var js = await BodyAsync(new ScriptExceptionResult(new InvalidOperationException(Message)));

		Assert.IsTrue(js.StartsWith("alert(\"") && js.EndsWith("\")"), js);
		Assert.AreEqual(Message, JsonSerializer.Deserialize<String>(js["alert(".Length..^1]));
		Assert.IsFalse(js.Contains('`'), js);
		Assert.IsFalse(js.Contains("<script>"), js);
	}
}
