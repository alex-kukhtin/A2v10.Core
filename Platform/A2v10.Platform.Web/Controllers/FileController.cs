// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Platform.Web.Controllers;

[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
public class FileController : BaseController
{
	private readonly IDataService _dataService;
	private readonly ITokenProvider _tokenProvider;
	private readonly IAppCodeProvider _appCodeProvider;

	public FileController(IApplicationHost host,
		ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, 
		IDataService dataService, ITokenProvider tokenProvider, IAppCodeProvider appCodeProvider)
		: base(host, localizer, currentUser, profiler)
	{
		_dataService = dataService;
		_tokenProvider = tokenProvider;
		_appCodeProvider = appCodeProvider;
	}

	[Route("_file/{*pathInfo}")]
	[HttpGet]
	public async Task<IActionResult> DefaultGet(String pathInfo)
	{
		try
		{
			var token = Request.Query["token"];
			if (token.Count == 0)
				throw new InvalidReqestExecption("Invalid image token");
			var blob = await _dataService.LoadBlobAsync(UrlKind.File, pathInfo, SetSqlQueryParams);
			if (blob == null || blob.Stream == null)
				throw new InvalidReqestExecption($"Image not found. ({pathInfo})");

			ValidateToken(blob.Token, token);

			var ar = new WebBinaryActionResult(blob.Stream, blob.Mime ?? MimeTypes.Text.Plain);
			if (Request.Query["export"].Count > 0)
			{
				var cdh = new ContentDispositionHeaderValue("attachment")
				{
					FileNameStar = _localizer.Localize(null, blob.Name)
				};
				ar.AddHeader("Content-Disposition", cdh.ToString());
			}
			else if (MimeTypes.IsImage(blob.Mime))
			{
				ar.EnableCache();
			}
			return ar;
		}
		catch (Exception ex)
		{
			var accept = Request.Headers["Accept"].ToString();
			if (accept != null && accept.Trim().StartsWith("image", StringComparison.OrdinalIgnoreCase))
				return WriteImageException(ex);
			else
				return WriteExceptionStatus(ex);
		}
	}

	[Route("_file/{*pathInfo}")]
	[HttpPost]
	public IActionResult DefaultPost(String pathInfo)
	{
		throw new NotImplementedException("_file/post. Yet not implemented");
	}

	void ValidateToken(Guid dbToken, String token)
	{
		var generated = _tokenProvider.GenerateToken(dbToken);
		if (generated == token)
			return;
		throw new InvalidReqestExecption("Invalid image token");
	}

	[Route("file/{*pathInfo}")]
	[HttpGet]
	public IActionResult LoadFile(String pathInfo)
	{
		try
		{
			Int32 ix = pathInfo.LastIndexOf('-');
			if (ix != -1)
				pathInfo = pathInfo[..ix] + "." + pathInfo[(ix + 1)..];
			String fullPath = _appCodeProvider.MakeFullPath(Path.Combine("_files/", pathInfo), String.Empty, _currentUser.IsAdminApplication);
			if (!_appCodeProvider.FileExists(fullPath))
				throw new FileNotFoundException($"File not found '{pathInfo}'");
			if (!new FileExtensionContentTypeProvider().TryGetContentType(fullPath, out String? contentType))
				contentType = MimeTypes.Application.OctetStream;
			var stream = _appCodeProvider.FileStreamFullPathRO(fullPath);
			return File(stream, contentType, Path.GetFileName(fullPath));
		}
		catch (Exception ex)
		{
			return WriteHtmlException(ex);
		}
	}
}

