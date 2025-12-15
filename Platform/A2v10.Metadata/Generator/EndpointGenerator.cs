// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Services;
using A2v10.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal class EndpointGenerator(IModelBuilderFactory _modelBuilderFactory, IAppCodeProvider _appCodeProvider) : IEndpointGenerator
{
    public async Task BuildEndpointAsync(TableMetadata table)
    {
        await GenerateModelJsonAsync(table);
        await GenerateIndexAsync(table);
        await GenerateEditAsync(table);
        await GenerateBrowseAsync(table);
    }

    private Task GenerateModelJsonAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("index");
        var md = new ModelJsonD()
        {
            Schema = table.Schema,
            Meta = new DatabaseMetaD()
            {
                Table = table.Name
            },
            Actions = new Dictionary<String, ModelJsonViewD>() { 
                { "index", new ModelJsonViewD()
                    {
                        Index = true,
                        Template = "index.template",
                        View = "index.view"
                    }
                }
            },
            Dialogs = new Dictionary<String, ModelJsonViewD>()
        };

        if (table.EditWith == EditWithMode.Dialog)
        {
            md.Dialogs.Add("edit", new ModelJsonViewD()
            {
                Template = "edit.template",
                View = "edit.dialog"
            });
        }
        else
        {
            md.Actions.Add("edit", new ModelJsonViewD()
            {
                Template = "edit.template",
                View = "edit.view"
            });
        }

        md.Dialogs.Add("browse", new ModelJsonViewD()
        {
            Index = true,
            Template = "index.template",
            View = "browse.dialog"
        });

        var modelJsonContent = JsonConvert.SerializeObject(md, JsonSettings.CamelCaseSerializerSettingsFormat);

        var fullPath = _appCodeProvider.GetMainModuleFullPath(platformUrl.LocalPath.RemoveHeadSlash(), $"model.json");
        return WriteFileAsync(fullPath, modelJsonContent);
    }

    private async Task GenerateIndexAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("index");
        await GenerateFormAsync(table, platformUrl);
        // GenerateIndexTemplate(); //index.template.ts
        // GenerateIndexData(); // index.d.ts

    }
    private async Task GenerateEditAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("edit");
        await GenerateFormAsync(table, platformUrl);
        // GenerateEditTemplate(); // edit.template.ts
        // GenerateEditData();     // edit.d.ts
    }

    private async Task GenerateBrowseAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("browse");
        await GenerateFormAsync(table, platformUrl);
    }

    private async Task GenerateFormAsync(TableMetadata table, IPlatformUrl platformUrl)
    {
        var builder = await _modelBuilderFactory.BuildAsync(platformUrl, table, null);
        var formIndex = await builder.GetFormAsync();
        var pageIndex = XamlBulder.BuildForm(formIndex.Form);
        var pageXaml = XamlBulder.GetXaml(pageIndex);

        var fileType = platformUrl.Kind == UrlKind.Dialog ? "dialog" : "view";
        var fullPath = _appCodeProvider.GetMainModuleFullPath(platformUrl.LocalPath.RemoveHeadSlash(), $"{platformUrl.Action}.{fileType}.xaml");
        await WriteFileAsync(fullPath, pageXaml);
    }

    private Task WriteFileAsync(String fullPath, String content)
    {
        var dir =  Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return File.WriteAllBytesAsync(fullPath, Encoding.UTF8.GetBytes(content));
    }
}
