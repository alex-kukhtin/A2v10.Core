// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;
using A2v10.Data;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace A2v10.Metadata;

public class EditFormEndpointHandler(IServiceProvider _serviceProvider) : IEndpointHandler
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly CodeLoader _codeLoader = new(_serviceProvider);
    private readonly IModelBuilderFactory _modelBuilderFactory = _serviceProvider.GetRequiredService<IModelBuilderFactory>();
    private readonly DatabaseMetadataProvider _dbMetadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    public async Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        var key = prms.Get<String>("Form")
              ?? throw new InvalidOperationException("Form is null");
        var dbPrms = new ExpandoObject()
        {
            {"Id", platformUrl.Id },
            {"Key", key },
            {"WithColumns", true }
        };

        var dataModel = await _dbContext.LoadModelAsync(modelView.DataSource, "a2meta.[Table.Form]", dbPrms);

        var eo = dataModel.Eval<Object>("Form.Json");
        if (eo == null)
        {
            var table = dataModel.Eval<String>("Table.Name")
                ?? throw new InvalidOperationException("Table.Name is null");
            var schema = dataModel.Eval<String>("Table.Schema")
                ?? throw new InvalidOperationException("Table.Schema is null");

            var ew = dataModel.Eval<String>("Table.EditWith")
                ?? throw new InvalidOperationException("Table.EditWith is null");

            var tm = await _dbMetadataProvider.GetSchemaAsync(modelView.DataSource, schema, table);

            var editPlatformUrl = platformUrl.CreateFromMetadata(tm.LocalPath(key));

            var modelBuilder = await _modelBuilderFactory.BuildAsync(editPlatformUrl, tm, null /*always*/);

            var defaultForm = modelBuilder.CreateDefaultForm();

            // with null for Form Designer
            var json = JsonConvert.SerializeObject(defaultForm, JsonSettings.WithNull);
            dbPrms.Add("Json", json);
            // update default form
            dataModel = await _dbContext.LoadModelAsync(modelView.DataSource, "a2meta.[Table.Form.Update]", dbPrms);
        }

        var formDm = CreateFormDataModel(dataModel.Root);

        String rootId = $"el{Guid.NewGuid()}";
        String templateText = String.Empty;
        if (!String.IsNullOrEmpty(modelView.Template))
            templateText = await _codeLoader.GetTemplateScriptAsync(modelView);

        var rawView = modelView.GetRawView(false);
        if (String.IsNullOrEmpty(rawView))
            throw new InvalidOperationException("View not found");
        UIElement page = _codeLoader.LoadPage(modelView, rawView);

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = modelView,
            PlatformUrl = platformUrl,
            Model = formDm, //dataModel,
            Template = templateText
        };
        return await _dynamicRenderer.RenderPage(rri);
    }

    public Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        throw new NotImplementedException("RELOAD FORM");
    }

    public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms)
    {
        var eo = data.Eval<ExpandoObject>("Form.Json");
        var id = data.Eval<Object>("Form.Id");
        var key = data.Eval<String>("Form.Key");

        // serialize with null for designer
        var json = JsonConvert.SerializeObject(eo);
        var form = JsonConvert.DeserializeObject<Form>(json, JsonSettings.Default);
        var jsonForSave = JsonConvert.SerializeObject(form, JsonSettings.IgnoreNull);

        var dbPrms = new ExpandoObject()
        {
            { "Id", id },
            { "Key", key },
            { "Json", jsonForSave }
        };
        await _dbContext.ExecuteExpandoAsync(modelView.DataSource, "a2meta.[Table.Form.Update]", dbPrms);
        return new();
    }


    private IDataModel CreateFormDataModel(ExpandoObject root)
    {
        var mb = new DataModelBuilder();

        ElementMetadata AddFormItemProps(ElementMetadata elem)
        {
            elem.AddField("Is", SqlDataType.String, 32)
            .AddField("Label", SqlDataType.String, 255)
            .AddField("Data", SqlDataType.String, 255)
            .AddField("Items", "TFormItemArray")
            .AddField("Width", SqlDataType.String, 255)
            .AddField("Height", SqlDataType.String, 255)
            .AddField("MinHeight", SqlDataType.String, 255)
            .AddField("CssClass", SqlDataType.String, 255)
            .AddField("If", SqlDataType.String, 255)
            .AddField("Grid", "TGrid")
            .AddField("Command", "TCommand")
            .AddField("Props", "TProp")
            .IsArrayType = true;
            return elem;
        }

        mb.AddMetadata("TTable")
            .AddField("Id", SqlDataType.Guid)
            .AddField("Schema", SqlDataType.String, 255)
            .AddField("Table", SqlDataType.String, 255)
            .AddField("EditWith", SqlDataType.String, 16);

        mb.AddMetadata("TFormWrap")
            .AddField("Id", SqlDataType.Guid)
            .AddField("Key", SqlDataType.String, 255)
            .AddField("Json", "TForm");

        AddFormItemProps(mb.AddMetadata("TForm"))
            .AddField("UseCollectionView", SqlDataType.Bit)
            .AddField("Schema", SqlDataType.String, 255)
            .AddField("Table", SqlDataType.String, 255)
            .AddField("Buttons", "TFormItemArray")
            .AddField("Taskpad", "TFormItem")
            .AddField("Toolbar", "TFormItem")
            .AddField("EditWith", SqlDataType.String, 16);

        AddFormItemProps(mb.AddMetadata("TFormItem"));

        mb.AddMetadata("TCommand")
            .AddField("Command", SqlDataType.String, 64)
            .AddField("Argument", SqlDataType.String, 255)
            .AddField("Url", SqlDataType.String, 255);

        mb.AddMetadata("TGrid")
            .AddField("Row", SqlDataType.Int)
            .AddField("Col", SqlDataType.Int)
            .AddField("RowSpan", SqlDataType.Int)
            .AddField("ColSpan", SqlDataType.Int);

        mb.AddMetadata("TProp")
            .AddField("Rows", SqlDataType.String, 255)
            .AddField("Columns", SqlDataType.String, 255)
            .AddField("Url", SqlDataType.String, 255)
            .AddField("Placeholder", SqlDataType.String, 255)
            .AddField("ShowClear", SqlDataType.Bit)
            .AddField("Style", SqlDataType.String, 64)
            .AddField("Filters", SqlDataType.String, 64)
            .AddField("Multiline", SqlDataType.Bit)
            .AddField("TabIndex", SqlDataType.Int)
            .AddField("LineClamp", SqlDataType.Int)
            .AddField("Fit", SqlDataType.Bit)
            .AddField("NoWrap", SqlDataType.Bit)
            .AddField("Required", SqlDataType.Bit)
            .AddField("ItemsSource", SqlDataType.String, 255);

        mb.AddMetadata("TColumn")
            .AddField("Id", SqlDataType.Guid)
            .AddField("Name", SqlDataType.String, 255)
            .AddField("Label", SqlDataType.String, 255)
            .AddField("DataType", SqlDataType.String, 64)
            .AddField("Reference", SqlDataType.String, 255)
            .IsArrayType = true;

        mb.AddMetadata("TRoot")
            .AddField("Table", "TTable")
            .AddField("Form", "TFormWrap")
            .AddField("Columns", "TColumnArray");

        return mb.CreateDataModel(root);
    }
}
