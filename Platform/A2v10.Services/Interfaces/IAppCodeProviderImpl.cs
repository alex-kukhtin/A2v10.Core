using System.IO;

namespace A2v10.Services;

internal interface IAppCodeProviderImpl
{
    Boolean IsFileSystem { get; }
    Boolean IsLicensed { get; }
    Guid? ModuleId { get; }
    String NormalizePath(String path);
    Boolean IsFileExists(String path);
    Stream? FileStreamRO(String path);
    Stream? FileStreamResource(String path);
    IEnumerable<String> EnumerateFiles(String path, String searchPattern);
	String? ModuleVersion { get; }
}
