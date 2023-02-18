// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Infrastructure
{
    public class RenderResult : IRenderResult
    {
        public RenderResult(String body, String contentType)
        {
            Body = body;
            ContentType = contentType;
        }

        public String Body { get; }
        public String ContentType { get; }
    }
}
