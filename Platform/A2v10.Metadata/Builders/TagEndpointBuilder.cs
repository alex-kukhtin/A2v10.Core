// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class TagEndpointBuilder(IServiceProvider _serviceProvider, TagEndpointMetadata _endpoint,
    IPlatformUrl _platformUrl, String? _dataSource, AppPlatformId _platformId, Boolean _useGrants) : IModelBuilder
{
    private readonly SqlBuilderTags _sqlBuilder = new(_serviceProvider, _platformId, _useGrants);
    private readonly TagsTemplateBuilder _templateBuilder = new();
    private readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();

    public String Path => _endpoint.Path;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        var dm = await _sqlBuilder.LoadTagsModel(_dataSource, await ScreenAsync());
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, _templateBuilder.CreateIndexTemplate());
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }

    public async Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return await _sqlBuilder.SaveModelAsync(_dataSource, data, await ScreenAsync());
    }

    /* The screen the dialog was opened from. The dictionary is the model's, one for all its addresses;
     * a right is held on an address, so the screen answers who may change it, and each address answers
     * for itself. The model comes off the endpoint: nothing the client sent reaches the SQL text.
     */
    internal async Task<NormalEndpointMetadata> ScreenAsync()
    {
        if (_platformUrl.Action != TagEndpointMetadata.SettingsAction)
            throw new InvalidOperationException($"Tags. Unsupported action '{_platformUrl.Action}'");

        var address = _platformUrl.Query?.Get<String>(Constants.FieldNames.BaseQuery)
            ?? throw new InvalidOperationException($"Tags. '?{Constants.FieldNames.BaseQuery}=' is missing - the address of the screen the dialog is opened from");

        var (schema, name) = DatabaseMetadataProvider.ParsePath(address);
        var screen = await _metadataProvider.GetNormalEndpointAsync(_dataSource, schema, name);
        if (!screen.Storage.HasTags)
            throw new InvalidOperationException($"Tags. {screen.Path} has no 'Tags' trait");
        return screen;
    }
}

