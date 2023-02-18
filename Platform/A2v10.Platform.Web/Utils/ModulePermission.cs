// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class ModulePermission : Dictionary<String, PermissionBits>
{
    public String Module { get; set; }
    public Int32 Permissions { get; set; }

    public static String Serialize(IList<ModulePermission> list)
    {
        return String.Join(separator: "; ", list.Select(x => $"{x.Module}:{x.Permissions}"));
    }

    public static String FromExpandoList(IList<ExpandoObject> list)
    {
        return String.Join("; ", list.Select(x => $"{x.Eval<String>("Module")}:{x.Eval<Int32>("Permissions")}"));
    }
}
