using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Web.Identity.ApiKey
{
	public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
	{
		public const String DefaultScheme = "API Key";
		public String Scheme => DefaultScheme;
		public String AuthenticationType = DefaultScheme;
	}
}
