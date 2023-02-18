// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

namespace A2v10.Platform.Web;
public static class HttpHelpers
{
    public static async Task<String> JsonFromBodyAsync(this HttpRequest request)
    {
        using var tr = new StreamReader(request.Body);
        return await tr.ReadToEndAsync();
    }

    public static async Task<ExpandoObject?> ExpandoFromBodyAsync(this HttpRequest request)
    {
        var json = await request.JsonFromBodyAsync();
        if (json == null)
            return null;
        return JsonConvert.DeserializeObject<ExpandoObject>(json);
    }
}

