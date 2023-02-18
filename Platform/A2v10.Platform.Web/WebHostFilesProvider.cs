// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
