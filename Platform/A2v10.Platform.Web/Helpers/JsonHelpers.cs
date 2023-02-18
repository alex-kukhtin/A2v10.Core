// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace A2v10.Platform.Web
{
    public static class JsonHelpers
    {
        public static readonly JsonSerializerSettings StandardSerializerSettings =
            new()
            {
                Formatting = Formatting.Indented,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

        public static readonly JsonSerializerSettings ReleaseSerializerSettings =
            new()
            {
                Formatting = Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

        public static JsonSerializerSettings ConfigSerializerSettings(bool bDebug)
        {
            return bDebug ? StandardSerializerSettings : ReleaseSerializerSettings;
        }
    }
}
