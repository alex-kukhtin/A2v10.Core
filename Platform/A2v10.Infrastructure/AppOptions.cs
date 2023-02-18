﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Infrastructure;

public record AppEnvironment
{
    private const String DEBUG = "debug";
    public Boolean Watch { get; set; }
    public String Stage { get; set; } = DEBUG;
    public Boolean IsDebug => Stage == DEBUG;
    public Boolean IsRelease => !IsDebug;
}

public record AppOptions
{
    public String Path { get; set; } = "undefined";
    public String AppName { get; set; } = String.Empty;
    public String? UserMenu { get; set; }
    public String? Theme { get; set; }
    public Boolean MultiTenant { get; set; }
    public Boolean MultiCompany { get; set; }
    public AppEnvironment Environment { get; } = new AppEnvironment();
    public Boolean IsCustomUserMenu => !String.IsNullOrEmpty(UserMenu);
}
