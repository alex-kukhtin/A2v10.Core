using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Web.Identity.UI
{
	public class LoginViewModel
	{
		public String Login { get; set; }
		public String Password { get; set; }
		public Boolean RememberMe { get; set; }
		public String RequestToken { get; set; }

		public Boolean IsPersistent => RememberMe;
	}
}
