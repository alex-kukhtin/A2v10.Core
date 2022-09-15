// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IXamlPartProvider
{
	Task<Object?> GetXamlPartAsync(String path);
	Object? GetXamlPart(String path);
	Object? GetCachedXamlPart(String path);
}
