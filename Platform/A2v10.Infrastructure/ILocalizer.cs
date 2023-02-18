﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Infrastructure;
public interface ILocalizer
{
    String? Localize(String? locale, String? content, Boolean replaceNewLine = true);
    String? Localize(String? content);
}

