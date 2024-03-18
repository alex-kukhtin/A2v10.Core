// Copyright © 2020-2024 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;

public class AppUserStoreConfiguration
{
    public const String ConfigurationKey = "Identity:UserStore";
	public String? DataSource { get; set; }
	public String? Schema { get; set; }
	public Boolean? MultiTenant { get; set; }
	public TimeSpan? ValidationInterval { get; set; }	
	public String? AuthenticatorIssuer { get; set; }	
}

