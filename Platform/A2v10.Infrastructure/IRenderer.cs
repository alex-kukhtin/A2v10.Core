// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace A2v10.Infrastructure
{
    public interface IRenderer
    {
        void Render(IRenderInfo info, TextWriter writer);
    }
}
