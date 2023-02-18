﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace A2v10.Services;

public record AssemblyDescription
{
    public String Name { get; private set; }
    public String ProductName { get; private set; }
    public String Version { get; private set; }
    public String Copyright { get; private set; }
    public String Build { get; private set; }

    public AssemblyDescription(AssemblyName an, String productName, String copyright)
    {
        Name = an.Name ?? String.Empty;
        ProductName = productName;
        Version = String.Format("{0}.{1}.{2}", an.Version?.Major, an.Version?.Minor, an.Version?.Build);
        Build = an.Version?.Build.ToString() ?? String.Empty;
        Copyright = copyright.Replace("(C)", "©").Replace("(c)", "©");
    }
}

public class AppInfo
{
    public static AssemblyDescription MainAssembly => GetDescription(Assembly.Load("A2v10.Platform"));

    static AssemblyDescription GetDescription(Assembly a)
    {
        var c = a.GetCustomAttributes<AssemblyCopyrightAttribute>().FirstOrDefault();
        var n = a.GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault();
        return new AssemblyDescription(a.GetName(),
            n?.Product ?? String.Empty,
            c?.Copyright ?? String.Empty);
    }
}

public class PlatformAppVersion : IAppVersion
{
    public String AppVersion => AppInfo.MainAssembly.Version;
    public String AppBuild => AppInfo.MainAssembly.Build;
    public String Copyright => AppInfo.MainAssembly.Copyright;
}

