using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Web.Identity
{
	public class LoginViewModel
	{
		public String Login { get; set; }
		public String Password { get; set; }
		public String RememberMe { get; set; }

		public Boolean IsPersistent => RememberMe != null && RememberMe.Equals("on", StringComparison.OrdinalIgnoreCase);
	}
}
