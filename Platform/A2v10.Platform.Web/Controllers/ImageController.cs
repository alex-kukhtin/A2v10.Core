// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Dynamic;
using System.Collections.Generic;

using A2v10.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Newtonsoft.Json;

namespace A2v10.Platform.Web.Controllers;

[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
public class ImageController : BaseController
{
    private readonly IDataService _dataService;
    private readonly IAppCodeProvider _appCodeProvider;

    public ImageController(IApplicationHost host,
        ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler,
        IDataService dataService, IAppCodeProvider appCodeProvider)
        : base(host, localizer, currentUser, profiler)
    {
        _dataService = dataService;
        _appCodeProvider = appCodeProvider;
    }

    [Route("_image/{*pathInfo}")]
    [HttpGet]
    public async Task<IActionResult> Image(String pathInfo)
    {
        try
        {
            StringValues token = Request.Query[key: "token"];
            IBlobInfo? blob = await _dataService.LoadBlobAsync(UrlKind.Image, pathInfo, SetSqlQueryParams);

            #region Check blob value
            if (blob == null || blob.Stream == null)
            {
                throw new InvalidRequestException(message: $"Image not found. ({pathInfo})");
            }
            else if (blob.Mime is null)
            {
                throw new InvalidRequestException($"Invalid mime type for image. ({pathInfo})");
            }
            else if (!IsTokenValid(blob.Token, token))
            {
                throw new InvalidRequestException("Invalid image token");
            }
            #endregion

            WebBinaryActionResult result = new(blob.Stream, blob.Mime);
            result.EnableCache();

            return result;
        }
        catch (Exception ex)
        {
            return WriteImageException(ex);
        }
    }

    [Route("_image/{*pathInfo}")]
    [HttpPost]
    public async Task<IActionResult> ImagePost(String pathInfo)
    {
        try
        {
            IFormFileCollection files = Request.Form.Files;

            List<AttachmentUpdateIdToken> savedAttachments = await SaveAttachments(TenantId, pathInfo, UrlKind.Image, files, UserId, CompanyId);
            ExpandoObject resultAsExpandoObj = new();
            resultAsExpandoObj.Set(name: "status", value: "OK");
            resultAsExpandoObj.Set("elems", savedAttachments);

            String resultJson = JsonConvert.SerializeObject(resultAsExpandoObj, JsonHelpers.StandardSerializerSettings);

            WebActionResult postResult = new(resultJson);
            return postResult;
        }
        catch (Exception ex)
        {
            return WriteExceptionStatus(ex);
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

    private Boolean IsTokenValid(Guid dbToken, String token)
    {
        var generated = _tokenProvider.GenerateToken(dbToken);
        if (generated == token)
            return true;
        Response.ContentType = MimeTypes.Text.Plain;
        Response.StatusCode = 403;
        return false;
    }
}
