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
			ILocalizer localizer, ICurrentUser currentUser, IUserStateManager userStateManager, IProfiler profiler, 
			IDataService dataService, ITokenProvider tokenProvider, IAppCodeProvider appCodeProvider, IUserLocale userLocale)
			: base(host, localizer, currentUser, userStateManager, profiler, userLocale)
		{
			_dataService = dataService;
			_tokenProvider = tokenProvider;
			_appCodeProvider = appCodeProvider;
		}

		[Route("_image/{*pathInfo}")]
		[HttpGet]
		public async Task Image(String pathInfo)
		{
			try
			{
				var token = Request.Query["token"];
				var blob = await _dataService.LoadBlobAsync(UrlKind.Image, pathInfo, SetSqlQueryParams);
				if (blob == null)
					throw new InvalidReqestExecption($"Image not found. ({pathInfo})");

				if (!IsTokenValid(blob.Token, token))
					throw new InvalidReqestExecption("Invalid image token");

				Response.ContentType = blob.Mime;
				if (blob.Stream != null)
					await Response.BodyWriter.WriteAsync(blob.Stream);
			}
			catch (Exception ex)
			{
				await WriteImageException(ex);
			}
		}

		[Route("_static_image/{*pathInfo}")]
		[HttpGet]
		public async Task StaticImage(String pathInfo)
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
				Response.ContentType = MimeTypes.GetMimeMapping(ext);
				await stream.CopyToAsync(Response.BodyWriter.AsStream());
			} 
			catch (Exception ex)
			{
				await WriteImageException(ex);
			}
		}

		async Task WriteImageException(Exception ex)
		{
			if (ex.InnerException != null)
				ex = ex.InnerException;

			var len = ex.Message.Length * 5;
			var svg = 
			$@"<svg width='{len}px' height='40px' xmlns='http://www.w3.org/2000/svg'>
				<rect width='{len}' height='40' fill='#fff0f5' stroke='#880000' stroke-width='1'/>
				<text x='{len/2}' y='25' fill='#880000' font-size='11px' text-anchor='middle'>{ex.Message}</text>
			</svg>";
			Response.Headers.SetCommaSeparatedValues("cache-control", "no-store", "no-cache");
			Response.ContentType = MimeTypes.Image.Svg;
			await HttpResponseWritingExtensions.WriteAsync(Response, svg, Encoding.UTF8);
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
