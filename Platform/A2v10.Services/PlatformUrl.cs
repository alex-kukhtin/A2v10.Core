﻿// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Web;


namespace A2v10.Services;
public class PlatformUrl : IPlatformUrl
{
	public PlatformUrl(UrlKind kind, String url, String? id = null)
	{
		var nurl = NormalizePath(url);
		Kind = kind;
		var parts = ("_/" + nurl.Path).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
		Construct(parts, nurl.Query, id);
	}

	public PlatformUrl(String url, String? id = null)
	{
		var nurl = NormalizePath(url);

		var parts = nurl.Path.Split('/');

		Kind = parts[0] switch
		{
			"_page" => UrlKind.Page,
			"_dialog" => UrlKind.Dialog,
			"_popup" => UrlKind.Popup,
			"_image" => UrlKind.Image,
			_ => UrlKind.Undefined
		};
		Construct(parts, nurl.Query, id);
	}

	public String LocalPath { get; set; } = String.Empty;
	public String BaseUrl { get; private set; } = String.Empty;

	public UrlKind Kind { get; private set; }
	public String Action { get; private set; } = String.Empty;
	public String? Id { get; private set; }

	public Boolean Auto { get; private set; }
	public ExpandoObject? Query { get; private set; }

	public void Redirect(String? path)
	{
		if (path == null || LocalPath == path)
			return;
		LocalPath = path;
	}

	public String NormalizedLocal(String fileName)
	{
		return Path.GetRelativePath(".", Path.Combine(LocalPath, fileName)).NormalizeSlash();
	}

	static (String Path, String Query) NormalizePath(String path)
	{
		String query = String.Empty;

		if (path.Contains('?'))
		{
			var px = path.Split('?');
			path = px[0];
			query = px[1];
		}
				
		path = path.ToLowerInvariant().Replace('\\', '/');
		if (path.StartsWith('/'))
			path = path[1..];
		return (path, query);
	}

	void Construct(String[] parts, String query, String? id)
	{
		Int32 len = parts.Length;
		if (len == 0)
			throw new InvalidOperationException($"Invalid URL {String.Join('/', parts)}");
		Id = parts[len - 1];
		if (String.IsNullOrEmpty(id))
		{
			/* HACK? */
			if (String.IsNullOrEmpty(Id) || Id == "0" || Id.Equals("new", StringComparison.OrdinalIgnoreCase))
				Id = null;
		}
		else
			Id = id;
		Action = parts[len - 2];
		if (len < 3)
			throw new InvalidOperationException($"Invalid URL {String.Join('/', parts)}");
		var pathArr = new ArraySegment<String>(parts, 1, len - 3);
		LocalPath = String.Join("/", pathArr.ToArray());
		// baseUrl with action and id
		var baseArr = new List<String>(pathArr)
		{
			Action
		};
		if (Id != null)
			baseArr.Add(Id);
		baseArr.Add(String.Empty); // for last slash

		BaseUrl = String.Join("/", baseArr);

		if (!String.IsNullOrEmpty(query))
		{
			var eo = new ExpandoObject();
			var nvc = HttpUtility.ParseQueryString(query);
			foreach (var k in nvc.AllKeys)
			{
				if (k != null)
					AddQueryParam(eo, k, nvc[k]);
			}
			if (!eo.IsEmpty())
				Query = eo;
		}
	}

    public IPlatformUrl CreateFromMetadata(String localPath)
	{
		return new PlatformUrl(localPath);
	}

    static void AddQueryParam(ExpandoObject eo, String key, String? value)
	{
		if (value == null)
			return;
		if (!key.StartsWith("period", StringComparison.OrdinalIgnoreCase))
		{
			eo.Set(key.ToPascalCase(), value);
		}
		else
		{
			var suffix = key[6..]; // period
            var ps = value.Split('-');
			eo.RemoveKeys($"From{suffix},To{suffix}"); // replace prev value
			if ("all".Equals(ps[0], StringComparison.OrdinalIgnoreCase))
			{
				// from js! utils.date.minDate/maxDate
				eo.Set($"From{suffix}", "19010101");
				eo.Set($"To{suffix}", "29991231");
			}
			else
			{
				eo.Set($"From{suffix}", ps[0]);
				eo.Set($"To{suffix}", ps.Length == 2 ? ps[1] : ps[0]);
			}
		}
	}
}

