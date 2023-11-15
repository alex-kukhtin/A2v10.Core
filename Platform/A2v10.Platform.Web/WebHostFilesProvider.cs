// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;

using Microsoft.AspNetCore.Hosting;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebHostFilesProvider(IWebHostEnvironment webHost) : IWebHostFilesProvider
{

	private readonly IWebHostEnvironment _webHost = webHost;

    public String MapHostingPath(String path)
	{
		return Path.Combine(_webHost.WebRootPath, path);
	}
}
