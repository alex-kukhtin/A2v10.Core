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
using System.Net.Http.Headers;

namespace A2v10.Platform.Web.Controllers
{

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
				if (blob == null)
					throw new InvalidReqestExecption($"Image not found. ({pathInfo})");

				ValidateToken(blob.Token, token);

				var ar = new WebBinaryActionResult(blob.Stream, blob.Mime);
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
	}
}
