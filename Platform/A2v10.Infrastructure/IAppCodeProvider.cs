// Copyright © 2015-2023 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IAppCodeProvider
{
	Boolean IsFileSystem { get; }

	String MakeFullPath(String path, String fileName, Boolean admin);
    String? MakeFullPathCheck(String path, String fileName);
    Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin);
	String? ReadTextFile(String path, String fileName, Boolean admin);
	Boolean FileExists(String fullPath);
    Boolean IsFileExists(String path, String fileName);
    Boolean DirectoryExists(String fullPath);
	Stream FileStreamFullPathRO(String fullPath);
    Stream FileStreamRO(String path);
	String ReplaceFileName(String baseFullName, String relativeName);

    IEnumerable<String> EnumerateAllFiles(String path, String searchPattern);
}

