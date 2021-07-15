// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Reflection;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc
{

	public class AssemblyDescription
	{
		public String Name { get; private set; }
		public String ProductName { get; private set; }
		public String Version { get; private set; }
		public String Copyright { get; private set; }
		public String Build { get; private set; }

		public AssemblyDescription(AssemblyName an, String productName, String copyright)
		{
			Name = an.Name;
			ProductName = productName;
			Version = String.Format("{0}.{1}.{2}", an.Version.Major, an.Version.Minor, an.Version.Build);
			Build = an.Version.Build.ToString();
			Copyright = copyright.Replace("(C)", "©").Replace("(c)", "©");
		}
	}

	public class AppInfo
	{
		public static AssemblyDescription MainAssembly => GetDescription(Assembly.GetExecutingAssembly());

		static AssemblyDescription GetDescription(Assembly a)
		{
			var c = a.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0];
			var n = a.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0];
			return new AssemblyDescription(a.GetName(),
				(n as AssemblyProductAttribute).Product,
				(c as AssemblyCopyrightAttribute).Copyright);
		}
	}

	public class WebAppVersion : IAppVersion
	{
		public String AppVersion => AppInfo.MainAssembly.Version;
		public String AppBuild => AppInfo.MainAssembly.Build;
		public String Copyright => AppInfo.MainAssembly.Copyright;
	}
}
