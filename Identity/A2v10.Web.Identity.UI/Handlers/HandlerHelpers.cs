using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
