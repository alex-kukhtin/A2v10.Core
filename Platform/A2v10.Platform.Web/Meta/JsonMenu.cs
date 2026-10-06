// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
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
    public Int32? Columns { get; init; }
    public Int32? Row { get; init; }
    public Int32? Col { get; init; }
}


internal record JsonMenuRoot
{
    public String AppTitle { get; init; } = String.Empty;
    public List<JsonMenu>? Menu { get; init; }
    public Int32? Columns { get; init; }

    const String GRANTS_PROC = "a2security.[User.Grants.Load]";

    /* menu.json as this user may see it. One road for both readers - the shell and the aux page - so
     * that the filter cannot be put on one of them and forgotten on the other. null - no menu.json.
     * The menu hides, it does not forbid: an entry left out is still reached by its url.
     */
    internal static async Task<JsonMenuRoot?> LoadAsync(IAppCodeProvider codeProvider, ILocalizer localizer,
        IAppRuntimeBuilder runtimeBuilder, IDbContext dbContext, String? dataSource, Int64? userId)
    {
        using var stream = codeProvider.FileStreamRO("menu.json", true);
        if (stream == null)
            return null;
        using var sr = new StreamReader(stream);
        var json = localizer.Localize(null, await sr.ReadToEndAsync(), false)
            ?? throw new InvalidOperationException("menu.json is empty");
        var root = JsonConvert.DeserializeObject<JsonMenuRoot>(json, JsonHelpers.StandardSerializerSettings)
            ?? throw new InvalidOperationException("menu.json deserialize fail");

        if (!await runtimeBuilder.UseGrantsAsync())
            return root;
        var dm = await dbContext.LoadModelAsync(dataSource, GRANTS_PROC, new ExpandoObject() { { "UserId", userId } });
        if (dm.Eval<Boolean>("UserState.IsAdmin"))
            return root;
        var roles = (dm.Root.Get<List<ExpandoObject>>("Roles") ?? []).Select(r => r.Get<String>("Id")!).ToHashSet(StringComparer.Ordinal);
        return root with { Menu = Transform(root.Menu, Grants(dm.Root.Get<List<ExpandoObject>>("Grants")), roles) };
    }

    // ignoring case as the urls do
    private static Dictionary<String, PermissionFlag> Grants(List<ExpandoObject>? rows)
    {
        static PermissionFlag Flag(ExpandoObject row, String column, PermissionFlag flag) =>
            row.Get<Boolean>(column) ? flag : default;
        return (rows ?? []).ToDictionary(
            r => r.Get<String>("Endpoint")!,
            r => Flag(r, "CanView", PermissionFlag.CanView) | Flag(r, "CanCreate", PermissionFlag.CanCreate),
            StringComparer.OrdinalIgnoreCase);
    }

    /* Two questions, not one. A section's 'roles' says whose workplace it is - a catalog everyone may
     * read (Everyone: view, so that its picker works) would otherwise keep the accounting section alive
     * for the storekeeper. Asked of the sections only; ordinal, as role keys are.
     */
    internal static List<JsonMenu>? Transform(List<JsonMenu>? sections, IReadOnlyDictionary<String, PermissionFlag> grants,
        IReadOnlySet<String> roles) =>
        Filter(sections?.Where(s => s.Roles == null || s.Roles.Any(roles.Contains)).ToList(), grants);

    /* The rest by the shape of a node, not its level: a leaf (url) is seen with CanView on its endpoint,
     * and its 'create' with CanCreate; a node with items (section, subsection, aux) lives while a child
     * that is not a spacer does; a spacer always - it is decoration, not an entry.
     */
    private static List<JsonMenu>? Filter(List<JsonMenu>? items, IReadOnlyDictionary<String, PermissionFlag> grants) =>
        items?.Select(i => Visible(i, grants)).OfType<JsonMenu>().ToList();

    private static JsonMenu? Visible(JsonMenu item, IReadOnlyDictionary<String, PermissionFlag> grants)
    {
        if (item.Grow)
            return item;
        if (item.Items != null)
        {
            var items = Filter(item.Items, grants)!;
            return items.Any(i => !i.Grow) ? item with { Items = items } : null;
        }
        var flags = item.Url != null && grants.TryGetValue(item.Url, out var found) ? found : default;
        if (!flags.HasFlag(PermissionFlag.CanView))
            return null;
        return item.Create && !flags.HasFlag(PermissionFlag.CanCreate) ? item with { Create = false } : item;
    }

    internal static String ConvertToPlatformMenu(JsonMenuRoot root, ILocalizer localizer)
    {
        var menuRoot = new JsonMenu() { Items = root.Menu };

        var newRoot = new List<JsonPlatfomMenu>() { ToPlatform(menuRoot, 0, localizer) };
        var newTop = new JsonPlatfomMenu()
        {
            Menu = newRoot,
            SysParams = new JsonSysParams() { AppTitle = root.AppTitle },
            Columns = root.Columns
        };

        return JsonConvert.SerializeObject(newTop, JsonHelpers.StandardSerializerSettings);
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
            Row = level == 2 ? root.Row : null,
            Col = level == 2 ? root.Col : null, 
            ClassName = root.ToClassName(),
            CreateUrl = root.ToCreateUrl(),
            CreateName = root.ToCreateName(localizer),
            Url = isAux ? $"page:/_auxmenu/any?mode={root.Id}" : $"page:{root.Url}/index/0",
            Menu = isAux ? null : root.Items?.Select(i => ToPlatform(i, level + 1, localizer))?.ToList()
        };

        return platfom;
    }
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
    // a section's only: whose workplace it is (JsonMenuRoot.Transform)
    public String[]? Roles { get; init; }
    public List<JsonMenu>? Items { get; init; }
    public Int32? Row { get; init; }
    public Int32? Col { get; init; }

    internal String? ToClassName()
    {
        if (Grow)
            return "grow";
        else if (Underline)
            return "border-bottom";
        return null;
    }

    internal String? ToCreateUrl()
    {
        if (!Create)
            return null;
        return $"dialog:{Url}/edit/new";
    }

    internal String? IdFromUrl() => Url?.Split('/')[^1];

    internal String? ToCreateName(ILocalizer localizer)
    {
        if (!Create)
            return null;
        return localizer.Localize(null, "@[Create]", false);
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
