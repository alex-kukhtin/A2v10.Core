// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IXamlPartProvider
{
	Task<Object?> GetXamlPartAsync(String path);
	Object? GetXamlPart(String path);
	Object? GetCachedXamlPart(String path);
	Object? GetCachedXamlPartOrNull(String path);
}
