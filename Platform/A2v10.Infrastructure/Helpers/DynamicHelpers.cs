﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace A2v10.Infrastructure;

public static class DynamicHelpers
{
    public static T? Get<T>(this ExpandoObject obj, String name)
    {
        if (obj is not IDictionary<String, Object> d)
            return default;
        if (d.TryGetValue(name, out Object? result))
        {
            if (result is T t)
                return t;
        }
        return default;
    }

    public static T GetNotNull<T>(this ExpandoObject obj, String name)
    {
        if (obj is not IDictionary<String, Object?> d)
            throw new KeyNotFoundException(name);
        if (d.TryGetValue(name, out Object? result))
        {
            if (result is T t)
                return t;
        }
        throw new KeyNotFoundException(name);
    }

    public static void Set(this ExpandoObject obj, String name, Object? value)
    {
        if (obj is not IDictionary<String, Object?> d)
            return;
        if (d.ContainsKey(name))
            d[name] = value;
        else
            d.Add(name, value);
    }

    public static void SetNotNull(this ExpandoObject obj, String name, Object? value)
    {
        if (value == null)
            return;
        obj.Set(name, value);
    }

    public static Object? GetObject(this ExpandoObject obj, String name)
    {
        if (obj is not IDictionary<String, Object?> d)
            return null;
        if (d.TryGetValue(name, out Object? result))
        {
            return result;
        }
        return null;
    }

    public static ExpandoObject Add(this ExpandoObject obj, String name, Object value)
    {
        if (obj is not IDictionary<String, Object?> d)
            return obj;
        d.Add(name, value);
        return obj;
    }

    public static void SetIfNotExists(this ExpandoObject obj, String name, Object value)
    {
        if (obj is not IDictionary<String, Object> d)
            return;
        if (!d.ContainsKey(name))
            d.Add(name, value);
    }

    public static void RemoveKeys(this ExpandoObject obj, String keys)
    {
        if (obj is not IDictionary<String, Object?> d)
            return;
        foreach (var key in keys.Split(','))
        {
            if (d.ContainsKey(key))
                d.Remove(key);
        }
    }

    public static Boolean HasProperty(this ExpandoObject obj, String name)
    {
        if (obj is not IDictionary<String, Object?> d)
            return false;
        return d.ContainsKey(name);
    }

    public static void Append(this ExpandoObject obj, NameValueCollection? coll, Boolean toPascalCase = false)
    {
        if (coll == null)
            return;
        var d = obj as IDictionary<String, Object?>;
        foreach (var key in coll.Keys)
        {
            if (key == null)
                continue;
            var skey = key.ToString()!;
            if (toPascalCase)
                skey = skey.ToPascalCase();
            d.Add(skey, coll[key.ToString()]);
        }
    }

    public static void Append(this ExpandoObject? that, ExpandoObject? other, bool replace = true)
    {
        if (that == null)
            return;
        if (other == null)
            return;
        IDictionary<String, Object> thatD = that as IDictionary<String, Object>;
        foreach (var k in other as IDictionary<String, Object>)
        {
            if (!thatD.ContainsKey(k.Key))
                thatD.Add(k.Key, k.Value);
            else if (replace)
                thatD[k.Key] = k.Value;
        }
    }

    public static void Append(this ExpandoObject? that, ExpandoObject? other, String[]? exclude)
    {
        if (that == null)
            return;
        if (other == null)
            return;
        IDictionary<String, Object> thatD = that as IDictionary<String, Object>;
        foreach (var (k, v) in other as IDictionary<String, Object>)
        {
            if (exclude != null && exclude.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!thatD.ContainsKey(k))
                thatD.Add(k, v);
        }
    }

    public static void AppendAndReplace(this ExpandoObject? that, ExpandoObject? other)
    {
        if (that == null)
            return;
        if (other == null)
            return;
        IDictionary<String, Object> thatD = that as IDictionary<String, Object>;
        foreach (var k in other as IDictionary<String, Object>)
        {
            if (thatD.ContainsKey(k.Key))
                thatD[k.Key] = k.Value;
            else
                thatD.Add(k.Key, k.Value);
        }
    }

    public static void AppendIfNotExists(this ExpandoObject? that, ExpandoObject? other)
    {
        if (that == null)
            return;
        if (other == null)
            return;
        IDictionary<String, Object> thatD = that as IDictionary<String, Object>;
        foreach (var k in other as IDictionary<String, Object>)
        {
            if (!thatD.ContainsKey(k.Key))
                thatD.Add(k.Key, k.Value);
        }
    }

