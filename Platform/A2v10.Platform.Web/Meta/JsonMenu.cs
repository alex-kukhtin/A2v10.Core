// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

internal record JsonAppTitle
{
    public String? AppTitle { get; set; }
    public String? AppSubTitle { get; set; }
}

internal record JsonSysParams
{
    public String? AppTitle { get; init; }
    public String? AppSubTitle { get; init; }
}

internal record JsonPlatfomMenu
{
    public String? Id { get; init; } = default!;
    public String Name { get; init; } = default!;
    public String? Url { get; set; }
    public String? Icon { get; set; }
    public String? ClassName { get; init; } // grow, border-bottom 
    public String? CreateUrl { get; init; }
    public String? CreateName { get; init; }
    public List<JsonPlatfomMenu>? Menu { get; init; }
    public JsonSysParams? SysParams { get; init;  }
}

internal record JsonMenu
{
    public String Title { get; init; } = default!;
    public String? Url { get; init; }
    public String? Category { get; init; }
    public String? Id { get; init; }
    public String? Icon { get; init; }
    public Boolean Grow { get; init; }
    public Boolean Underline { get; init; }
    public Boolean Create { get; init; }
    public List<JsonMenu>? Items { get; init; }

    private String? ToClassName()
    {
        if (Grow)
            return "grow";
        else if (Underline)
            return "border-bottom";
        return null;
    }

    private String? ToCreateUrl()
    {
        if (!Create)
            return null;
        return $"dialog:{Url}/edit/new";
    }

    internal String? IdFromUrl() => Url?.Split('/')[^1];

    private String? ToCreateName(ILocalizer localizer)
    {
        if (!Create)
            return null;
        return localizer.Localize(null, "@[Create]", false);
    }

    private static JsonPlatfomMenu ToPlatform(JsonMenu root, Int32 level, ILocalizer localizer)
    {
        Boolean isAux = false;
        if (level == 3 && root.Items?.Count > 0)
            isAux = true;
        var platfom = new JsonPlatfomMenu()
        {
            Name = root.Title,
            Icon = root.Icon,
            ClassName = root.ToClassName(),
            CreateUrl = root.ToCreateUrl(),
            CreateName = root.ToCreateName(localizer),
            Url = isAux ? $"page:/_auxmenu/any?mode={root.Id}" : $"page:{root.Url}/index/0",
            Menu = isAux ? null : root.Items?.Select(i => ToPlatform(i, level + 1, localizer))?.ToList()
        };
        return platfom;
    }
    internal static String ConvertToPlatformMenu(String json, String? appTitle, ILocalizer localizer)
    {
        var menuArray = JsonConvert.DeserializeObject<List<JsonMenu>>(json, JsonHelpers.StandardSerializerSettings)
            ?? throw new InvalidOperationException("menu.json deserialize fial");
        var menuRoot = new JsonMenu() { Items = menuArray };

        var newRoot = new List<JsonPlatfomMenu>() { ToPlatform(menuRoot, 0, localizer) };
        var newTop = new JsonPlatfomMenu() { 
            Menu = newRoot, 
            SysParams = new JsonSysParams() { AppTitle = appTitle } 
        };

        return JsonConvert.SerializeObject(newTop, JsonHelpers.StandardSerializerSettings);
    }

    internal static JsonMenu? FindById(IEnumerable<JsonMenu>? items, String? id)
    {
        if (items == null)
            return null;
        foreach (var itm in items)
        {
            if (itm.Id == id)
                return itm;
            var found = FindById(itm.Items, id);
            if (found != null) 
                return found;
        }
        return null;
    }
}
