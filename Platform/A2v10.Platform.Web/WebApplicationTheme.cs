// Copyright © 2021-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;


namespace A2v10.Platform.Web;

public class WebApplicationTheme(IWebHostEnvironment _webHostEnviromnent, IOptions<AppOptions> options, IUserDevice _userDevice) : IApplicationTheme
{
	private readonly AppOptions _appOptions = options.Value;

    public String MakeTheme()
    {
		String theme = _appOptions.Theme ?? "classic";
		String? colorScheme = null;
		if (theme.Contains('.'))
		{
			var tx = theme.Split('.');
			theme = tx[0].Trim().ToLowerInvariant();
			colorScheme = tx[1].Trim().ToLowerInvariant();
		}
		if (theme == "advance" && colorScheme == null)
			colorScheme = "default";
		var mobile = _userDevice.IsMobile ? "_mobile": "";
        var themeFileName = $"/css/{theme}{mobile}.min.css";
		var tfi = _webHostEnviromnent.WebRootFileProvider.GetFileInfo(themeFileName);
		var themeFileStamp = tfi.LastModified.ToUnixTimeSeconds().ToString();
		var sb = new StringBuilder();
		sb.AppendLine($"<link href=\"{themeFileName}?ts={themeFileStamp}\" rel=\"stylesheet\">");
		if (colorScheme != null)
		{
			var fi = _webHostEnviromnent.WebRootFileProvider.GetFileInfo($"css/{colorScheme}.colorscheme.css");
			using var rs = fi.CreateReadStream();
			using var tr = new StreamReader(rs);
			sb.AppendLine("<style>");
			sb.Append(tr.ReadToEnd());
			sb.AppendLine("</style>");
		}
		return sb.ToString();
	}

	public String LogoUrl()
	{
        var fi = _webHostEnviromnent.WebRootFileProvider.GetFileInfo($"img/applogo.svg");
		if (fi == null || !fi.Exists)
			return String.Empty;
		return "/img/applogo.svg";
    }

    public String BodyCssClass =>
		_appOptions.BodyCssClass ?? String.Empty;
}
