// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;


namespace A2v10.Infrastructure;

public interface ILocalizerDictiorany
{
	IDictionary<String, String> GetLocalizerDictionary(String locale);
}
