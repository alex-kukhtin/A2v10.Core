// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebAppDataProvider : IAppDataProvider
{
	private readonly IAppCodeProvider _codeProvider;
	private readonly ILocalizer _localizer;
	private readonly IAppVersion _appVersion;
	public WebAppDataProvider(IAppCodeProvider codeProvider, ILocalizer localizer, IAppVersion appVersion)
	{
		_codeProvider = codeProvider;
		_localizer = localizer;
		_appVersion = appVersion;
	}

	public String AppVersion => _appVersion.AppVersion;

	public async Task<ExpandoObject> GetAppDataAsync()
	{
		var appJson = await _codeProvider.ReadTextFileAsync(String.Empty, "app.json", false);
		if (appJson != null)
		{
			return JsonConvert.DeserializeObject<ExpandoObject>(appJson) ?? new ExpandoObject();
		}

		return new ExpandoObject()
		{
			{ "version", _appVersion.AppVersion },
			{ "title", "A2v10.Core Web Application" },
			{ "copyright", _appVersion.Copyright }
		};
	}

	public async Task<String> GetAppDataAsStringAsync()
	{
		return _localizer.Localize(null, JsonConvert.SerializeObject(await GetAppDataAsync())) ?? String.Empty;
	}
}
