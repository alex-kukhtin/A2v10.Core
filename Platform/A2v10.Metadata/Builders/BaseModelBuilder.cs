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
using A2v10.Data;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(IServiceProvider _serviceProvider, BuilderDescriptor descriptor) : IModelBuilder
{
    internal readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    internal readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    internal readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    internal readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    private readonly SqlBuilder _sqlBuilder = new(descriptor, _serviceProvider);
    private readonly XamlBuilder _xamlBuilder = new(descriptor); 
    // types erased: the browser runs this text as it is, there is no compiler here
    private readonly ScriptBuilder _jsBuilder = new(descriptor, isTs: false);

    protected Boolean IsDialog => descriptor.PlatformUrl.Kind == UrlKind.Dialog;
    protected String Action => descriptor.PlatformUrl.Action.ToLowerInvariant();

    // off the interface now - both are this class's own business, and nobody outside asked
    public NormalEndpointMetadata Endpoint => descriptor.Endpoint;
    public TableMetadata Table => descriptor.Endpoint.Storage;
    public String Path => descriptor.Endpoint.Path;

    /* The load/render split that used to live in AppMetadataBuilder. It belongs here: it is the
     * shape of THIS builder's work, and the caller has no business knowing there are two halves.
     */
    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        var dm = await LoadModelAsync();
        if (isReload)
            return new AppRuntimeResult(dm, null);
        return new AppRuntimeResult(dm, await RenderPageAsync(view, dm));
    }

    public Task<IDataModel> LoadLazyModelAsync()
    {
        return _sqlBuilder.LoadIndexModelAsync(true);
    }

    // no tree here expands on demand: the folders and the chart of accounts are read whole
    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms) =>
        throw new InvalidOperationException($"Expand. {Path}: a tree is read whole, nothing expands on demand");

    public Task DbRemoveAsync(String? propName, ExpandoObject execPrms)
    {
        return _sqlBuilder.DbRemoveAsync(propName, execPrms);
    }

    public async Task<IDataModel> LoadModelAsync()
    {
        return Action switch
        {
            "index" or "browse" when Table.Kind == EndpointKind.AccPlan => await _sqlBuilder.LoadAccountTreeModelAsync(Action == "browse"),
            "browse" or "index" or "indexpartial" => Table.HasFolders
                ? await _sqlBuilder.LoadIndexTreeModelAsync()
                : await _sqlBuilder.LoadIndexModelAsync(),
            "edit" or "show" => await _sqlBuilder.LoadPlainModelAsync(),
            Constants.Trans.Action => await _sqlBuilder.LoadTransModelAsync(),
            Constants.Print.Action => await _sqlBuilder.LoadPrintPageModelAsync(),
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
            // read-only, and the one piece of state it has is a field of its own model
            Constants.Trans.Action => String.Empty,
            /* One computed property and nothing else: the blank is drawn by a report engine, so the
             * page has no fields to bind - what it needs is the address to point the viewer at, and
             * that address carries the chosen blank, which only the query knows.
             */
            Constants.Print.Action => await _jsBuilder.CreatePrintTemplate(),
            "browsefolder" => String.Empty,
            "editfolder" => await _jsBuilder.CreateEditFolderTemplate(),
            _ => throw new NotImplementedException($"Create template for {Action}")
        };
    }

    public UIElement CreateDefaultXamlForm()
        => _xamlBuilder.CreateXamlContainer(Action);

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return Action switch
        {
            "edit" => _sqlBuilder.SavePlainModelAsync(data, savePrms),
            "editfolder" => _sqlBuilder.SaveFolderModelAsync(data),
            _ => throw new NotImplementedException($"Save Model Async for {Action}")
        };
    }

    public async Task<String> RenderPageAsync(IModelView modelView, IDataModel dataModel)
    {
        var codeLoader = new CodeLoader(_serviceProvider);
        var dynamicRenderer = new DynamicRenderer(_serviceProvider);

        String rootId = $"el{Guid.NewGuid()}";

        /* Each of the two taken on its own: written in model.json, the file; absent, generated. The
         * generated template used to be the ELSE of the view, so a hand-written template under a
         * generated view was read and then overwritten. See CLAUDE.md, "Two files in one folder".
         */
        var templateText = !String.IsNullOrEmpty(modelView.Template)
            ? await codeLoader.GetTemplateScriptAsync(modelView)
            : await CreateTemplateAsync();

        var rawView = modelView.GetRawView(false);
        UIElement page;
        if (!String.IsNullOrEmpty(rawView))
            page = codeLoader.LoadPage(modelView, rawView);
        else
            /* The print page is not cached: it depends on '?Form=', which the cache key does not
             * carry - keyed by action, the first blank opened answered for every other.
             */
            page = Action == Constants.Print.Action
                ? CreateDefaultXamlForm()
                : await _metadataProvider.GetXamlFormAsync(descriptor.DataSource, Endpoint, descriptor.PlatformUrl.Action, CreateDefaultXamlForm);

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
