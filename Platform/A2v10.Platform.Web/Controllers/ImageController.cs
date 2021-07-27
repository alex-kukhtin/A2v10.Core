// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Platform.Web.Controllers
{

	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
	public class ImageController : BaseController
	{
		private readonly IDataService _dataService;
		private readonly ITokenProvider _tokenProvider;
		private readonly IAppCodeProvider _appCodeProvider;

		public ImageController(IApplicationHost host,
			ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, 
			IDataService dataService, ITokenProvider tokenProvider, IAppCodeProvider appCodeProvider)
			: base(host, localizer, currentUser, profiler)
		{
			_dataService = dataService;
			_tokenProvider = tokenProvider;
			_appCodeProvider = appCodeProvider;
		}

		[Route("_image/{*pathInfo}")]
		[HttpGet]
		public async Task<IActionResult> Image(String pathInfo)
		{
			try
			{
				var token = Request.Query["token"];
				var blob = await _dataService.LoadBlobAsync(UrlKind.Image, pathInfo, SetSqlQueryParams);
				if (blob == null)
					throw new InvalidReqestExecption($"Image not found. ({pathInfo})");

				if (!IsTokenValid(blob.Token, token))
					throw new InvalidReqestExecption("Invalid image token");

				return new WebBinaryActionResult(blob.Stream, blob.Mime);
			}
			catch (Exception ex)
			{
				return WriteImageException(ex);
			}
		}

		[Route("_static_image/{*pathInfo}")]
		[HttpGet]
		public IActionResult StaticImage(String pathInfo)
		{
			try
			{
				if (String.IsNullOrEmpty(pathInfo))
					throw new ArgumentOutOfRangeException(nameof(pathInfo), nameof(StaticImage));
				pathInfo = pathInfo.Replace('-', '.');
				var fullPath = _appCodeProvider.MakeFullPath(pathInfo, String.Empty, _currentUser.IsAdminApplication);
				if (!_appCodeProvider.FileExists(fullPath))
					throw new FileNotFoundException($"File not found '{pathInfo}'");

				using var stream = _appCodeProvider.FileStreamFullPathRO(fullPath);
				var ext = _appCodeProvider.GetExtension(fullPath);
				return new FileStreamResult(stream, MimeTypes.GetMimeMapping(ext));
			} 
			catch (Exception ex)
			{
				return WriteImageException(ex);
			}
		}

		Boolean IsTokenValid(Guid dbToken, String token)
		{
			var generated = _tokenProvider.GenerateToken(dbToken);
			if (generated == token)
				return true;
			Response.ContentType = MimeTypes.Text.Plain;
			Response.StatusCode = 403;
			return false;
		}

	}
}
