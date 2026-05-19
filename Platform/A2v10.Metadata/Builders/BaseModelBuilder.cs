// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(IServiceProvider _serviceProvider, BuilderDescriptor descriptor) : IModelBuilder
{
    internal readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    internal readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    internal readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    internal readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    private readonly SqlBuilder _sqlBuilder = new(descriptor, _serviceProvider);
    private readonly XamlBuilder _xamlBuilder = new(descriptor, _serviceProvider); 
    private readonly JavascriptBuilder _jsBuilder = new(descriptor);

    protected Boolean IsDialog => descriptor.PlatformUrl.Kind == UrlKind.Dialog;
    protected String Action => descriptor.PlatformUrl.Action.ToLowerInvariant();

    public TableMetadata Table => descriptor.Table;

    public String? MetadataEndpointBuilder => Table.Origin?.Schema switch
    {
        "report" => "rep:report.render",
        _ => null
    };
    public Task<IDataModel> LoadLazyModelAsync()
    {
        return _sqlBuilder.LoadIndexModelAsync(true);
    }

    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms)
    {
        return _sqlBuilder.ExpandAsync(expandPrms);
    }

    public Task DbRemoveAsync(String? propName, ExpandoObject execPrms)
    {
        return _sqlBuilder.DbRemoveAsync(propName, execPrms);
    }

    public async Task<IDataModel> LoadModelAsync()
    {
        return Action switch
        {
            "browse" or "index" or "indexpartial" => Table.UseFolders
                ? await _sqlBuilder.LoadIndexTreeModelAsync()
                : await _sqlBuilder.LoadIndexModelAsync(),
            "edit" => await _sqlBuilder.LoadPlainModelAsync(),
            "browsefolder" => await _sqlBuilder.LoadBrowseTreeModelAsync(),
            "editfolder" => await _sqlBuilder.LoadEditFolderModelAsync(),
            _ => throw new NotImplementedException($"Load model for {Action}")
        };
    }
    public async Task<String> CreateTemplateAsync()
    {
        return Action switch
        {
            "browse" or "index" or "indexpartial" => await _jsBuilder.CreateIndexTemplate(),
            "edit" => await _jsBuilder.CreateEditTemplate(),
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create template for {Action}")
        };
    }

    public UIElement CreateDefaultXamlForm()
    {
        return Action switch
        {
            "browse" => _xamlBuilder.CreateBrowseDialogXaml(),
            "index" => _xamlBuilder.CreateIndexPageXaml(),
            "indexpartial" => _xamlBuilder.CreateIndexPagePartialXaml(),
            "edit" => IsDialog ? _xamlBuilder.CreateEditDialogXaml() : _xamlBuilder.CreateDocumentPageXaml(),
            //"browsefolder" => _index.CreateBrowseTreeDialogXaml(),
            _ => throw new NotImplementedException($"Create form for {Action}")
        };
    }

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return Action switch
        {
            "edit" => _sqlBuilder.SavePlainModelAsync(data, savePrms),
            _ => throw new NotImplementedException($"Save Model Async for {Action}")
        };
    }

    public async Task<String> RenderPageAsync(IModelView modelView, IDataModel dataModel)
    {
        var codeLoader = new CodeLoader(_serviceProvider);
        var dynamicRenderer = new DynamicRenderer(_serviceProvider);

        String rootId = $"el{Guid.NewGuid()}";
        String templateText = String.Empty;
        if (!String.IsNullOrEmpty(modelView.Template))
            templateText = await codeLoader.GetTemplateScriptAsync(modelView);

        UIElement? page = null;
        var rawView = modelView.GetRawView(false);
        if (!String.IsNullOrEmpty(rawView))
        {
            page = codeLoader.LoadPage(modelView, rawView);
        }
        else
        {
            page = await _metadataProvider.GetXamlFormAsync(descriptor.DataSource, Table.Origin ?? Table, descriptor.PlatformUrl.Action, CreateDefaultXamlForm);
            templateText = await CreateTemplateAsync();
        }
        if (page == null)
            throw new InvalidOperationException("Page is null");

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(descriptor.PlatformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = modelView,
            PlatformUrl = descriptor.PlatformUrl,
            Model = dataModel,
            Template = templateText
        };
        return await dynamicRenderer.RenderPage(rri);
    }
}
