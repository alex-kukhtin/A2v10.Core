// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class AppConfiruation : IAppConfiguration
	{
		public AppConfiruation(IConfiguration config)
		{
			var appSettings = config.GetSection("appSettings");
			Watch = appSettings.GetValue<Boolean>("watch");
		}

		#region IAppConfiguration
		public Boolean Watch { get; }
		#endregion
	}
}
