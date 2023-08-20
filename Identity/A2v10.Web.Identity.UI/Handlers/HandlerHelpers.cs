// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Security.Cryptography;

namespace A2v10.Identity.UI;

internal static class HandlerHelpers
{
    public static String GenerateApiKey()
    {
        Int32 size = 48;
        Byte[] data = RandomNumberGenerator.GetBytes(size);
        String res = Convert.ToBase64String(data);
        res = res.Remove(res.Length - 2);
        return res;
    }
}
