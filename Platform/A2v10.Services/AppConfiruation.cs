// Copyright © 2021 Alex Kukhtin. All rights reserved.


using Microsoft.Extensions.Configuration;

namespace A2v10.Services;
public class AppConfiruation : IAppConfiguration
{
	public AppConfiruation(IConfiguration config)
	{
		var appSettings = config.GetSection("appSettings");
		Watch = appSettings.GetValue<Boolean>("watch");
		var strConfig = appSettings.GetValue<String>("configuration");
		// default configuration is "debug"
		Debug = strConfig == null ||  strConfig.Equals("debug", StringComparison.OrdinalIgnoreCase);
		Release = !Debug;
	}

	#region IAppConfiguration
	public Boolean Watch { get; }
	public Boolean Debug { get; }
	public Boolean Release { get; }
	#endregion
}

