
using System;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using System.Text;

namespace A2v10.Platform.Web
{

	public class WebUserStateManager : IUserStateManager
	{
		const String _readOnly_ = "_readOnly_";
		const String _userCompanyKey = "_userCompany_";

		private readonly IHttpContextAccessor _httpContextAccessor;
		private Boolean _isAdmin = false;

		public WebUserStateManager(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public Boolean IsReadOnly(Int64 userId)
		{
			if (userId == 0)
				return false;
			var ro = _httpContextAccessor.HttpContext.Session.GetInt32(_readOnly_);
			return ro != 0;
		}

		public void SetReadOnly(Boolean readOnly)
		{
			_httpContextAccessor.HttpContext.Session.SetInt32(_readOnly_, readOnly ? 1 : 0);
		}

		public void SetUserCompanyId(Int64 CompanyId)
		{
			_httpContextAccessor.HttpContext.Session.SetString(_userCompanyKey, CompanyId.ToString());
		}

		public Int64 UserCompanyId(Int32 TenantId, Int64 UserId)
		{
			var str =_httpContextAccessor.HttpContext.Session.GetString(_userCompanyKey);
			if (String.IsNullOrEmpty(str))
				return 0;
			return Int64.Parse(str);
		}

		public void SetAdmin()
		{
			_isAdmin = true;
		}

		public Boolean IsAdmin => _isAdmin;
	}
}
