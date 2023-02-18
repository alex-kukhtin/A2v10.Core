﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System.IO;

namespace A2v10.Platform.Web;
public static class HostHelpers
{
    // @Html.Raw
    public static String AppStyleSheetsLink(this IAppCodeProvider provider)
    {
        var files = provider.EnumerateFiles("_assets", "*.css", false);
        // at least one file
        if (files != null && files.Any())
            return $"<link  href=\"/_shell/appstyles\" rel=\"stylesheet\" />";
        return String.Empty;
    }

    // @Html.Raw
    public static String AppScriptsLink(this IAppCodeProvider provider)
    {
        var files = provider.EnumerateFiles("_assets", "*.js", false);
        if (files != null && files.Any())
            return $"<script type=\"text/javascript\" src=\"/_shell/appscripts\"></script>";
        return String.Empty;
    }

    public static async Task<String> AppLinksAsync(this IAppCodeProvider provider)
    {
        String? appLinks = await provider.ReadTextFileAsync(String.Empty, "links.json", false);
        if (appLinks != null)
        {
            // with validation
            Object? links = JsonConvert.DeserializeObject<List<Object>>(appLinks);
            return JsonConvert.SerializeObject(links);
        }
        return "[]";
    }

    public static async Task<String> CustomAppHead(this IAppCodeProvider provider)
    {
        String? head = await provider.ReadTextFileAsync("_layout", "_head.html", false);
        /* TODO:
		String head = provider.ReadTextFile("_layout", "_head.html");
		*/
        return String.Empty;
    }

    public static async Task<String> CustomAppScripts(this IAppCodeProvider provider)
    {
        var scripts = await provider.ReadTextFileAsync("_layout", "_scripts.html", false);
        if (scripts == null)
            return String.Empty;
        // TODO:
        /*
		String scripts = provider.ReadTextFile("_layout", "_scripts.html");
		return scripts != null ? host.GetAppSettings(scripts) : String.Empty;
		*/
        return String.Empty;
    }

    public static String? CustomManifest(this IWebHostFilesProvider provider)
    {
        var manifestPath = provider.MapHostingPath("manifest.json");
        return File.Exists(manifestPath) ? "<link rel=\"manifest\" href=\"/manifest.json\">" : null;
    }

    public static Task ProcessDbEvents(this IApplicationHost host, IDbContext dbContext)
    {
        // TODO:
        throw new NotImplementedException(nameof(ProcessDbEvents));
        //return ProcessDbEventsCommand.ProcessDbEvents(dbContext, host.CatalogDataSource, host.IsAdminMode);
    }

    public static ITypeChecker? CheckTypes(this IApplicationHost host, String path, String typesFile, IDataModel model)
    {
        // TODO:
        if (!host.IsDebugConfiguration)
            return null;
        if (String.IsNullOrEmpty(typesFile))
            return null;
        return null;
        /*
		var tc = new TypeChecker(host.ApplicationReader, path);
		tc.CreateChecker(typesFile, model);
		return tc;
		*/
    }
}

