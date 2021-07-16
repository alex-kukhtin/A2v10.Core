
using System;
using Microsoft.Extensions.Configuration;
using Stimulsoft.Base;

namespace A2v10.ReportEngine.Stimulsoft
{
	public class StimulsoftLicenseManager
	{
		public static void SetLicense(IConfiguration config) { 
			String lic = config.GetValue<String>("stimulsoft:license");
			if (!String.IsNullOrEmpty(lic))
				StiLicense.LoadFromString(lic);
		}
	}
}
