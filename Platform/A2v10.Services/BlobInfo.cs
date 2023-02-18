// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace A2v10.Services;
public class BlobInfo : IBlobInfo
{
    public String? Mime { get; set; }
    public String? Name { get; set; }
    public Guid Token { get; set; }
    public Byte[]? Stream { get; set; }
    public String? BlobName { get; set; }
}