    public static ExpandoObject? RemoveEmptyArrays(this ExpandoObject? obj)
    {
        if (obj == null)
            return obj;
        var dict = obj as IDictionary<String, Object?>;
        var arr = dict.Keys.ToList();
        foreach (var key in arr)
        {
            var val = dict[key];
            if (val is IList<ExpandoObject>)
            {
                if (val is IList<ExpandoObject> list)
                {
                    if (list.Count == 0)
                        dict[key] = null;
                    else
                    {
                        foreach (var l in list)
                            l.RemoveEmptyArrays();
                    }
                }
            }
            else if (val is ExpandoObject expVal)
            {
                expVal.RemoveEmptyArrays();
            }
        }
        return obj;
    }

    public static Object? EvalExpression(this ExpandoObject root, String expression, Boolean throwIfError = false)
    {
        Object currentContext = root;
        var arrRegEx = new Regex(@"(\w+)\[(\d+)\]{1}");
        foreach (var exp in expression.Split('.'))
        {
            if (currentContext == null)
                return null;
            String prop = exp.Trim();
            var d = currentContext as IDictionary<String, Object?>;
            if (prop.Contains('['))
            {
                var match = arrRegEx.Match(prop);
                prop = match.Groups[1].Value;
                if ((d != null) && d.ContainsKey(prop))
                {
                    if (d[prop] is IList<ExpandoObject> list)
                        currentContext = list[Int32.Parse(match.Groups[2].Value)];
                    else
                        throw new ArgumentException($"Error in expression '{expression}'. Property '{prop}' is not a list");
                }
                else
                {
                    if (throwIfError)
                        throw new ArgumentException($"Error in expression '{expression}'. Property '{prop}' not found");
                    return null;
                }
            }
            else
            {
                if ((d != null) && d.ContainsKey(prop))
                    currentContext = d[prop]!;
                else
                {
                    if (throwIfError)
                        throw new ArgumentException($"Error in expression '{expression}'. Property '{prop}' not found");
                    return null;
                }
            }
        }
        return currentContext;
    }

    // for workflow
    public static Object? EvalObject(this ExpandoObject root, String expression)
    {
        return root.Eval<Object>(expression, null, true);
    }

    public static ExpandoObject Clone(this ExpandoObject elem, String[]? exclude = null)
    {
        var eo = new ExpandoObject();
        foreach (var v in elem as IDictionary<String, Object>)
        {
            if (exclude != null && exclude.Contains(v.Key))
                continue;
            eo.Set(v.Key, v.Value);
        }
        return eo;
    }

    public static Boolean IsEmpty(this ExpandoObject that)
    {
        return that == null || that is not IDictionary<String, Object> dict || dict.Count == 0;
    }

    public static T? Eval<T>(this ExpandoObject root, String? expression, T? fallback = default, Boolean throwIfError = false)
    {
        if (expression == null)
            return fallback;
        Object? result = root.EvalExpression(expression, throwIfError);
        if (result == null)
            return fallback;
        if (result is T t)
            return t;
        return fallback;
    }

    public static String? Resolve(this ExpandoObject This, String? source)
    {
        if (source == null)
            return null;
        var r = new Regex("\\{\\{(.+?)\\}\\}");
        var ms = r.Matches(source);
        if (ms.Count == 0)
            return source;
        var sb = new StringBuilder(source);
        foreach (Match m in ms.Cast<Match>())
        {
            String key = m.Groups[1].Value;
            String? val = This.EvalExpression(key)?.ToString();
            sb.Replace(m.Value, val);
        }
        return sb.ToString();
    }

    public static Dictionary<String, Object>? Object2Dictionary(Object? obj)
    {
        if (obj == null)
            return null;
        var d = new Dictionary<String, Object>();
        foreach (var pi in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = pi.GetValue(obj);
            if (val != null)
                d.Add(pi.Name, val);
        }
        return d;
    }

    public static ExpandoObject Merge(ExpandoObject that, ExpandoObject other)
    {
        var eo = new ExpandoObject();
        eo.Append(that);
        eo.Append(other);
        return eo;
    }

    public static void AddNameValue(this List<ExpandoObject> coll, String name, Object value)
    {
        if (value == null) return;
        var nvObj = new ExpandoObject();
        nvObj.Set("Name", name);
        nvObj.Set("Value", value);
        coll.Add(nvObj);
    }

    public static Boolean IsNullableType(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static Type GetNonNullableType(this Type type)
    {
        return IsNullableType(type) ? type.GetGenericArguments()[0] : type;
    }

    public static IEnumerable<KeyValuePair<String, Object>> Enumerate(this ExpandoObject obj)
    {
        return obj as IDictionary<String, Object>;
    }
}
