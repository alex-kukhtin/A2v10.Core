// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Data;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(IServiceProvider _serviceProvider, BuilderDescriptor descriptor) : IModelBuilder
{
    private readonly SqlBuilder _sqlBuilder = new(descriptor, _serviceProvider);
    // types erased: the browser runs this text as it is, there is no compiler here
    private readonly ScriptBuilder _jsBuilder = new(descriptor, isTs: false);

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
        var request = new PageRequest(Endpoint, descriptor.PlatformUrl, descriptor.DataSource, view, dm, await TemplateAsync(view));
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }

    /* Taken apart from the view: written in model.json, the file; absent, generated. The generated
     * template used to be the ELSE of the view, so a hand-written template under a generated view was
     * read and then overwritten. See CLAUDE.md, "Two files in one folder".
     */
    async Task<String> TemplateAsync(IModelView view)
    {
        if (String.IsNullOrEmpty(view.Template))
            return await CreateTemplateAsync();
        if (view.Path == null)
            throw new InvalidOperationException("Model.Path is null");
        var codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
        var pathToRead = codeProvider.MakePath(view.Path, $"{view.Template}.js");
        using var stream = codeProvider.FileStreamRO(pathToRead)
            ?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
        using var sr = new StreamReader(stream);
        return await sr.ReadToEndAsync();
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
            Constants.Actions.Index or Constants.Actions.Browse when Table.Kind == TableKind.AccPlan
                => await _sqlBuilder.LoadAccountTreeModelAsync(Action == Constants.Actions.Browse),
            Constants.Actions.Browse or Constants.Actions.Index => Table.HasFolders
                ? await _sqlBuilder.LoadIndexTreeModelAsync()
                : await _sqlBuilder.LoadIndexModelAsync(),
            // embedded in another page: the list alone, whatever the owner's own screen shows beside it
            Constants.Actions.IndexPartial => await _sqlBuilder.LoadIndexModelAsync(),
            Constants.Actions.Edit or Constants.Actions.Show => await _sqlBuilder.LoadPlainModelAsync(),
            Constants.Trans.Action => await _sqlBuilder.LoadTransModelAsync(),
            Constants.Print.Action => await _sqlBuilder.LoadPrintPageModelAsync(),
            Constants.Actions.BrowseFolder => await _sqlBuilder.LoadBrowseTreeModelAsync(),
            Constants.Actions.EditFolder => await _sqlBuilder.LoadEditFolderModelAsync(),
            _ => throw Constants.Actions.Unknown(Action)
        };
    }
    public async Task<String> CreateTemplateAsync()
    {
        return Action switch
        {
            Constants.Actions.Browse or Constants.Actions.Index or Constants.Actions.IndexPartial => await _jsBuilder.CreateIndexTemplate(),
            Constants.Actions.Edit => await _jsBuilder.CreateEditTemplate(),
            Constants.Actions.Show => throw Constants.Actions.NotBuilt(Action, "template"),
            // read-only, and the one piece of state it has is a field of its own model
            Constants.Trans.Action => String.Empty,
            /* One computed property and nothing else: the blank is drawn by a report engine, so the
             * page has no fields to bind - what it needs is the address to point the viewer at, and
             * that address carries the chosen blank, which only the query knows.
             */
            Constants.Print.Action => await _jsBuilder.CreatePrintTemplate(),
            Constants.Actions.BrowseFolder => String.Empty,
            Constants.Actions.EditFolder => await _jsBuilder.CreateEditFolderTemplate(),
            _ => throw Constants.Actions.Unknown(Action)
        };
    }

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return Action switch
        {
            Constants.Actions.Edit => _sqlBuilder.SavePlainModelAsync(data, savePrms),
            Constants.Actions.EditFolder => _sqlBuilder.SaveFolderModelAsync(data),
            _ => throw IModelBuilder.NotHere($"save on '{Action}'", Path)
        };
    }
}
