// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.Json.Serialization;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;


namespace A2v10.Services.Api;


public sealed class DataResult
{
    public ExpandoObject? Data { get; init; }
    public ExpandoObject? Metadata { get; init; }

    // the model itself, for the callers in the process; what the API sends is Data and Metadata
    [JsonIgnore]
    public IDataModel Model { get; init; } = default!;

    /* The id the save gave the record this model opened. The main object is a fact of the model
     * ('!MainObject'), not of the address, and the saved root carries no metadata - so it is asked
     * of the model that was saved, as the client asks its '$main'.
     */
    public Object IdOf(ExpandoObject saved)
    {
        var main = Model.Metadata["TRoot"].MainObject
            ?? throw new InvalidOperationException("The model marks no main object");
        return saved.Get<ExpandoObject>(main)?.Get<Object>("Id")
            ?? throw new InvalidOperationException($"The save returned no '{main}.Id'");
    }
}

/* Endpoints addressed by their route ('document/waybillin'), not by the URL conventions of the
 * client. Not a part of the platform host: registered by the callers that have no browser - the
 * API, the CLI, the scenario tests - via UseEndpointDataServices.
 */
public class EndpointDataService(IDataService _dataService)
{
    /* 'render': every load also builds the page and its template and drops them, so a screen that
     * cannot be built fails the call. The mode of the caller, not of a call: a test opens screens,
     * the API never does.
     */
    public DataAt At(String route, Boolean render = false) => new(_dataService, route, render);
}

public sealed class DataAt(IDataService _dataService, String _route, Boolean _render)
{
    private readonly (UrlKind Kind, String Prefix) _kind = KindOf(_route);

    public async Task<DataResult> IndexAsync(IndexQuery query, ExpandoObject filters)
    {
        var url = $"{_route}/index/0";
        var iq = CreateIndexQuery(query, filters);
        if (!String.IsNullOrEmpty(iq))
            url += $"?{iq}";
        var model = ModelOf(await _dataService.LoadAsync(UrlKind.Page, url, prms => { }, isReload: !_render));
        return new DataResult()
        {
            Data = model.Root.FitModelInfo(),
            Metadata = model.BuildDataModelMeta(),
            Model = model
        };
    }

    public async Task<DataResult> LoadAsync(String id)
    {
        var model = ModelOf(await _dataService.LoadAsync(_kind.Kind, $"{_route}/edit/{id}", prms => { }, isReload: !_render));
        return new DataResult()
        {
            Data = model.Root,
            Metadata = model.BuildDataModelMeta(),
            Model = model
        };
    }

    /* 'new' and not '0': the server reads both as no id (PlatformUrl), the client does not - for it
     * '/0' is new in a dialog only, and a page saved from 'edit/0' keeps that address.
     */
    public async Task<DataResult> CreateAsync()
    {
        var model = ModelOf(await _dataService.LoadAsync(_kind.Kind, $"{_route}/edit/new", prms => { }, isReload: !_render));
        return new DataResult()
        {
            Data = model.BuildNewInstance(),
            Metadata = model.BuildDataModelMeta(),
            Model = model
        };
    }

    public async Task<ExpandoObject> SaveAsync(ExpandoObject data)
    {
        var lr = await _dataService.SaveAsync($"{_kind.Prefix}/{_route}/edit/0", data, prms => { });
        return lr.Raw;
    }

    public Task PostAsync(Object id) => InvokeAsync("post", id);

    public Task UnPostAsync(Object id) => InvokeAsync("unpost", id);

    // as the generated template calls it: $invoke('post', {Id}, endpoint) → _page/<endpoint>/index/0
    async Task InvokeAsync(String command, Object id)
    {
        var data = new ExpandoObject();
        data.Set("Id", id);
        await _dataService.InvokeAsync($"{_kind.Prefix}/{_route}/index/0", command, data, prms => { });
    }

    IDataModel ModelOf(IDataLoadResult result)
        => result.Model ?? throw new InvalidOperationException($"{_route}: the load returned no model");

    // a catalog opens in a dialog, a document on a page
    static (UrlKind Kind, String Prefix) KindOf(String route) => route.Split('/')[0] switch
    {
        "catalog" => (UrlKind.Dialog, "_dialog"),
        "document" => (UrlKind.Page, "_page"),
        var other => throw new NotImplementedException($"Endpoint kind '{other}' in route '{route}'")
    };

    static String CreateIndexQuery(IndexQuery query, ExpandoObject filters)
    {
        IEnumerable<String> Params()
        {
            if (query.Take != 0)
                yield return $"PageSize={query.Take}";
            if (query.Skip != 0)
                yield return $"Offset={query.Skip}";
            if (!String.IsNullOrEmpty(query.Sort))
                yield return $"SortOrder={query.Sort}";
            yield return $"SortDir={(query.Desc ? "desc" : "asc")}";
            var d = filters as IDictionary<String, Object>;
            foreach (var x in d.Keys)
                yield return $"{x}={d[x]}";
        }
        return String.Join("&", Params());
    }
}
