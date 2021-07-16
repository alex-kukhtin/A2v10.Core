// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Platform.Web
{
	public record PlatformOptions
	{
		public Boolean MultiTenant { get; set; }
		public Boolean MultiCompany { get; set; }
		public Boolean GlobalPeriod { get; set; }
	}
}
