// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Api;

public static class ModelInfoFit
{
    public static ExpandoObject FitModelInfo(this ExpandoObject src)
    {
        var srcDict = (IDictionary<String, Object?>)src;
        if (!srcDict.TryGetValue("$ModelInfo", out var modelInfo) || modelInfo is not ExpandoObject infoSrc)
            return src;   // nothing to translate — the source is already the answer

        var dst = new ExpandoObject();
        var dstDict = (IDictionary<String, Object?>)dst;
        foreach (var (key, value) in srcDict)
            dstDict[key] = key == "$ModelInfo" ? FitEntries(infoSrc) : value;
        return dst;
    }

    // $ModelInfo is keyed by root name ("Agents" → its control echo); translate each entry.
    private static ExpandoObject FitEntries(ExpandoObject src)
    {
        var dst = new ExpandoObject();
        var dstDict = (IDictionary<String, Object?>)dst;
        foreach (var (key, value) in (IDictionary<String, Object?>)src)
            dstDict[key] = value is ExpandoObject entry ? FitEntry(entry) : value;
        return dst;
    }

    private static ExpandoObject FitEntry(ExpandoObject src)
    {
        var dst = new ExpandoObject();
        var dstDict = (IDictionary<String, Object?>)dst;
        foreach (var (key, value) in (IDictionary<String, Object?>)src)
        {
            switch (key)
            {
                case "PageSize":
                    dstDict["Take"] = value;
                    break;
                case "Offset":
                    dstDict["Skip"] = value;
                    break;
                case "SortOrder":
                    dstDict["Sort"] = value;
                    break;
                case "SortDir":
                    // The wire has no asc/desc magic string — desc is a bool (IndexQuery.Desc).
                    dstDict["Desc"] = String.Equals(value as String, "desc", StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    // Not the engine's control vocabulary (Filter, future keys) — carried as is.
                    dstDict[key] = value;
                    break;
            }
        }
        return dst;
    }
}
