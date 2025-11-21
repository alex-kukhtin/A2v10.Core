// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text;

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
		var sb = new StringBuilder();
		foreach (var stream in provider.EnumerateFileStreamsRO("_layout/_styles.html"))
		{
			if (stream == null)
				continue;
            using var rdr = new StreamReader(stream);
            var str = await rdr.ReadToEndAsync();
			sb.AppendLine(str);
			stream.Dispose();
		}
		return sb.ToString();
	}

    public static async Task<String> LayoutScripts(this IAppCodeProvider provider)
	{
        var sb = new StringBuilder();
        foreach (var stream in provider.EnumerateFileStreamsRO("_layout/_scripts.html"))
        {
            if (stream == null)
                continue;
            using var rdr = new StreamReader(stream);
            var str = await rdr.ReadToEndAsync();
            sb.AppendLine(str);
            stream.Dispose();
        }
        return sb.ToString();
    }

    public static String? CustomManifest(this IWebHostFilesProvider provider)
	{
		var manifestPath = provider.MapHostingPath("manifest.json");
		return File.Exists(manifestPath) ? "<link rel=\"manifest\" href=\"/manifest.json\">" : null;
	}

	public static String? SwitchLocale(this IGlobalization glob)
	{
        var dateLocale = glob.DateLocale;
        var numLocale = glob.NumberLocale;
		if (String.IsNullOrEmpty(dateLocale) && String.IsNullOrEmpty(numLocale))
			return null;
        var sb = new StringBuilder("<script type=\"text/javascript\">");
        if (!String.IsNullOrEmpty(dateLocale))
            sb.Append($"window.$$locale.$DateLocale = '{dateLocale}';");
        if (!String.IsNullOrEmpty(numLocale))
            sb.Append($"window.$$locale.$NumberLocale = '{numLocale}';");
		sb.Append("</script>");
        return sb.ToString();
    }
}

