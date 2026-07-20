// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;
using A2v10.Data.Core.Extensions;


namespace A2v10.Services.Api;


public sealed class DataResult
{
    public ExpandoObject? Data { get; init; }
    public ExpandoObject? Metadata { get; init; }
}

public class ApiDataService(IDataService _dataService)
{
    public async Task<DataResult> IndexAsync(String route, IndexQuery query, ExpandoObject filters)
    {
        var url = $"{route}/index/0";
        var iq = CreateIndexQuery(query, filters);
        if (!String.IsNullOrEmpty(iq))
            url += $"?{iq}";
        var lr = await _dataService.LoadAsync(UrlKind.Page, url, prms => { });
        return new DataResult()
        {
            Data = lr.Model?.Root.FitModelInfo() ?? throw new InvalidOperationException("Model is null"),
            Metadata = lr.Model.BuildDataModelMeta()
        };
    }

    public async Task<DataResult> LoadAsync(String route, String id)
    {
        var url = $"{route}/edit/{id}";
        var segments = route.Split("/");
        if (segments.Length < 1)
            throw new InvalidOperationException($"Invalid route '{route}'");
        var kind = segments[0] switch
        {
            "catalog" => UrlKind.Dialog,
            "document" => UrlKind.Page,
            _ => throw new NotImplementedException($"Edit for {segments[0]}")
        };
        var lr = await _dataService.LoadAsync(kind, url, prms => { });
        return new DataResult()
        {
            Data = lr.Model?.Root ?? throw new InvalidOperationException("Model is null"),
            Metadata = lr.Model.BuildDataModelMeta()
        };
    }

    public async Task<DataResult> CreateAsync(String route)
    {
        var url = $"{route}/edit/0";
        var segments = route.Split("/");
        if (segments.Length < 1)
            throw new InvalidOperationException($"Invalid route '{route}'");
        var kind = segments[0] switch
        {
            "catalog" => UrlKind.Dialog,
            "document" => UrlKind.Page,
            _ => throw new NotImplementedException($"Edit for {segments[0]}")
        };
        var lr = await _dataService.LoadAsync(kind, url, prms => { });
        return new DataResult()
        {
            Data = lr.Model?.BuildNewInstance() ?? throw new InvalidOperationException("Model is null"),
            Metadata = lr.Model.BuildDataModelMeta()
        };
    }

    public async Task<ExpandoObject> SaveAsync(String route, ExpandoObject data)
    {
        var segments = route.Split("/");
        if (segments.Length < 1)
            throw new InvalidOperationException($"Invalid route '{route}'");
        var kind = segments[0] switch
        {
            "catalog"  => "_dialog",
            "document" => "_page",
            _ => throw new NotImplementedException($"Save for {segments[0]}")
        };
        var url = $"{kind}/{route}/edit/0";
        var lr = await _dataService.SaveAsync(url, data, prms => { });
        return lr.Raw;
    }

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
