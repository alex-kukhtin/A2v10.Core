// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace A2v10.Services;

public class ModelJsonPartProviderClr : IModelJsonPartProvider
{
    private readonly RedirectModule? _redirect;
    private readonly IAppContainer _appContainer;

    public ModelJsonPartProviderClr(IAppCodeProvider appCodeProvider, IAppProvider appProvider)
    {
        _appContainer = appProvider.Container;

        String redPath = appCodeProvider.MakeFullPath(path: String.Empty, fileName: "redirect.json", admin: false);
        if (appCodeProvider.FileExists(redPath))
        {
            _redirect = new RedirectModule(redPath, watch: false);
        }
    }

    public async Task<ModelJson?> TryGetModelJsonAsync(IPlatformUrl url)
    {
        ModelJson? modelJson = null;
        try
        {
            modelJson = await GetModelJsonAsync(url).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (ModelJsonException)
        {
            //do nothing
        }

        return modelJson;
    }

    public Task<ModelJson> GetModelJsonAsync(IPlatformUrl url)
    {
        String? localPath = _redirect?.Redirect(url.LocalPath);
        url.Redirect(localPath);
        String relativeFileName = url.NormalizedLocal(fileName: "model.json");
        ModelJson? ms = _appContainer.GetModelJson<ModelJson>(relativeFileName);
        
        if (ms == null)
        {
            throw new ModelJsonException(msg: $"File not found '{url.LocalPath}/model.json'");
        }
        else
        {
            ms!.OnEndInit(url);
            return Task.FromResult(ms);
        }
    }
}

