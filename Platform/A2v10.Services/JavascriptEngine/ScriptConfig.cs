﻿// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services.Javascript;
public class ScriptConfig(IApplicationHost host)
{
	private readonly IApplicationHost _host = host;

#pragma warning disable IDE1006 // Naming Styles
    public ExpandoObject appSettings(String name)
#pragma warning restore IDE1006 // Naming Styles
	{
		return _host.GetEnvironmentObject(name);
	}
}

