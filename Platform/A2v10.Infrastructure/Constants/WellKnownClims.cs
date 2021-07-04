// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{
	public static class WellKnownClims
	{
		public const String NameIdentifier =  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
		public const String PersonName = nameof(PersonName);
		public const String Admin = nameof(Admin);
		public const String TenantAdmin = nameof(TenantAdmin);
		public const String ClientId = nameof(ClientId);
		public const String TenantId = nameof(TenantId);
		public const String Segment = nameof(Segment);
		public const String Locale = nameof(Locale);
	}
}
