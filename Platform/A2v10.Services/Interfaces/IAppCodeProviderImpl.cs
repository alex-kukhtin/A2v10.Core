using System.IO;
using System.Threading.Tasks;

namespace A2v10.Services;

internal interface IAppCodeProviderImpl
{
    Boolean IsFileSystem { get; }

    String MakeFullPath(String path, String fileName, Boolean admin);
    String? MakeFullPathCheck(String path, String fileName);
    Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin);
    String? ReadTextFile(String path, String fileName, Boolean admin);
    Boolean FileExists(String fullPath);
    Boolean IsFileExists(String path, String fileName);
    Boolean DirectoryExists(String fullPath);
    Stream FileStreamRO(String path);
    IEnumerable<String> EnumerateFiles(String? path, String searchPattern);
    String ReplaceFileName(String baseFullName, String relativeName);
}
