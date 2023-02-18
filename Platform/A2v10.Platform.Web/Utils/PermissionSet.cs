// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data;

namespace A2v10.Platform.Web;

public class PermissionSet : Dictionary<String, PermissionBits>
{
    public void CheckAllow(String actual, Boolean debug)
    {
        if (Count == 0)
            return;

        if (actual == null)
        {
            throw new AccessDeniedException(message: $"UI:Access denied.\nRequired:\t{this}\nActual:\t&lt;empty&gt;");
        }

        PermissionSet expected = FromString(actual);

        if (Intersect(expected))
            return;

        if (debug)
        {
            throw new AccessDeniedException($"UI:Access denied.\nRequired:\t{this}\nActual:\t{expected}");
        }
        else
        {
            throw new AccessDeniedException("UI:Access denied");
        }
    }

    public override String ToString()
    {
        return String.Join(separator: ", ", values: this.Select(x => $"<b>{x.Key}</b>: [{x.Value.ToString().ToLowerInvariant()}]"));
    }

    private Boolean Intersect(PermissionSet other)
    {
        foreach (KeyValuePair<String, PermissionBits> p in this)
        {
            if (other.TryGetValue(p.Key, out PermissionBits bits))
            {
                // found in expected
                if ((Int32)(bits & p.Value) != 0)
                {
                    return true; // success
                }
            }
        }
        return false;
    }

    static PermissionSet FromString(String text)
    {
        var modules = text.Split(';');
        var result = new PermissionSet();
        foreach (var m in modules)
        {
            var tmp = m.Split(':');
            var module = tmp[0].Trim();
            if (Enum.TryParse<PermissionBits>(tmp[1].Trim(), out PermissionBits perm))
            {
                result.Add(module, perm);
            }
        }
        return result;
    }
}
