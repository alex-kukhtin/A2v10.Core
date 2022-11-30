// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

public class TemplateReader
{
	public Page ReadReport(String path)
	{
		throw new NotImplementedException("ReadReport from file");
	}

	public static Page ReadReport(Stream stream)
	{
		var xamlReader = new XamlReaderService();
		var obj = xamlReader.Load(stream);
		if (obj is Page objPage)
		{
			var styleBag = new StyleBag();
			objPage.ApplyStyles("Root", styleBag);
			return objPage;
		}
		throw new InvalidOperationException("Object is not a A2v10.Xaml.Report.Page");
	}
}
