using System.IO;

namespace A2v10.Services;

internal interface IAppCodeProviderImpl
{
    Boolean IsFileSystem { get; }
    String MakeFullPath(String path, String fileName, Boolean admin);
    Boolean IsFileExists(String path, String fileName);
    Stream? FileStreamRO(String path);
    IEnumerable<String> EnumerateFiles(String path, String searchPattern);
}
