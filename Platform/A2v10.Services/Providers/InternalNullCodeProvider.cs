using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services;

internal class InternalNullCodeProvider : IAppCodeProviderImpl
{
    public bool IsFileSystem => false;
    public bool IsLicensed => false;
    public Guid? ModuleId => null;
    public string? ModuleVersion => null;

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => [];
    public IEnumerable<String> EnumerateFilesRecursive(String path, String searchPattern) => [];

    // «null:» — приложение без файлов вообще, поэтому промах тут не «ресурса нет»,
    // а «спросили не у того»; сообщение говорит именно это
    public Stream FileStreamResource(string path) =>
        throw new FileNotFoundException($"There is no application to read '{path}' from");
    public Stream? FileStreamRO(string path) => null;
    public bool IsFileExists(string path) => false;
    public string NormalizePath(string path) => path;
}
