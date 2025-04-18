﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;

namespace A2v10.Metadata;

public class EditFormEndpointHandler(IServiceProvider _serviceProvider) : IEndpointHandler
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly CodeLoader _codeLoader = new(_serviceProvider);
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
            var tm = new TableMetadata() {
                Schema = schema,
                Name = table,
                EditWith = Enum.Parse<EditWithMode>(ew)
            };

            var editPlatformUrl = platformUrl.CreateFromMetadata(tm.LocalPath(key));

            var modelBuilder = _serviceProvider.GetRequiredService<IModelBuilder>();
            await modelBuilder.BuildAsync(editPlatformUrl, tm, null /*always*/);

            var defaultForm = modelBuilder.CreateDefaultForm();

            // with null for Form Designer
            var json = JsonConvert.SerializeObject(defaultForm, JsonSettings.WithNull);
            dbPrms.Add("Json", json);
            // update default form
            dataModel = await _dbContext.LoadModelAsync(modelView.DataSource, "a2meta.[Table.Form.Update]", dbPrms);
        }

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
            Model = dataModel,
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
        var form = JsonConvert.DeserializeObject<Form>(json);
        var jsonForSave = JsonConvert.SerializeObject(form, JsonSettings.WithNull);

        var dbPrms = new ExpandoObject()
        {
            { "Id", id },
            { "Key", key },
            { "Json", json }
        };
        await _dbContext.ExecuteExpandoAsync(modelView.DataSource, "a2meta.[Table.Form.Update]", dbPrms);
        return new();
    }
}
