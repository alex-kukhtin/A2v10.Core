// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

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
		var redPath = _appCodeProvider.MakeFullPath(String.Empty, "redirect.json", false);
		if (appCodeProvider.FileExists(redPath))
			_redirect = new RedirectModule(redPath, appOptions.Value.Environment.Watch);
	}

	public async Task<ModelJson?> GetModelJsonAsync(IPlatformUrl url)
	{
		var localPath = _redirect?.Redirect(url.LocalPath);
		url.Redirect(localPath);
		String? json = await _appCodeProvider.ReadTextFileAsync(url.LocalPath, "model.json", false);
		if (json == null)
			return null;
		var rm = JsonConvert.DeserializeObject<ModelJson>(json, JsonHelpers.CamelCaseSerializerSettings);
		rm?.OnEndInit(url);
		return rm;
	}
}

