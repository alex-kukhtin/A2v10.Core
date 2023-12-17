// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;

public class NullSqlQueryTextProvider : ISqlQueryTextProvider
{
    public String GetSqlText(String key, ExpandoObject prms)
    {
        throw new NotImplementedException(key);
    }
}
