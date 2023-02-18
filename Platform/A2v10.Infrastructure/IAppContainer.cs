// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace A2v10.Infrastructure;

public interface IAppContainer
{
    T? GetModelJson<T>(String path);
    String? GetText(String path);
    Object? GetXamlObject(String path);
    IEnumerable<String> EnumerateFiles(String prefix, String pattern);
    Stream GetStream(String path);
    Boolean FileExists(String path);
}
