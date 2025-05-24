// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebApplicationHost(IConfiguration _config, IOptions<AppOptions> appOptions, ICurrentUser _currentUser) : IApplicationHost
{
	private readonly IConfiguration _appSettings = _config.GetSection("appSettings");
	private readonly AppOptions _appOptions = appOptions.Value;

    public Boolean IsMultiTenant => _appOptions.MultiTenant;
	public Boolean IsMultiCompany => _appOptions.MultiCompany;
	public Boolean IsDebugConfiguration => _appOptions.Environment.IsDebug;

    public String? TenantDataSource => String.IsNullOrEmpty(_currentUser.Identity.Segment) ? null : _currentUser.Identity.Segment;

	public String? GetAppSettings(String? source)
	{
		if (source == null)
			return null;
		if (!source.Contains("@{AppSettings.", StringComparison.InvariantCulture))
			return source;
		Int32 xpos = 0;
		var sb = new StringBuilder();
		do
		{
			Int32 start = source.IndexOf("@{AppSettings.", xpos);
			if (start == -1) break;
			Int32 end = source.IndexOf('}', start + 14);
			if (end == -1) break;
			var key = source.Substring(start + 14, end - start - 14);
			var value = _appSettings.GetValue<String>(key) ?? String.Empty;
			sb.Append(source[xpos..start]);
			sb.Append(value);
			xpos = end + 1;
		} while (true);
		sb.Append(source[xpos..]);
		return sb.ToString();
	}

	public ExpandoObject GetEnvironmentObject(String key)
	{
		var val = _appSettings.GetValue<String>(key);
		if (val != null)
			return JsonConvert.DeserializeObject<ExpandoObject>(val, new ExpandoObjectConverter()) 
				?? [];
		var valObj = _appSettings.GetSection(key);
		if (valObj != null)
		{
			var eo = new ExpandoObject();
			foreach (var v in valObj.GetChildren())
				if (v != null && v.Value != null)
					eo.Add(v.Key, v.Value);
			return eo;
		}
		throw new InvalidOperationException($"Configuration parameter 'appSettings/{key}' not defined");
	}
}

