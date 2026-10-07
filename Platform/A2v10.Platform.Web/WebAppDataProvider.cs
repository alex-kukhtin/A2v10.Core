// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.IO;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebAppDataProvider(IAppCodeProvider codeProvider, ILocalizer localizer, IAppVersion appVersion,
        ICurrentUser currentUser) : IAppDataProvider
{
	private readonly IAppCodeProvider _codeProvider = codeProvider;
	private readonly ILocalizer _localizer = localizer;
	private readonly IAppVersion _appVersion = appVersion;
    private readonly ICurrentUser _currentUser = currentUser;

    public String AppVersion => _appVersion.AppVersion;

	/* The keys of app.json the server reads (A2v10.Metadata, AppJsonMetadata): the client has no use for
	 * them. A list of what to drop and not of what to keep - a legacy app.json carries keys of the client
	 * no schema here describes, and a list of what to keep would cut them silently.
	 */
	private const String SERVER_KEYS = "platformid,aliases,roles,useGrants,boundary";

	public async Task<ExpandoObject> GetAppDataAsync()
	{
		Int64 userId = 0;
        if (_currentUser != null && _currentUser.Identity.Id != null)
			userId = _currentUser.Identity?.Id ?? 0;
        using var stream = _codeProvider.FileStreamRO("app.json", primaryOnly: true);
		if (stream != null)
		{
			using var sr = new StreamReader(stream);
			var appJson = await sr.ReadToEndAsync();
			var result = JsonConvert.DeserializeObject<ExpandoObject>(appJson) ?? [];
			result.RemoveKeys(SERVER_KEYS);
			// the client compares these with the App-Version/Module-Version headers on every request
			result.Add("version", _appVersion.AppVersion);
			result.Add("moduleVersion", _appVersion.ModuleVersion ?? String.Empty);
			result.Add("appId", _codeProvider.AppId);
			if (userId != 0)
				result.Add("userId", userId);
			return result;
		}

		return new ExpandoObject()
		{
			{ "version", _appVersion.AppVersion },
			{ "moduleVersion", _appVersion.ModuleVersion ?? String.Empty },
			{ "title", "A2v10.Core Web Application" },
			{ "copyright", _appVersion.Copyright },
			{ "appId", _codeProvider.AppId },
			{ "userId", userId}
		};
	}

	public async Task<String> GetAppDataAsStringAsync()
	{
		return _localizer.Localize(null, JsonConvert.SerializeObject(await GetAppDataAsync())) ?? String.Empty;
	}
}
