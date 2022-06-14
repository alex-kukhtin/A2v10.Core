// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public class ModelJsonPartProviderClr : IModelJsonPartProvider
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly RedirectModule? _redirect;
	private readonly IAppContainer _appContainer;

	public ModelJsonPartProviderClr(IAppCodeProvider appCodeProvider,
		IAppProvider appProvider)
	{
		_appCodeProvider = appCodeProvider;
		_appContainer = appProvider.Container;
		var redPath = _appCodeProvider.MakeFullPath(String.Empty, "redirect.json", false);
		if (appCodeProvider.FileExists(redPath))
			_redirect = new RedirectModule(redPath, false);
	}

	public Task<ModelJson?> GetModelJsonAsync(IPlatformUrl url)
	{
		var localPath = _redirect?.Redirect(url.LocalPath);
		url.Redirect(localPath);
		var relativeFileName = url.NormalizedLocal("model.json");
		var ms = _appContainer.GetModelJson<ModelJson>(relativeFileName);
		if (ms == null)
			return Task.FromResult<ModelJson?>(null);
		ms?.OnEndInit(url);
		return Task.FromResult<ModelJson?>(ms);
	}
}

