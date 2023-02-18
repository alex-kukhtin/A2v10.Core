﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Platform.Web;

public sealed class AccessDeniedException : Exception
{
    public AccessDeniedException(String message)
        : base(message)
    {
    }
}
