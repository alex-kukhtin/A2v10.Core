// Copyright © 2022-2023 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

public class Components : MarkupExtension
{
	public String? Pathes { get; set; }

	public Components()
	{

	}
	public Components(String pathes)
	{
		Pathes = pathes;
	}

	public override Object? ProvideValue(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget iTarget)
			return null;
		var targetProp = iTarget.TargetProperty as PropertyInfo;
		if (targetProp == null)
			return null;
		if (targetProp.PropertyType != typeof(ComponentDictionary))
			throw new XamlException("The 'Components' markup extension can only be used for properties that are of type 'ComponentDictionary'");
		if (serviceProvider.GetService(typeof(IUriContext)) is IUriContext root && root.BaseUri != null)
		{
			String baseFileName = root.BaseUri.ToString();
			return Load(baseFileName, serviceProvider);
		}
		return null;
	}

	ComponentDictionary? Load(String baseFileName, IServiceProvider serviceProvider)
	{
        if (String.IsNullOrEmpty(Pathes))
            return null;
        
        String basePath = Path.GetDirectoryName(baseFileName) ??
			throw new XamlException("Invalid Base path");

		var dict = new ComponentDictionary();
		foreach (var path in Pathes.Split(','))
			dict.Append(LoadOneFile(serviceProvider, basePath, path.Trim()));
		return dict;
	}

	static ComponentDictionary LoadOneFile(IServiceProvider serviceProvider, String basePath, String path)
	{
		String targetPath = Path.Combine(basePath, path) + ".xaml";
		targetPath= Path.GetRelativePath(".", targetPath);
		var xamPartProvider = serviceProvider.GetRequiredService<IXamlPartProvider>();
		var xmlPart = xamPartProvider.GetXamlPart(targetPath);
		if (xmlPart is ComponentDictionary dict)
			return dict;
		throw new XamlException("Root element is not a ComponentDictionary");
	}
}
