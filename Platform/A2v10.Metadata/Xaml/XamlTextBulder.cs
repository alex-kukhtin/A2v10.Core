// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlTextBulder()
{
    public static String GetXaml(Object elem)
    {
        if (elem is not IRootContainer)
            throw new InvalidOperationException($"XamlBuilder.GetXaml. Invalid element type ({elem.GetType().Name}). Expected IRootContainer");
        if (elem is IInitComplete initComplete)
            initComplete.InitComplete();
        var xamlWriter = new XamlWriter();
        return xamlWriter.GetXaml(elem);
    }
}
