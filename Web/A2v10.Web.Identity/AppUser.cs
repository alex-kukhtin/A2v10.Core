using System;

namespace A2v10.Web.Identity
{
	public class AppUser
	{
		public Int64 Id { get; set; }
		public String UserName { get; set; }
		public String PersonName { get; set; }
		public String Email { get; set; }
		public String PhoneNumber { get; set; }

		public String PasswordHash { get; set; }
		public String SecurityStamp { get; set; }
		public DateTimeOffset LockoutEndDateUtc { get; set; }
		public Boolean LockoutEnabled { get; set; }
		public Int32 AccessFailedCount { get; set; }
		public Boolean EmailConfirmed { get; set; }
		public Boolean PhoneNumberConfirmed { get; set; }

		public Int32 Tenant { get; set; }
		public String Segment { get; set; }
		public String Locale { get; set; }
	}
}
