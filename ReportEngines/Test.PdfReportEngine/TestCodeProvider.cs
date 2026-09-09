// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.IO;

using A2v10.Infrastructure;

namespace Test.PdfReportEngine;

/*
Заглушка провайдера: отчётный слой путями не занимается, поэтому склейка тут та же,
что в боевом MakePath, а всё остальное — словарь. Файлы отдаются только ресурсным
каналом, потому что байты умеет только он.
*/
internal class TestCodeProvider(Dictionary<String, Byte[]>? files = null) : IAppCodeProvider
{
	private readonly Dictionary<String, Byte[]> _files = files ?? [];

	public String AppId => "test";

	public String MakePath(String path, String fileName) =>
		Path.GetRelativePath(".", Path.Combine(path, fileName)).Replace('\\', '/');

	public Boolean IsFileExists(String path) => _files.ContainsKey(path);

	// Промах ресурса — ошибка, промах RO — ответ: заглушка держит оба контракта, иначе
	// тесты пинали бы не тот, что в бою
	public Stream FileStreamResource(String path, Boolean primaryOnly = false) =>
		FileStreamRO(path) ?? throw new FileNotFoundException($"File not found '{path}'");

	public Stream? FileStreamRO(String path, Boolean primaryOnly = false) =>
		_files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null;

	public IEnumerable<Stream> EnumerateFileStreamsRO(String path) => [];
	public IEnumerable<String> EnumerateAllFiles(String path, String searchPattern) => [];
	public IEnumerable<String> EnumerateAllFilesRecursive(String path, String searchPattern) => [];
	public IEnumerable<String> EnumerateWatchedDirs(String path, String searchPattern) => [];

	public Boolean HasLicensedModules => false;
	public IEnumerable<Guid> LicensedModules => [];
	public String? ModuleVersion => null;
	public String GetMainModuleFullPath(String path, String fileName) => String.Empty;
}
