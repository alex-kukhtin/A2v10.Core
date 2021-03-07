﻿// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc
{
	public static class HostHelpers
	{
		public static String AppStyleSheetsLink(this IAppCodeProvider provider)
		{
			var files = provider.EnumerateFiles("_assets", "*.css");
			if (files == null)
				return String.Empty;
			// at least one file
			foreach (var f in files)
				return $"<link  href=\"/_shell/appstyles\" rel=\"stylesheet\" />";
			return String.Empty;
		}

		public static String AppLinks(this IAppCodeProvider provider)
		{
			String appLinks = provider.ReadTextFile(String.Empty, "links.json");
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

		public static String CustomAppScripts(this IAppCodeProvider provider)
		{
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
