// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Module.Infrastructure;

public interface IAppContainer
{
	String? GetText(String path);
	IEnumerable<String> EnumerateFiles(String prefix, String pattern);
	Boolean FileExists(String path);
	Guid Id { get; }
	String? Authors { get; }
	String? Company { get; }
	String? Description { get; }
	String? Copyright { get; }
	Boolean IsLicensed { get; }
}
