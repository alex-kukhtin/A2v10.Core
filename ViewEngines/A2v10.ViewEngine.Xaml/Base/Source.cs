// Copyright © 2015-2022 Oleksandr Kukhtin. All rights reserved.

using System.Reflection;
using System.IO;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

using PathIO = System.IO.Path;

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
				String baseFileName = root.BaseUri.ToString();
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


		String targetFileName = PathHelpers.ReplaceFileName(baseFileName, Path);

		String ext = PathHelpers.GetExtension(targetFileName);

		if (ext == ".js" || ext == ".html")
		{
            using var stream = appReader.FileStreamRO(targetFileName);
            if (stream != null)
			{
				using var rdr = new StreamReader(stream);
				return rdr.ReadToEnd();
			}
			else
				throw new XamlException($"File not found {Path}");
		}
		else
		{
			String trgPath = PathHelpers.ChangeExtension(targetFileName, "xaml");

			if (appReader.IsFileExists(trgPath))
			{
				var xamPartProvider = serviceProvider.GetRequiredService<IXamlPartProvider>();
				return xamPartProvider.GetXamlPart(trgPath);
			}
			else
				throw new XamlException($"File not found {Path}");
		}
	}
}

