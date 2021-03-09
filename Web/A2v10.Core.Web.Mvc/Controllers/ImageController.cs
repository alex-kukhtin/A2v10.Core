// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Core.Web.Mvc.Controllers
{

	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
	public class ImageController : BaseController
	{
		private readonly IDataService _dataService;
		private readonly ITokenProvider _tokenProvider;

		public ImageController(IApplicationHost host,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IDataService dataService, ITokenProvider tokenProvider)
			: base(host, localizer, userStateManager, profiler)
		{
			_dataService = dataService;
			_tokenProvider = tokenProvider;
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
					return;

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
			//var bi = await _dataService.StaticImage(pathInfo, Response.BodyWriter.AsStream());
			//Response.ContentType = bi.Mime;
			//await Response.Body.WriteAsync(bi.Stream);
			//Stream s;
			//await s.CopyToAsync(Response.BodyWriter.AsStream());
			//Response.BodyWriter
			// {pagePath}/action/id
			try
			{
				throw new NotImplementedException("ImageController.StaticImage");
			} catch (Exception ex)
			{
				await WriteImageException(ex);
			}
		}

		async Task WriteImageException(Exception ex)
		{
			if (ex.InnerException != null)
				ex = ex.InnerException;

			var svg = 
$@"<svg width='180px' height='40px' xmlns='http://www.w3.org/2000/svg'>
	<rect width='180' height='40' fill='#f9f0f0' stroke='#a94442' />
	<text x='90' y='25' fill='#a94442' font-size='11px' text-anchor='middle'>{ex.Message}</text>
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
