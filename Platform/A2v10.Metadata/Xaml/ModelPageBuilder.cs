// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Data.Interfaces;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class ModelPageBuilder(IServiceProvider _serviceProvider)
{
    private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly DatabaseMetadataProvider _dbMetaProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    private readonly CodeLoader _codeLoader = new(_serviceProvider);

    public async Task<String> RenderPageAsync(IPlatformUrl platformUrl, IModelView modelView, IDataModel dataModel, TableMetadata meta)
    {
        String rootId = $"el{Guid.NewGuid()}";
        String templateText = String.Empty;
        if (!String.IsNullOrEmpty(modelView.Template))
            templateText = await _codeLoader.GetTemplateScriptAsync(modelView);

        UIElement? page = null;
        var rawView = modelView.GetRawView(false);
        if (!String.IsNullOrEmpty(rawView))
        {
            page = _codeLoader.LoadPage(modelView, rawView);
        }
        else
        {
            var formMeta = await _dbMetaProvider.GetFormAsync(modelView.DataSource, meta, platformUrl.Action);
            page = formMeta.Page;
            templateText = formMeta.Template;
        }

        if (page == null)
            throw new InvalidOperationException("Page is null");

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = modelView,
            PlatformUrl = platformUrl,
            Model = dataModel,
            Template = templateText
        };
        return await _dynamicRenderer.RenderPage(rri);
    }
}
