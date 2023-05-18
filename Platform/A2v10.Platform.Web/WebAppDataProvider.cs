// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using System.IO;
using Microsoft.Extensions.Options;

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
		using var stream = _codeProvider.FileStreamRO("app.json", primaryOnly: true);
		if (stream != null)
		{
			using var sr = new StreamReader(stream);
			var appJson = await sr.ReadToEndAsync();
			var result = JsonConvert.DeserializeObject<ExpandoObject>(appJson) ?? new ExpandoObject();
			result.Add("appId", _codeProvider.AppId);
			return result;
		}

		return new ExpandoObject()
		{
			{ "version", _appVersion.AppVersion },
			{ "title", "A2v10.Core Web Application" },
			{ "copyright", _appVersion.Copyright },
			{ "appId", _codeProvider.AppId }
		};
	}

	public async Task<String> GetAppDataAsStringAsync()
	{
		return _localizer.Localize(null, JsonConvert.SerializeObject(await GetAppDataAsync())) ?? String.Empty;
	}
}
