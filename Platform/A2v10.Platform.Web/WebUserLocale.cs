// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web
{
	public class WebUserLocale : IUserLocale
	{
		public String Locale { get; set; }

		public String Language
		{
			get
			{
				var loc = Locale;
				if (loc != null)
					return loc.Substring(0, 2);
				else
					return Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
			}
		}
	}
}
