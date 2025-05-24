// Copyright © 2014-2025 Sarin Na Wangkanai, All Rights Reserved. Apache License, Version 2.0
// https://github.com/wangkanai/wangkanai

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.


using System;

namespace A2v10.Platform.Web.DeviceDetector;

[Flags]
internal enum Device
{
    Unknown = 1 << 0,
    Desktop = 1 << 1, // Windows, Mac, Linux
    Tablet = 1 << 2, // iPad, Android
    Mobile = 1 << 3, // iPhone, Android
    Watch = 1 << 4, // Smart Watch
    Tv = 1 << 5, // Samsung, LG
    Console = 1 << 6, // XBox, Play Station
    Car = 1 << 7, // Ford, Toyota
    IoT = 1 << 8  // Raspberry Pi
}


internal static class UserDeviceDetector
{
    public static Device DeviceFromUserAgent(String? userAgent)
    {
        var agent = (userAgent ?? "").ToLowerInvariant();

        if (IsTablet(agent))
            return Device.Tablet;
        if (IsTV(agent))
            return Device.Tv;
        if (IsMobile(agent))
            return Device.Mobile;
        if (agent.ContainsLower(Device.Watch))
            return Device.Watch;
        if (agent.ContainsLower(Device.Console))
            return Device.Console;
        if (agent.ContainsLower(Device.Car))
            return Device.Car;
        if (agent.ContainsLower(Device.IoT))
            return Device.IoT;

        return Device.Desktop;
    }

    private static Boolean IsTablet(String agent)
    {
        return agent.SearchContains(TabletCollection.KeywordsSearchTrie);
    }

    private static Boolean IsMobile(String agent)
    {
        return agent.Length >= 4 && agent.SearchStartsWith(MobileCollection.PrefixesSearchTrie) ||
               agent.SearchContains(MobileCollection.KeywordsSearchTrie);
    }
    private static Boolean IsTV(String agent)
    {
        return agent.ContainsLower(Device.Tv) || agent.Contains("bravia", StringComparison.Ordinal);
    }
}

internal static class TabletCollection
{
    private static readonly string[] Keywords =
    {
        "tablet",
        "ipad",
        "playbook",
        "hp-tablet",
        "kindle",
        "sm-t",
        "kfauwi"
    };

    public static readonly IPrefixTrie KeywordsSearchTrie = Keywords.BuildSearchTrie();
}

internal static class MobileCollection
{
    // mobile device keywords
    private static readonly string[] Keywords =
    {
        "iphone",
        "mobile",
        "blackberry",
        "phone",
        "smartphone",
        "webos",
        "ipod",
        "lge vx",
        "midp",
        "maemo",
        "mmp",
        "netfront",
        "hiptop",
        "nintendo DS",
        "novarra",
        "openweb",
        "opera mobi",
        "opera mini",
        "palm",
        "psp",
        "smartphone",
        "symbian",
        "up.browser",
        "up.link",
        "wap",
        "windows ce",
        "windows phone"
    };


    public static readonly IPrefixTrie KeywordsSearchTrie = Keywords.BuildSearchTrie();

    // reference 4 character from http://www.webcab.de/wapua.htm
    private static readonly string[] Prefixes =
    {
        "w3c ",
        "w3c-",
        "acs-",
        "alav",
        "alca",
        "amoi",
        "audi",
        "avan",
        "benq",
        "bird",
        "blac",
        "blaz",
        "brew",
        "cell",
        "cldc",
        "cmd-",
        "dang",
        "doco",
        "eric",
        "hipt",
        "htc_",
        "inno",
        "ipaq",
        "ipod",
        "jigs",
        "kddi",
        "keji",
        "leno",
        "lg-c",
        "lg-d",
        "lg-g",
        "lge-",
        "lg/u",
        "maui",
        "maxo",
        "midp",
        "mits",
        "mmef",
        "mobi",
        "mot-",
        "moto",
        "mwbp",
        "nec-",
        "newt",
        "noki",
        "palm",
        "pana",
        "pant",
        "phil",
        "play",
        "port",
        "prox",
        "qwap",
        "sage",
        "sams",
        "sany",
        "sch-",
        "sec-",
        "send",
        "seri",
        "sgh-",
        "shar",
        "sie-",
        "siem",
        "smal",
        "smar",
        "sony",
        "sph-",
        "symb",
        "t-mo",
        "teli",
        "tim-",
        "tosh",
        "tsm-",
        "upg1",
        "upsi",
        "vk-v",
        "voda",
        "wap-",
        "wapa",
        "wapi",
        "wapp",
        "wapr",
        "webc",
        "winw",
        "winw",
        "xda ",
        "xda-"
    };

    public static readonly IPrefixTrie PrefixesSearchTrie = Prefixes.BuildSearchTrie();
}

