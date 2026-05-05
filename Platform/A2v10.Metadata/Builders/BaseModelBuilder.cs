// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
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

#pragma warning disable IDE1006 // Naming Styles
    internal TableMetadata _table => descriptor.Table;
    internal TableMetadata? _baseTable => descriptor.BaseTable;
    internal AppMetadata _appMeta => descriptor.AppMeta;
    internal String? _dataSource => descriptor.DataSource;
    internal IPlatformUrl _platformUrl => descriptor.PlatformUrl;
    internal IEnumerable<ReferenceMember> _refFields => descriptor.RefFields;

    private SqlBuilder _sqlBuilder => new(descriptor, _serviceProvider);
    private Lazy<IndexModelBuilder> _indexBuilder => new(new IndexModelBuilder(this));
    private IndexModelBuilder _index => _indexBuilder.Value;
    private Lazy<PlainModelBuilder> _plainBuilder => new(new PlainModelBuilder(this));
    private PlainModelBuilder _plain => _plainBuilder.Value;
#pragma warning restore IDE1006 // Naming Styles
    protected Boolean IsDialog => descriptor.PlatformUrl.Kind == UrlKind.Dialog;
    protected String Action => descriptor.PlatformUrl.Action.ToLowerInvariant();

    public TableMetadata Table => _table;
    public TableMetadata? BaseTable => _baseTable;
    public AppMetadata AppMeta => _appMeta;

    public String? MetadataEndpointBuilder => _baseTable?.Schema switch
    {
        "rep" => "rep:report.render",
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
        return _index.DbRemoveAsync(propName, execPrms);
    }

    public async Task<IDataModel> LoadModelAsync()
    {
        return Action switch
        {
            "browse" or "index" => _table.UseFolders
                ? await _sqlBuilder.LoadIndexTreeModelAsync()
                : await _sqlBuilder.LoadIndexModelAsync(),
            "edit" => await _plain.LoadPlainModelAsync(),
            "browsefolder" => await _sqlBuilder.LoadBrowseTreeModelAsync(),
            "editfolder" => await _sqlBuilder.LoadEditFolderModelAsync(),
            _ => throw new NotImplementedException($"Load model for {Action}")
        };
    }
    public async Task<String> CreateTemplateAsync()
    {
        return Action switch
        {
            "browse" or "index" => await _index.CreateIndexTemplate(),
            "edit" => await _plain.CreateEditTemplate(),
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create template for {Action}")
        };
    }
    public async Task<String> CreateTemplateTSAsync()
    {
        return Action switch
        {
            "index" => await _index.CreateIndexTSTemplate(),
            "edit" => await _plain.CreateEditTSTemplate(),
            "browse" => String.Empty,
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }
    public async Task<String> CreateMapTSAsync()
    {
        return Action switch
        {
            "index" => await _index.CreateMapTS(),
            "edit" => await _plain.CreateMapTS(),
            "browse" => String.Empty,   
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }

    public static String EnumsMapSql(IEnumerable<ReferenceMember> refs, Boolean isFilter)
    {
        var sb = new StringBuilder();
        var where = isFilter ? "" : " where e.[Id] <> N''";
        foreach (var r in refs.Where(c => c.Column.Type == ColumnType.Enum))
        {
            sb.AppendLine($"""
                select [{r.Table.RealItemsName}!TR{r.Table.RealItemName}!Map] = null, [Id!!Id] = e.Id, [Name!!Name] = e.[Name]
                from {r.Table.SqlTableName} e
                {where}
                order by e.[Order];
                """);
        }
        return sb.ToString();
    }

    public UIElement CreateDefaultXamlForm()
    {
        return Action switch
        {
            "browse" => _index.CreateBrowseDialogXaml(),
            "index" => _index.CreateIndexPageXaml(),
            "edit" => IsDialog ? _plain.CreateEditDialogXaml() : _plain.CreateDocumentPageXaml(),
            //"browsefolder" => _index.CreateBrowseTreeDialogXaml(),
            _ => throw new NotImplementedException($"Create form for {Action}")
        };
    }
    public Form CreateDefaultForm()
    {
        return Action switch
        {
            "browse" => _index.CreateBrowseDialog(),
            "index" => _index.CreateIndexPage(),
            "edit" => IsDialog ? _plain.CreateEditDialog() : _plain.CreateDocumentPage(),
            "browsefolder" => _index.CreateBrowseTreeDialog(),
            _ => throw new NotImplementedException($"Create form for {Action}")
        };
    }

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return Action switch
        {
            "edit" => _plain.SavePlainModelAsync(data, savePrms),
            _ => throw new NotImplementedException($"Save Model Async for {Action}")
        };
    }

    public Task<FormMetadata> GetFormAsync()
    { 
        return _metadataProvider.GetFormAsync(descriptor.DataSource, _baseTable ?? _table, Action, CreateDefaultForm);
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
            // var formMeta = await _metadataProvider.GetFormAsync(descriptor.DataSource, _baseTable ?? _table, descriptor.PlatformUrl.Action, CreateDefaultForm);
            page = await _metadataProvider.GetXamlFormAsync(descriptor.DataSource, _baseTable ?? _table, descriptor.PlatformUrl.Action, CreateDefaultXamlForm);
            //page = XamlBulder.BuildForm(formMeta.Form);
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
