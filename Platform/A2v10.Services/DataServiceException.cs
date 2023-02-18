﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace A2v10.Services;

public sealed class DataServiceException : Exception
{
    public DataServiceException(String msg)
        : base(msg)
    {
    }
}

