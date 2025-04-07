// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(
    IServiceProvider _serviceProvider, 
    IDbContext _dbContext,
    DatabaseMetadataProvider _metadataProvider, 
    ICurrentUser _currentUser) : IModelBuilder
{
    protected TableMetadata _table { get; private set; } = default!;
    protected TableMetadata? _baseTable { get; private set; }
    protected AppMetadata _appMeta { get; private set; } = default!;
    protected String? _dataSource { get; private set; }
    protected IPlatformUrl _platformUrl { get; private set; } = default!;
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

    public async Task BuildAsync(IPlatformUrl platformUrl, IModelBase modelBase)
    {
        if (modelBase.Meta == null)
            throw new InvalidOperationException("Meta is null");
        _dataSource = modelBase.DataSource;
        _platformUrl = platformUrl;
        _table = await _metadataProvider.GetSchemaAsync(modelBase.Meta, modelBase.DataSource);
        await CheckParent(modelBase.DataSource);
        _appMeta = await _metadataProvider.GetAppMetadataAsync(modelBase.DataSource);
    }

    public async Task BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource)
    {
        _dataSource = dataSource;
        _table = table;
        _platformUrl = platformUrl;
        _table = await _metadataProvider.GetSchemaAsync(_dataSource, table.Schema, table.Name);
        await CheckParent(_dataSource);
        _appMeta = await _metadataProvider.GetAppMetadataAsync(_dataSource);
    }

    private async Task CheckParent(String? dataSource)
    {
        if (_table.ParentTable.IsEmpty()) return;
        _baseTable = _table;
        _table = await _metadataProvider.GetSchemaAsync(dataSource, _table.ParentTable!.RefSchema, _table.ParentTable.RefTable)
            ?? throw new InvalidOperationException($"Parent Table {_table.ParentTable.RefTable} not found");
    }

    public async Task<IDataModel> LoadModelAsync()
    {
        return Action switch
        {
            "browse" or "index" => await LoadIndexModelAsync(),
            "edit" => await LoadPlainModelAsync(),
            _ => throw new NotImplementedException($"Load model for {Action}")
        };
    }
    public async Task<String> CreateTemplateAsync()
    {
        return Action switch
        {
            "browse" or "index" => await CreateIndexTemplate(),
            "edit" => await CreateEditTemplate(),
            _ => throw new NotImplementedException($"Create template for {Action}")
        };
    }

    public Form CreateDefaultForm()
    {
        return Action switch
        {
            "browse" => CreateBrowseDialog(),
            "index" => CreateIndexPage(),
            "edit" => IsDialog ? CreateEditDialog() : CreateDocumentPage(),
            _ => throw new NotImplementedException($"Create form for {Action}")
        };
    }

    public Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        return Action switch
        {
            "edit" => SavePlainModelAsync(data, savePrms),
            _ => throw new NotImplementedException($"Create form for {Action}")
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
            var formMeta = await _metadataProvider.GetFormAsync(_dataSource, _baseTable ?? _table, _platformUrl.Action, CreateDefaultForm);
            page = formMeta.Page;
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

    protected DbParameterCollection AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
        return prms;
    }
    protected DbParameterCollection AddPeriodParameters(DbParameterCollection prms, ExpandoObject? qry)
    {
        if (!_table.HasPeriod())
            return prms;

        DateTime? DateTimeFromString(String? value)
        {
            if (value == null)
                return null;
            return DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture);    
        }

        return prms.AddDate("@From", DateTimeFromString(qry?.Get<String>("From")))
            .AddDate("@To", DateTimeFromString(qry?.Get<String>("To")));
    }

    protected String RefTableJoins(IEnumerable<(TableColumn Column, Int32 Index)> refFields, String alias)
    {
        var sb = new StringBuilder();
        foreach (var col in refFields)
        {
            var rc = col.Column.Reference ??
                throw new InvalidOperationException("Invalid Reference");
            sb.AppendLine($"""
                left join {rc.RefSchema}.[{rc.RefTable}] r{col.Index} on {alias}.[{col.Column.Name}] = r{col.Index}.[{_appMeta.IdField}]
            """);
        }
        return sb.ToString();
    }
}
