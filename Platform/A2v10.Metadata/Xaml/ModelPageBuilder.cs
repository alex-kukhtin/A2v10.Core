// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.IO;
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
    private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
    private readonly IXamlPartProvider _xamlPartProvider = _serviceProvider.GetRequiredService<IXamlPartProvider>();
    private readonly DatabaseMetadataProvider _dbMetaProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();

    public async Task<String> RenderPageAsync(IPlatformUrl platformUrl, IModelView modelView, IDataModel dataModel, TableMetadata meta)
    {
        String rootId = $"el{Guid.NewGuid()}";
        String templateText = String.Empty;
        if (!String.IsNullOrEmpty(modelView.Template))
            templateText = await GetTemplateScriptAsync(modelView);

        UIElement? page = null;
        var rawView = modelView.GetRawView(false);
        if (!String.IsNullOrEmpty(rawView))
        {
            page = LoadPage(modelView, rawView);
        }
        else
        {
            if (platformUrl.Action.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                var form = MergeIndexForm(await _dbMetaProvider.GetFormAsync(modelView.DataSource, meta.Schema, meta.Name, platformUrl.Action), meta);
                page = CreateIndexPage(platformUrl, modelView, form);
            }
            else if (modelView.IsDialog && platformUrl.Action == "edit")
            {
                page = CreateEditDialog(platformUrl, modelView, meta);
            }
            else if (modelView.IsDialog && platformUrl.Action == "browse")
            {
                var form = MergeIndexForm(await _dbMetaProvider.GetFormAsync(modelView.DataSource, meta.Schema, meta.Name, platformUrl.Action), meta);
                page = CreateBrowseDialog(platformUrl, modelView, meta);
            }
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

    private async Task<String> GetTemplateScriptAsync(IModelView view)
    {
        if (view.Path == null)
            throw new InvalidOperationException("Model.Path is null");
        var pathToRead = _codeProvider.MakePath(view.Path, $"{view.Template}.js");
        using var stream = _codeProvider.FileStreamRO(pathToRead)
            ?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
        using var sr = new StreamReader(stream);
        var fileTemplateText = await sr.ReadToEndAsync() ??
            throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
        return fileTemplateText;
    }

    private UIElement LoadPage(IModelView modelView, String viewName)
    {
        var path = _codeProvider.MakePath(modelView.Path, viewName + ".xaml");
        var obj = _xamlPartProvider.GetXamlPart(path);
        if (obj is UIElement uIElement)
            return uIElement;
        throw new InvalidOperationException("Xaml. Root is not an IXamlElement");
    }

    Form MergeIndexForm(Form? form, TableMetadata meta)
    {
        var metaColumns = meta.Columns.Select(c => 
        {
            var sf = c.IsParent ? "Elem" : string.Empty;
            var col = new FormColumn()
            {
                // TODO: .Name заменить на appMeta.NameField
                Path = c.IsReference ? $"{c.Name}.Name" : c.Name,
                Header = $"@[{c.Name}]",
                Clamp = c.MaxLength >= 255 ? 2 : 0,
                Role = c.DataType.ToColumnRole(c.IsReference),
                BindDataType = c.DataType.ToBindDataType(),
                SortProperty = c.IsReference ? c.Name : null
            };
            return col;
         });

        var fc = form?.Columns;
        return new Form()
        {
            Title = form?.Title ?? $"@[{meta.Name}]",
            Columns = fc != null && fc.Count > 0 ? fc : metaColumns.ToList()
        };
    }

}
