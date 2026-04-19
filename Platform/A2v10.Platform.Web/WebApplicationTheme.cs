// Copyright © 2021-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;


namespace A2v10.Platform.Web;

public class WebApplicationTheme(IWebHostEnvironment _webHostEnviromnent, IOptions<AppOptions> options, IUserDevice _userDevice, ICurrentUser _currentUser) : IApplicationTheme
{
    private readonly AppOptions _appOptions = options.Value;

    public Boolean IsDarkThemeEnabled => !String.IsNullOrEmpty(_appOptions.DarkTheme);
    public Boolean IsDark => _currentUser.Identity.DarkTheme;

    public (String theme, String colorScheme) UserColorScheme(String theme)
    {
        String? colorScheme = null;
        if (theme.Contains('.'))
        {
            var tx = theme.Split('.');
            theme = tx[0].Trim().ToLowerInvariant();
            colorScheme = tx[1].Trim().ToLowerInvariant();
        }
        if (theme == "advance" && colorScheme == null)
            colorScheme = "default";
        var mobile = _userDevice.IsMobile ? "_mobile" : "";
        var themeFileName = $"/css/{theme}{mobile}.min.css";
        var tfi = _webHostEnviromnent.WebRootFileProvider.GetFileInfo(themeFileName);
        var themeFileStamp = tfi.LastModified.ToUnixTimeSeconds().ToString();

        var themeFile = $"""<link href="{themeFileName}?ts={themeFileStamp}" rel="stylesheet">""";
        var schemeFile = $"""<link href="/css/{colorScheme}.colorscheme.min.css?ts={themeFileStamp}\" rel="stylesheet">""";

        return (themeFile, schemeFile);

    }
    public String MakeTheme()
    {
        var themeName = IsDarkThemeEnabled && IsDark ? _appOptions.DarkTheme : _appOptions.Theme;
        var files = UserColorScheme(themeName ?? "classic");

        return $"""
            {files.theme}
            {files.colorScheme}
            """;
    }

    public String LogoUrl()
    {
        var fi = _webHostEnviromnent.WebRootFileProvider.GetFileInfo($"img/applogo.svg");
        if (fi == null || !fi.Exists)
            return String.Empty;
        return "/img/applogo.svg";
    }

    public String BodyCssClass => _appOptions.BodyCssClass ?? String.Empty;
    public String HtmlCssClass => $"{(IsDark ? "dark" : String.Empty)}".Trim();
}
