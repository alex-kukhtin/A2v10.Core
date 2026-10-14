// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

/* The base is an owner's endpoint address, and its root is where its roles are - read through the
 * reader, so a metadata folder's synthesized root is a base too; no root at all - Load is loud, or the
 * call would pass with nothing checked. The roles stop only someone with no access to any owner; which
 * owner a user may see is the application's procedures', as everywhere. One check for every caller:
 * the calls of a requireBase endpoint (DataService) and the upload of an attachment (BlobService)
 */
internal static class BaseAccess
{
	public static async Task CheckAsync(IModelJsonReader reader, ICurrentUser user, String? @base)
	{
		if (String.IsNullOrEmpty(@base))
			throw new DataServiceException("The endpoint requires base");
		var root = await reader.GetBaseAsync(new PlatformUrl(UrlKind.Page, $"{@base.Trim('/')}/base/"));
		if (!root.CheckRoles(user.Identity.Roles))
			throw new DataServiceException("Access denied");
	}
}
