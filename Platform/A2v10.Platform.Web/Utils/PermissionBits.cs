// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Platform.Web;

[Flags]
public enum PermissionBits
{
    View = 0x1,
    Edit = 0x2,
    Delete = 0x4,
    Apply = 0x8
}
