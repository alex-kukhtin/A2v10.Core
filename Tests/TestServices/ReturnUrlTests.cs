// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

using A2v10.Identity.UI;

namespace TestServices;

// after a sign-in the browser goes to a local address of this site, or to the root
[TestClass]
[TestCategory("ReturnUrl")]
public class ReturnUrlTests
{
	private static readonly IUrlHelper Url = new UrlHelper(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()));

	[TestMethod]
	public void A_foreign_address_goes_to_the_root()
	{
		foreach (var url in new[] { "https://evil", "//evil", "/\\evil", "javascript:alert(1)", "evil", "/account/login", "/Account/twofactor", "", null })
			Assert.AreEqual("/", ReturnUrls.LocalOrRoot(Url, url), url);
	}

	[TestMethod]
	public void A_local_address_passes()
	{
		Assert.AreEqual("/app/page", ReturnUrls.LocalOrRoot(Url, "/app/page"));
		Assert.AreEqual("/catalog/agent/index?x=1", ReturnUrls.LocalOrRoot(Url, "/catalog/agent/index?x=1"));
	}
}
