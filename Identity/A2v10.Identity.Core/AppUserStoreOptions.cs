// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Web.Identity;

public enum RolesMode
{
	None,
	Claims,
	Database
}
public record AppUserStoreOptions<T> where T: struct
{
	public String? DataSource { get; set; }
	public String? Schema { get; set; }
	public Boolean? MultiTenant { get; set; }
	public RolesMode UseRoles { get; set; }	
	public Func<AppUser<T>, IEnumerable<KeyValuePair<String, String?>>>? Claims { get; set; } 
}

