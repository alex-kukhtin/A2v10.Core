// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Reflection;

using A2v10.Infrastructure;
using A2v10.System.Xaml;

namespace A2v10.Xaml
{
	public class Source : MarkupExtension
	{
		public String Path { get; set; }

		public Source(String path)
		{
			Path = path;
		}

		public override Object ProvideValue(IServiceProvider serviceProvider)
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

		Object Load(String baseFileName, IServiceProvider serviceProvider)
		{
			if (serviceProvider.GetService(typeof(IAppCodeProvider)) is not IAppCodeProvider appReader)
				throw new XamlException("The IAppCodeProvider service not found");

			String targetDir = appReader.ReplaceFileName(baseFileName, Path);

			String ext = appReader.GetExtension(targetDir);

			if (ext == ".js" || ext == ".html")
			{
				if (appReader.FileExists(targetDir))
					return appReader.FileReadAllText(targetDir);
				else
					throw new XamlException($"File not found {Path}");
			}
			else
			{
				String trgPath = appReader.ChangeExtension(targetDir, "xaml");

				if (serviceProvider.GetService(typeof(IXamlReaderService)) is not IXamlReaderService xamlReader)
					throw new XamlException("The IXamlReaderService service not found");
				if (appReader.FileExists(trgPath))
				{
					using var stream = appReader.FileStreamFullPathRO(trgPath);
					return xamlReader.Load(stream, new Uri(trgPath));
				}
				else
					throw new XamlException($"File not found {Path}");
			}
		}
	}
}
