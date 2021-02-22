// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IAppCodeProvider
	{
		String MakeFullPath(String path, String fileName);
		String CombineRelativePath(String path1, String path2);

		Boolean FileExists(String fullPath);
		Task<String> ReadTextFileAsync(String path, String fileName);
		String ReadTextFile(String path, String fileName);

		Stream FileStreamFullPathRO(String fullPath);
		String FileReadAllText(String fullPath);

		// replaces
		String ReplaceFileName(String baseFullName, String relativeName);
		String GetExtension(String fullName);
		String ChangeExtension(String fullName, String extension);

}
}
