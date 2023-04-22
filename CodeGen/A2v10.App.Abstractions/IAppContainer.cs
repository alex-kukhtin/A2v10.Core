// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.App.Abstractions;

public interface IAppContainer
{
    String? GetText(String path);
    IEnumerable<String> EnumerateFiles(String prefix, String pattern);
    Guid ModuleId { get; }
}
