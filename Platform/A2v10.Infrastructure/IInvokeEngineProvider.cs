// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using A2v10.Runtime.Interfaces;

namespace A2v10.Infrastructure
{
    public enum InvokeScope
    {
        Singleton,
        Scoped,
        Transient
    }

    public interface IInvokeEngineProvider
    {
        IRuntimeInvokeTarget FindEngine(String name);
    }
}
