// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

using Newtonsoft.Json;

namespace A2v10.Services;
public record InvokeResult : IInvokeResult
{
    public InvokeResult(Byte[] body, String contentType, String? fileName = null)
    {
        Body = body;
        ContentType = contentType;
        FileName = fileName;
    }

    public Byte[] Body { get; }
    public String ContentType { get; }
    public String? FileName { get; }

    public static InvokeResult JsonFromObject(Object obj)
    {
        var json = JsonConvert.SerializeObject(obj, JsonHelpers.CompactSerializerSettings);
        return new InvokeResult(
            body: Encoding.UTF8.GetBytes(json),
            contentType: MimeTypes.Application.Json
        );
    }
}

