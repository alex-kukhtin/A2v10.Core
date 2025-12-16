// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(IServiceProvider _serviceProvider) : IModelBuilder
{
    internal readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    internal readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    internal readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
#pragma warning disable IDE1006 // Naming Styles
    internal TableMetadata _table { get; init; } = default!;
    internal TableMetadata? _baseTable { get; init; }
    internal AppMetadata _appMeta { get; init; } = default!;
    internal String? _dataSource { get; init; }
    internal IPlatformUrl _platformUrl { get; init; } = default!;
    internal IEnumerable<ReferenceMember> _refFields { get; init; } = default!;
    private Lazy<IndexModelBuilder> _indexBuilder => new(new IndexModelBuilder(this));
    private IndexModelBuilder _index => _indexBuilder.Value;
    private Lazy<PlainModelBuilder> _plainBuilder => new(new PlainModelBuilder(this));
    private PlainModelBuilder _plain => _plainBuilder.Value;
#pragma warning restore IDE1006 // Naming Styles
    protected Boolean IsDialog => _platformUrl.Kind == UrlKind.Dialog;
    protected String Action => _platformUrl.Action.ToLowerInvariant();

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
        return _index.LoadIndexModelAsync(true);
    }

    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms)
    {
        return _index.ExpandAsync(expandPrms);
    }

    public async Task<IDataModel> LoadModelAsync()
    {
        return Action switch
        {
            "browse" or "index" => _table.UseFolders
                ? await _index.LoadIndexTreeModelAsync()
                : await _index.LoadIndexModelAsync(),
            "edit" => await _plain.LoadPlainModelAsync(),
            "browsefolder" => await _index.LoadBrowseTreeModelAsync(),
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
            "browse" or "index" => await _index.CreateIndexTSTemplate(),
            "edit" => await _plain.CreateEditTSTemplate(),
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }
    public async Task<String> CreateMapTSAsync()
    {
        return Action switch
        {
            "browse" or "index" => await _index.CreateMapTS(),
            "edit" => await _plain.CreateMapTS(),
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }

    public static String EnumsMapSql(IEnumerable<ReferenceMember> refs, Boolean isFilter)
    {
        var sb = new StringBuilder();
        var where = isFilter ? "" : " where e.[Id] <> N''";
        foreach (var r in refs.Where(c => c.Column.DataType == ColumnDataType.Enum))
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
            _ => throw new NotImplementedException($"Create form for {Action}")
        };
    }

    public Task<FormMetadata> GetFormAsync()
    { 
        return _metadataProvider.GetFormAsync(_dataSource, _baseTable ?? _table, Action, CreateDefaultForm);
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
            var formMeta = await _metadataProvider.GetFormAsync(_dataSource, _baseTable ?? _table, _platformUrl.Action, CreateDefaultForm);
            page = XamlBulder.BuildForm(formMeta.Form);
            templateText = await CreateTemplateAsync();
        }
        if (page == null)
            throw new InvalidOperationException("Page is null");

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(_platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = modelView,
            PlatformUrl = _platformUrl,
            Model = dataModel,
            Template = templateText
        };
        return await dynamicRenderer.RenderPage(rri);
    }
}
