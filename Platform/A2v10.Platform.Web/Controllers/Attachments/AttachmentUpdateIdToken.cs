// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Platform.Web;

public record AttachmentUpdateIdToken
{
    public Object Id { get; set; }
    public String Mime { get; set; }
    public String Name { get; set; }
    public String Token { get; set; }
}
