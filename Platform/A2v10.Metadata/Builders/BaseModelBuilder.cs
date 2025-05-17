// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;
using System.Text;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder(IServiceProvider _serviceProvider) : IModelBuilder
{
    protected readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    protected readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    protected readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    internal TableMetadata _table { get; init; } = default!;
    internal TableMetadata? _baseTable { get; init; }
    internal AppMetadata _appMeta { get; init; } = default!;
    internal String? _dataSource { get; init; }
    internal IPlatformUrl _platformUrl { get; init; } = default!;
    protected Boolean IsDialog => _platformUrl.Kind == UrlKind.Dialog;
    protected String Action => _platformUrl.Action.ToLowerInvariant();
    internal IEnumerable<ReferenceMember> _refFields { get; init; } = default!;

    public TableMetadata Table => _table;
    public TableMetadata? BaseTable => _baseTable;
    public AppMetadata AppMeta => _appMeta;

    public String? MetadataEndpointBuilder => _baseTable?.Schema switch
    {
        "rep" => "rep:report.render",
        _ => null
    };

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
            page = XamlBulder.BuildForm(formMeta.form);
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

    protected String RefTableJoins(IEnumerable<ReferenceMember> refFields, String alias)
    {
        return String.Join("\n", refFields.Select(refField =>
        {
            var enumWhere = "";
            if (refField.Table.IsEnum)
                enumWhere = $" and r{refField.Index}.[{refField.Table.PrimaryKeyField}] <> N''";
            return $"    left join {refField.Table.SqlTableName} r{refField.Index} on {alias}.[{refField.Column.Name}] = r{refField.Index}.[{refField.Table.PrimaryKeyField}]{enumWhere}";
        }));
    }
}
