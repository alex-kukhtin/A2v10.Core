// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Web.Identity;
public class AppUserStoreOptions
{
	public AppUserStoreOptions()
	{
		Schema = "a2security";
	}

	public String? DataSource { get; set; }
	public String Schema { get; set; }

	public Func<AppUser, IEnumerable<KeyValuePair<String, String>>>? Claims { get; set; } 
}

