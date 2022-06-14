// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;

using Microsoft.AspNetCore.Hosting;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebHostFilesProvider : IWebHostFilesProvider
{

	private readonly IWebHostEnvironment _webHost;
	public WebHostFilesProvider(IWebHostEnvironment webHost)
	{
		_webHost = webHost;
	}

	public String MapHostingPath(String path)
	{
		return Path.Combine(_webHost.WebRootPath, path);
	}
}
