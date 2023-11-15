// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class WebLocalizer(ILocalizerDictiorany dictiorany, ICurrentUser user) : BaseLocalizer(user)
{
	private readonly ILocalizerDictiorany _dictionary = dictiorany;

    protected override IDictionary<String, String> GetLocalizerDictionary(String locale)
	{
		return _dictionary.GetLocalizerDictionary(locale);
	}
}
