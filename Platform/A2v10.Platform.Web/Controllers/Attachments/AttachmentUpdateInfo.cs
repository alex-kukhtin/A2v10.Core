// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Platform.Web;

public record AttachmentUpdateInfo
{
    public Int32? TenantId { get; set; }
    public Int64? CompanyId { get; set; }
    public Int64 UserId { get; set; }
    public String Key { get; set; }
    public Object? Id { get; set; }
    public String Mime { get; set; }
    public String Name { get; set; }
    public Stream Stream { get; set; }
    public String BlobName { get; set; }
}
