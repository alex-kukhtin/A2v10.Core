// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.AspNetCore.DataProtection;

namespace A2v10.Platform.Web
{
	public record UserIdentity : IUserIdentity
	{
		public Int64? Id { get; init; }
		public String Name { get; init; }
		public String PersonName { get; init; }

		public Int32? Tenant { get; init; }
		public String Segment { get; init; }

		public Boolean IsAdmin { get; init; }
		public Boolean IsTenantAdmin { get; init; }
	}

	public record UserState : IUserState
	{
		public Int64? Company { get; set; }
		public Boolean IsReadOnly { get; set; }

		// TODO: isValud???
		public Boolean Invalid { get; set; }
	}

	public record UserLocale : IUserLocale
	{
		public String Locale { get; init; }

		public String Language
		{
			get
			{
				var loc = Locale;
				if (loc != null)
					return loc.Substring(0, 2);
				else
					return Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
			}
		}
	}

	public class CurrentUser : ICurrentUser
	{
		public IUserIdentity Identity { get; private set; }
		public UserState State {get; private set;}
		public IUserLocale Locale { get; private set; }

		IUserState ICurrentUser.State => State;

		public Boolean IsAdminApplication { get; private set; }

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IDataProtector _protector;

		public CurrentUser(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider)
		{
			_httpContextAccessor = httpContextAccessor;
			_protector = dataProtectionProvider.CreateProtector("State");
		}

		public void Setup(HttpContext context)
		{
			SetupUserIdentity(context);
			SetupUserState(context);
			SetupUserLocale(context);
			IsAdminApplication = context.Request.Path.StartsWithSegments("/admin");
		}

		void SetupUserIdentity(HttpContext context) 
		{ 
			var ident = context.User.Identity;
			if (ident.IsAuthenticated) {
				Identity = new UserIdentity()
				{
					Id = ident.GetUserId<Int64>(),
					Tenant = ident.GetUserTenantId(),
					Name = ident.Name,
					PersonName = ident.GetUserPersonName(),
					Segment = ident.GetUserSegment(),
					IsAdmin = ident.IsUserAdmin(),
					IsTenantAdmin = ident.IsTenantAdmin()
				};
			} 
			else
			{
				Identity = new UserIdentity();
			}
		}

		void SetupUserState(HttpContext context)
		{
			var state = context.Request.Cookies[CookieNames.Identity.State];
			if (String.IsNullOrEmpty(state))
			{
				State = new UserState()
				{
					Invalid = true
				};
			}
			else
			{
				try
				{
					State = JsonConvert.DeserializeObject<UserState>(_protector.Unprotect(state));
				} 
				catch (Exception ex)
				{
					State = new UserState()
					{
						Invalid = true
					};
				}
			}
		}

		void SetupUserLocale(HttpContext context)
		{
			var ident = context.User.Identity;
			var userLoc = ident.GetUserLocale();
			if (context.Request.Query.ContainsKey("lang"))
			{
				var lang = context.Request.Query["lang"];
				// TODO: check available locales
			}
			Locale = new UserLocale()
			{
				Locale = userLoc
			};
		}

		public void SetCompanyId(Int64 id)
		{
			if (State == null)
				throw new InvalidProgramException("There is no current user state");
			State.Company = id;
			StoreState();
		}

		public void SetReadOnly(Boolean readOnly)
		{
			if (State == null)
				throw new InvalidProgramException("There is no current user state");
			State.IsReadOnly = readOnly;
			StoreState();
		}

		void StoreState()
		{
			var stateJson = JsonConvert.SerializeObject(State);
			_httpContextAccessor.HttpContext.Response.Cookies.Append(CookieNames.Identity.State, _protector.Protect(stateJson));
		}
	}
}

