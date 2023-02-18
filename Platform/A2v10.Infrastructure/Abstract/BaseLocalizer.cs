﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;
public abstract class BaseLocalizer : ILocalizer, IDataLocalizer
{
    protected abstract IDictionary<String, String> GetLocalizerDictionary(String locale);

    private readonly ICurrentUser _user;

    public BaseLocalizer(ICurrentUser user)
    {
        _user = user;
    }

    String GetLocalizedValue(String? locale, String key)
    {
        if (locale == null)
        {
            locale = _user.Locale.Locale;
            locale ??= Thread.CurrentThread.CurrentUICulture.Name;
        }
        var dict = GetLocalizerDictionary(locale);
        if (dict.TryGetValue(key, out String? value))
            return value;
        return key;
    }

    public String? Localize(String? content)
    {
        return Localize(null, content);
    }

    public String? Localize(String? locale, String? content, Boolean replaceNewLine = true)
    {
        if (content == null)
            return null;
        String s = content;
        if (replaceNewLine)
            s = content.Replace("\\n", "\n");
        var sb = new StringBuilder();
        Int32 xpos = 0;
        String key;
        do
        {
            Int32 start = s.IndexOf("@[", xpos);
            if (start == -1)
                break;
            Int32 end = s.IndexOf("]", start + 2);
            if (end == -1)
                break;
            else
                key = $"@{s.Substring(start + 2, end - start - 2)}";

            var value = GetLocalizedValue(locale, key);
            sb.Append(s[xpos..start]);
            sb.Append(value);
            xpos = end + 1;
        } while (true);
        // tail!
        sb.Append(s[xpos..]);
        return sb.ToString();
    }
}

