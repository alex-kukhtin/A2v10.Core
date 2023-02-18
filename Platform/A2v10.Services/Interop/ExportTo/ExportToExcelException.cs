﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Services.Interop.ExportTo
{
    public sealed class ExportToExcelException : Exception
    {
        public ExportToExcelException(String message)
            : base(message)
        {
        }
    }
}
