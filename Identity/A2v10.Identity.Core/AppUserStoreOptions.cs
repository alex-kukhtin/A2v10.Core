// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity
{
	public class AppUserStoreOptions
	{
		public AppUserStoreOptions()
		{
			Schema = "a2security";
		}

		public String DataSource { get; set; }
		public String Schema { get; set; }
	}
}
