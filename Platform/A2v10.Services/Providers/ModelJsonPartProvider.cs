// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace A2v10.Services;

public class ModelJsonPartProvider : IModelJsonPartProvider
{
	private readonly IAppCodeProvider _appCodeProvider;
	//private readonly RedirectModule? _redirect;

	public ModelJsonPartProvider(IAppCodeProvider appCodeProvider, IOptions<AppOptions> appOptions)
	{
		_appCodeProvider = appCodeProvider;
		/*
		var redPath = _appCodeProvider.MakeFullPath(String.Empty, "redirect.json", false);
		if (appCodeProvider.FileExists(redPath))
			_redirect = new RedirectModule(redPath, appOptions.Value.Environment.Watch);
		*/
	}

	public async Task<ModelJson?> GetModelJsonAsync(IPlatformUrl url)
	{
		//var localPath = _redirect?.Redirect(url.LocalPath);
		//url.Redirect(localPath);
		var modelPath = _appCodeProvider.MakePath(url.LocalPath, "model.json");
		using var stream = _appCodeProvider.FileStreamRO(modelPath);
		if (stream == null)
			return null;
		using var sr = new StreamReader(stream);
		String json = await sr.ReadToEndAsync();
		var rm = JsonConvert.DeserializeObject<ModelJson>(json, JsonHelpers.CamelCaseSerializerSettings);
		rm?.OnEndInit(url);
		return rm;
	}
}

