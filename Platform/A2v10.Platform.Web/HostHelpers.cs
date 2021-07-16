// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web
{
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
			String appLinks = await provider.ReadTextFileAsync(String.Empty, "links.json", false);
			if (appLinks != null)
			{
				// with validation
				Object links = JsonConvert.DeserializeObject<List<Object>>(appLinks);
				return JsonConvert.SerializeObject(links);
			}
			return "[]";
		}

		public static String CustomAppHead(this IAppCodeProvider provider)
		{
			/* TODO:
			String head = provider.ReadTextFile("_layout", "_head.html");
			return head != null ? host.GetAppSettings(head) : String.Empty;
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

		public static String CustomManifest(this IAppCodeProvider provider)
		{
			var manifestPath = provider.MapHostingPath("manifest.json");
			return provider.FileExists(manifestPath) ? "<link rel=\"manifest\" href=\"/manifest.json\">" : null;
		}

		public static Task ProcessDbEvents(this IApplicationHost host, IDbContext dbContext)
		{
			// TODO:
			throw new NotImplementedException(nameof(ProcessDbEvents));
			//return ProcessDbEventsCommand.ProcessDbEvents(dbContext, host.CatalogDataSource, host.IsAdminMode);
		}

		public static ITypeChecker CheckTypes(this IApplicationHost host, String path, String typesFile, IDataModel model)
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
}
