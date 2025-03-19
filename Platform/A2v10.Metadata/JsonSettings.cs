// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

namespace A2v10.Metadata;

public static class JsonSettings
{
    public static JsonSerializerSettings IgnoreNull => new JsonSerializerSettings()
    {   
        NullValueHandling = NullValueHandling.Ignore
    };
}
