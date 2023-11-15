
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;

using Newtonsoft.Json;

namespace A2v10.Core.Web.Site.TestServices;

public class TestBusinessAppProvider
{
	private readonly List<BusinessApplication> _businessApplications = [];
	private readonly String _appDirectory;
	public TestBusinessAppProvider()
	{
		var assPath = Assembly.GetExecutingAssembly().Location;
		_appDirectory = Path.GetDirectoryName(assPath) ??
			throw new InvalidOperationException("Invalid path");

		var fileName = Path.GetFullPath(Path.Combine(_appDirectory, "applications/applications.json"));

		if (!File.Exists(fileName))
			return;
		var json = File.ReadAllText(fileName);
		var appList = JsonConvert.DeserializeObject<List<BusinessApplication>>(json);
		if (appList != null)
			_businessApplications = appList;
	}

	public IList<BusinessApplication> AllApplications => _businessApplications;

	public String GetAppFilePath(String path)
	{
		var fileName = Path.GetFullPath(Path.Combine(_appDirectory, "applications", path));
		if (!File.Exists(fileName))
			throw new InvalidOperationException($"File {path} not found");
		return fileName;
	}
}
