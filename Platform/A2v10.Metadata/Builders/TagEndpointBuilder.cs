// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class TagEndpointBuilder(IServiceProvider _serviceProvider, TagEndpointMetadata _endpoint,
    IPlatformUrl _platformUrl, String? _dataSource, AppPlatformId _platformId) : IModelBuilder
{
    private readonly SqlBuilderTags _sqlBuilder = new(_serviceProvider, _platformId);
    private readonly TagsTemplateBuilder _templateBuilder = new();

    public String Path => _endpoint.Path;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        var dm = await _sqlBuilder.LoadTagsModel(_dataSource, TagsFor());
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, _templateBuilder.CreateIndexTemplate());
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return _sqlBuilder.SaveModelAsync(_dataSource, data, TagsFor());
    }

    /* Both entry points ask the same three questions, and both let 'For' through to SQL - so the
     * check cannot live on one path only. Letters and digits and nothing else: it names an entity,
     * and anything else is a request that was never going to match a row.
     */
    private String TagsFor()
    {
        if (_platformUrl.Action != TagEndpointMetadata.SettingsAction)
            throw new InvalidOperationException($"Tags. Unsupported action '{_platformUrl.Action}'");

        var tagsFor = _platformUrl.Query?.Get<String>(Constants.FieldNames.For)
            ?? throw new InvalidOperationException("Tags. For is null");

        foreach (var ch in tagsFor)
            if (!Char.IsLetterOrDigit(ch))
                throw new InvalidOperationException($"Invalid for '{tagsFor}'");
        return tagsFor;
    }
}

