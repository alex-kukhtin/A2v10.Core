// Copyright © 2021-2022 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Web.Identity;
public record AppUserStoreOptions
{
	public String? DataSource { get; set; }
	public String? Schema { get; set; }
	public Func<AppUser, IEnumerable<KeyValuePair<String, String?>>>? Claims { get; set; } 
}

