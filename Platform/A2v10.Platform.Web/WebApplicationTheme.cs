// Copyright © 2021-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;


namespace A2v10.Platform.Web;

public class WebApplicationTheme(IWebHostEnvironment _webHostEnvironment, IOptions<AppOptions> options, IUserDevice _userDevice, ICurrentUser _currentUser) : IApplicationTheme
{
    private const String DefaultTheme = "classic";

    private readonly AppOptions _appOptions = options.Value;

    #region IApplicationTheme
    public String LogoUrl()
    {
        const String appLogo = "/img/applogo.svg";

        var fi = _webHostEnvironment.WebRootFileProvider.GetFileInfo(appLogo);
        return fi.Exists ? appLogo : String.Empty;
    }

    public String BodyCssClass => _appOptions.BodyCssClass ?? String.Empty;
    public String HtmlCssClass => IsDark ? "dark" : String.Empty;

    public Boolean IsDarkThemeEnabled => !String.IsNullOrEmpty(_appOptions.DarkTheme);
    public Boolean IsDark => _currentUser.Identity.Theme == "D";


    public String MakeTheme()
    {
        if (IsAuto)
        {
            var lightFiles = UserColorScheme(_appOptions.Theme ?? DefaultTheme, "media=\"(prefers-color-scheme:light)\"");
            var darkFiles = UserColorScheme(_appOptions.DarkTheme ?? DefaultTheme, "media=\"(prefers-color-scheme:dark)\"");

            return $"""
            <meta name="color-scheme" content="light dark">
            {lightFiles.theme}
            {lightFiles.colorScheme}
            {darkFiles.colorScheme}
            """;
        }

        var themeName = IsDarkThemeEnabled && IsDark ? _appOptions.DarkTheme : _appOptions.Theme;
        var files = UserColorScheme(themeName ?? DefaultTheme);

        var darkContent = IsDark ? "dark" : "light";

        return $"""
            <meta name="color-scheme" content="{darkContent}">
            {files.theme}
            {files.colorScheme}
            """;
    }

    #endregion

    Boolean IsAuto => IsDarkThemeEnabled && (_currentUser.Identity.Id == null || _currentUser.Identity.Theme == "A");

    (String theme, String colorScheme) UserColorScheme(String theme, String media = "")
    {
        String colorScheme = "default";
        if (theme.Contains('.'))
        {
            var tx = theme.Split('.');
            theme = tx[0].Trim().ToLowerInvariant();
            colorScheme = tx[1].Trim().ToLowerInvariant();
        }
        var mobile = _userDevice.IsMobile ? "_mobile" : "";

        var themeFileName = $"/css/{theme}{mobile}.min.css";
        var schemeFileName = $"/css/{colorScheme}.colorscheme.min.css";

        var tfi = _webHostEnvironment.WebRootFileProvider.GetFileInfo(themeFileName);
        var sfi = _webHostEnvironment.WebRootFileProvider.GetFileInfo(schemeFileName);

        if (!tfi.Exists)
            throw new InvalidOperationException($"Theme file not found: {themeFileName}");
        if (!sfi.Exists)
            throw new InvalidOperationException($"Color scheme file not found: {schemeFileName}");

        var themeFileStamp = tfi.LastModified.ToUnixTimeSeconds().ToString();
        var schemeFileStamp = sfi.LastModified.ToUnixTimeSeconds().ToString();

        var themeFile = $"""<link href="{themeFileName}?ts={themeFileStamp}" rel="stylesheet">""";
        var schemeFile = $"""<link href="{schemeFileName}?ts={schemeFileStamp}" rel="stylesheet" {media}>""";

        return (themeFile, schemeFile);

    }

}
