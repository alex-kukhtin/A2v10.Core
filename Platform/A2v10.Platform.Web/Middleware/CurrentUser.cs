// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

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
		public Int64? Company { get; init; }
		public Boolean IsReadOnly { get; init; }
	}

	public class CurrentUser : ICurrentUser
	{
		public IUserIdentity Identity { get; private set; }
		public IUserState State {get; private set;}

		public Boolean IsAdminApplication { get; private set; }

		public void Setup(HttpContext context)
		{
			SetupUserIdentity(context);
			SetupUserState(context);
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
			var ident = context.User.Identity;
			var userLoc = ident.GetUserLocale();
			if (context.Request.Query.ContainsKey("lang"))
			{
				var lang = context.Request.Query["lang"];

			}
			State = new UserState()
			{

			};
		}
	}
}
