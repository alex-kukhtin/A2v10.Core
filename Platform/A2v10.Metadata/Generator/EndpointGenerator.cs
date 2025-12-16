// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal record CreatedFile(String Path, Boolean Created);

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
                        Meta = new(),
                        Index = true,
                        Template = "index.template",
                        View = "index.view"
                    }
                }
            },
            Dialogs = []
        };

        if (table.EditWith == EditWithMode.Dialog)
        {
            md.Dialogs.Add("edit", new ModelJsonViewD()
            {
                Meta = new(),
                Template = "edit.template",
                View = "edit.dialog"
            });
        }
        else
        {
            md.Actions.Add("edit", new ModelJsonViewD()
            {
                Meta = new(),
                Template = "edit.template",
                View = "edit.view"
            });
        }

        md.Dialogs.Add("browse", new ModelJsonViewD()
        {
            Meta = new(),
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
        var formFile = await GenerateFormAsync(table, platformUrl);
        // GenerateIndexData(); // index.d.ts

    }
    private async Task GenerateEditAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("edit");
        var formFile = await GenerateFormAsync(table, platformUrl);
        // GenerateEditData();     // edit.d.ts
    }

    private async Task GenerateBrowseAsync(TableMetadata table)
    {
        var platformUrl = table.PlatformUrl("browse");
        await GenerateFormAsync(table, platformUrl);
    }

    private async Task<CreatedFile> GenerateFormAsync(TableMetadata table, IPlatformUrl platformUrl)
    {
        var fileType = platformUrl.Kind == UrlKind.Dialog ? "dialog" : "view";
        var fileName = $"{platformUrl.Action}.{fileType}.xaml";
        var fullPath = _appCodeProvider.GetMainModuleFullPath(platformUrl.LocalPath.RemoveHeadSlash(), fileName);
        var filePath = Path.Combine(fullPath, fileName);

        //if (File.Exists(fullPath))
            //return new CreatedFile(filePath, false);
        var builder = await _modelBuilderFactory.BuildAsync(platformUrl, table, null);
        var formIndex = await builder.GetFormAsync();
        var pageIndex = XamlBulder.BuildForm(formIndex.Form);
        var pageXaml = XamlBulder.GetXaml(pageIndex);
        await WriteFileAsync(fullPath, pageXaml);

        // TODO: Refactor
        var template = await builder.CreateTemplateTSAsync();
        fileName = $"{platformUrl.Action}.template.ts";
        fullPath = _appCodeProvider.GetMainModuleFullPath(platformUrl.LocalPath.RemoveHeadSlash(), fileName);
        await WriteFileAsync(fullPath, template);

        var map = await builder.CreateMapTSAsync(); 

        return new CreatedFile(filePath, true);
    }

    private Task WriteFileAsync(String fullPath, String content)
    {
        var dir =  Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return File.WriteAllBytesAsync(fullPath, Encoding.UTF8.GetBytes(content));
    }
}
