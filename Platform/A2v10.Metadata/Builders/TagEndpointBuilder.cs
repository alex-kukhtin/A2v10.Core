// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;

using A2v10.Xaml;
using System.Dynamic;


namespace A2v10.Metadata;

internal class TagEndpointBuilder(IServiceProvider _serviceProvider, AppPlatformId _platformId) : IMetaEndpointBuilder
{
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly SqlBuilderTags _sqlBuilder = new(_serviceProvider, _platformId);
    private readonly XamlTagsBuilder _xamlBuilder = new();
    public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
    {
        var tagsFor = TagsFor(platformUrl);

        var dm = await _sqlBuilder.LoadTagsModel(view.DataSource, tagsFor);

        String rootId = $"el{Guid.NewGuid()}";

        UIElement page = _xamlBuilder.RenderSettingsDialog();

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = view,
            PlatformUrl = platformUrl,
            Model = dm
        };
        return new AppRuntimeResult(dm, await _dynamicRenderer.RenderPage(rri));
    }

    public Task<ExpandoObject> SaveModelAsync(IPlatformUrl platformUrl, String? dataSource, ExpandoObject data, ExpandoObject savePrms)
    {
        return _sqlBuilder.SaveModelAsync(dataSource, data, TagsFor(platformUrl));
    }

    /* Both entry points ask the same three questions, and both let 'For' through to SQL - so the
     * check cannot live on one path only. Letters and digits and nothing else: it names an entity,
     * and anything else is a request that was never going to match a row.
     */
    private static String TagsFor(IPlatformUrl platformUrl)
    {
        if (platformUrl.Action != TagEndpointMetadata.SettingsAction)
            throw new InvalidOperationException($"Tags. Unsupported action '{platformUrl.Action}'");

        var tagsFor = platformUrl.Query?.Get<String>(Constants.FieldNames.For)
            ?? throw new InvalidOperationException("Tags. For is null");

        foreach (var ch in tagsFor)
            if (!Char.IsLetterOrDigit(ch))
                throw new InvalidOperationException($"Invalid for '{tagsFor}'");
        return tagsFor;
    }
}

