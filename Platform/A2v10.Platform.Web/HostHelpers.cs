﻿// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;
public static class HostHelpers
{
	// @Html.Raw
	public static String AppStyleSheetsLink(this IAppCodeProvider provider)
	{
		var files = provider.EnumerateAllFiles("_assets", "*.css");
		// at least one file
		if (files != null && files.Any())
			return $"<link  href=\"/_shell/appstyles\" rel=\"stylesheet\" />";
		return String.Empty;
	}

	// @Html.Raw
	public static String AppScriptsLink(this IAppCodeProvider provider)
	{
		var files = provider.EnumerateAllFiles("_assets", "*.js");
		if (files != null && files.Any())
			return $"<script type=\"text/javascript\" src=\"/_shell/appscripts\"></script>";
		return String.Empty;
	}

	public static async Task<String> AppLinksAsync(this IAppCodeProvider provider)
	{
		using var stream = provider.FileStreamRO("links.json", primaryOnly: true);
		if (stream != null)
		{
			using var sr = new StreamReader(stream);
			var appLinks = await sr.ReadToEndAsync();
			if (!String.IsNullOrEmpty(appLinks))
			{
				// with validation
				Object? links = JsonConvert.DeserializeObject<List<Object>>(appLinks);
				return JsonConvert.SerializeObject(links);
			}
		}
		return "[]";
	}

	public static Task<String> CustomAppHead(this IAppCodeProvider _1/*provider*/)
	{
        /* TODO: From main module?
		String head = provider.ReadTextFile("_layout", "_head.html");
		String? head = await provider.ReadTextFileAsync("_layout", "_head.html", false);
		*/
        return Task.FromResult<String>(String.Empty);
	}

	public static Task<String> CustomAppScripts(this IAppCodeProvider _1/*provider*/)
	{
        // TODO: From main module?
        /*
        var scripts = await provider.ReadTextFileAsync("_layout", "_scripts.html", false);
        if (scripts == null)
            return String.Empty;
		String scripts = provider.ReadTextFile("_layout", "_scripts.html");
		return scripts != null ? host.GetAppSettings(scripts) : String.Empty;
		*/
        return Task.FromResult<String>(String.Empty);
    }

    public static String? CustomManifest(this IWebHostFilesProvider provider)
	{
		var manifestPath = provider.MapHostingPath("manifest.json");
		return File.Exists(manifestPath) ? "<link rel=\"manifest\" href=\"/manifest.json\">" : null;
	}
}

