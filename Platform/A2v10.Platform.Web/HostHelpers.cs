// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

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
			stream.Close();
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

	public static async Task<String> LayoutStyles(this IAppCodeProvider provider)
	{
		// primary only
		using var stream = provider.FileStreamRO("_layout/_styles.html", true);
		if (stream == null)
			return String.Empty;
		using var rdr = new StreamReader(stream);
		return await rdr.ReadToEndAsync();
	}

	public static async Task<String> LayoutScripts(this IAppCodeProvider provider)
	{
		// primary only
		using var stream = provider.FileStreamRO("_layout/_scripts.html", true);
		if (stream == null)
			return String.Empty;
		using var rdr = new StreamReader(stream);
		return await rdr.ReadToEndAsync();
    }

    public static String? CustomManifest(this IWebHostFilesProvider provider)
	{
		var manifestPath = provider.MapHostingPath("manifest.json");
		return File.Exists(manifestPath) ? "<link rel=\"manifest\" href=\"/manifest.json\">" : null;
	}
}

