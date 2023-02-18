// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace A2v10.Services;

public class ModelJsonPartProviderFile : IModelJsonPartProvider
{
    private readonly IAppCodeProvider _appCodeProvider;
    private readonly RedirectModule? _redirect;

    public ModelJsonPartProviderFile(IAppCodeProvider appCodeProvider, IOptions<AppOptions> appOptions)
    {
        _appCodeProvider = appCodeProvider;
        String redPath = _appCodeProvider.MakeFullPath(path: String.Empty, fileName: "redirect.json", admin: false);
        if (appCodeProvider.FileExists(redPath))
            _redirect = new RedirectModule(redPath, appOptions.Value.Environment.Watch);
    }

    public async Task<ModelJson?> TryGetModelJsonAsync(IPlatformUrl url)
    {
        String? localPath = _redirect?.Redirect(url.LocalPath);
        url.Redirect(localPath);

        //TODO check access for user (admin)
        Boolean isAdmin = url.LocalPath.StartsWith(value: "identity/", StringComparison.OrdinalIgnoreCase);

        String? json = await _appCodeProvider.ReadTextFileAsync(url.LocalPath, "model.json", isAdmin);
        if (json == null)
        {
            return null;
        }
        else
        {
            var rm = JsonConvert.DeserializeObject<ModelJson>(json, JsonHelpers.CamelCaseSerializerSettings);
            rm?.OnEndInit(url);
            return rm;
        }
    }

    public async Task<ModelJson> GetModelJsonAsync(IPlatformUrl url)
    {
        return await TryGetModelJsonAsync(url) ?? throw new ModelJsonException(msg: $"File not found '{url.LocalPath}/model.json'");
    }
}

