// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;

namespace A2v10.Xaml;
public class Source : MarkupExtension
{
	public String Path { get; set; } = String.Empty;


	public Source()
	{
	}

	public Source(String path)
	{
		Path = path;
	}

	public override Object? ProvideValue(IServiceProvider serviceProvider)
	{
		try
		{
			if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget iTarget)
				return null;

			var targetProp = iTarget.TargetProperty as PropertyInfo;

			if (targetProp == null)
				return null;

			if (targetProp.PropertyType != typeof(Object) && targetProp.PropertyType != typeof(UIElementBase))
				throw new XamlException("The 'Source' markup extension can only be used for properties that are of type 'System.Object' or 'A2v10.Xaml.UIElementBase'");

			if (serviceProvider.GetService(typeof(IUriContext)) is IUriContext root && root.BaseUri != null)
			{
				String baseFileName = root.BaseUri.PathAndQuery;
				return Load(baseFileName, serviceProvider);
			}
			return null;
		}
		catch (Exception ex)
		{
			return new Span() { CssClass = "xaml-exception", Content = ex.Message };
		}
	}

	Object? Load(String baseFileName, IServiceProvider serviceProvider)
	{
		var appReader = serviceProvider.GetRequiredService<IAppCodeProvider>();

		String targetFileName = appReader.ReplaceFileName(baseFileName, Path);

		String ext = appReader.GetExtension(targetFileName);

		if (ext == ".js" || ext == ".html")
		{
			if (appReader.FileExists(targetFileName))
			{
				using var stream = appReader.FileStreamFullPathRO(targetFileName);
				using var rdr = new StreamReader(stream);
				return rdr.ReadToEnd();
			}
			else
				throw new XamlException($"File not found {Path}");
		}
		else
		{
			String trgPath = appReader.ChangeExtension(targetFileName, "xaml");

			if (appReader.FileExists(trgPath))
			{
				var xamPartProvider = serviceProvider.GetRequiredService<IXamlPartProvider>();
				return xamPartProvider.GetXamlPart(trgPath);
			}
			else
				throw new XamlException($"File not found {Path}");
		}
	}
}

