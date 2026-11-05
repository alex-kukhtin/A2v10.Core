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

    /* The main object is a fact of the model ('!MainObject'), not of the address, and the saved root
     * carries no metadata - so it is asked of the model that was saved, as the client asks its '$main'.
     */
    public String MainName => Model.Metadata["TRoot"].MainObject
        ?? throw new InvalidOperationException("The model marks no main object");

    // the record of this model, or of the root a save of it returned; null: there is no such record
    public ExpandoObject? Main(ExpandoObject? root = null) => (root ?? Data)?.Get<ExpandoObject>(MainName);

    // the id the save gave the record this model opened
    public Object IdOf(ExpandoObject saved) => Main(saved)?.Get<Object>("Id")
        ?? throw new InvalidOperationException($"The save returned no '{MainName}.Id'");

    /* What to save: this model with the fields laid over its record. It crosses JSON on its way back,
     * as it does between the browser and the server.
     */
    public ExpandoObject With(IEnumerable<KeyValuePair<String, Object?>> fields)
    {
        var root = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(Newtonsoft.Json.JsonConvert.SerializeObject(Data))!;
        var element = (root.Get<ExpandoObject>(MainName) as IDictionary<String, Object?>)
            ?? throw new InvalidOperationException($"The model has no '{MainName}'");
        foreach (var (key, value) in fields)
            element[key] = value;
        return root;
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

    /* The rows alone, whatever the screen shows beside them: 'index' of a catalog with folders is the
     * tree and an empty collection, the rows come with its lazy loads. A caller with no screen reads this.
     */
    public async Task<DataResult> ListAsync(IndexQuery query, ExpandoObject filters)
    {
        var url = $"{_route}/indexpartial/0?{CreateIndexQuery(query, filters)}";
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
    public async Task<DataResult> CreateAsync(IReadOnlyDictionary<String, String>? query = null)
    {
        var url = $"{_route}/edit/new";
        if (query?.Count > 0)
            url += $"?{String.Join("&", query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}"))}";
        var model = ModelOf(await _dataService.LoadAsync(_kind.Kind, url, prms => { }, isReload: !_render));
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

    // the Delete of a screen: the record is voided, the row stays
    public Task VoidAsync(Object id) => _dataService.DbRemoveAsync($"{_kind.Prefix}/{_route}/index/0", id, null, prms => { });

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
                yield return $"SortOrder={Uri.EscapeDataString(query.Sort)}";
            yield return $"SortDir={(query.Desc ? "desc" : "asc")}";
            // escaped: an '&' in a value would otherwise set another parameter of the query
            var d = filters as IDictionary<String, Object>;
            foreach (var x in d.Keys)
                yield return $"{Uri.EscapeDataString(x)}={Uri.EscapeDataString($"{d[x]}")}";
        }
        return String.Join("&", Params());
    }
}
