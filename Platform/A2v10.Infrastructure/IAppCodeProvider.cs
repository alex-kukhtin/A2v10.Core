// Copyright © 2015-2023 Oleksand Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace A2v10.Infrastructure;

public interface IAppCodeProvider
{
    String AppId { get; }
    String MakePath(String path, String fileName);
    Boolean IsFileExists(String path);
    // Два канала — два контракта. RO зондируют: «а есть ли такой файл» (.vxaml, потом
    // .xaml), поэтому null — законный ответ. Resource не зондирует никто, отсутствие
    // всюду фатально, и бросает сам провайдер — только он знает, ЧТО искал: у CLR это
    // вычисленное имя ресурса, а не путь, и снаружи его не назвать
    Stream? FileStreamRO(String path, Boolean primaryOnly = false);
    Stream FileStreamResource(String path, Boolean primaryOnly = false);
    IEnumerable<Stream> EnumerateFileStreamsRO(String path);
    IEnumerable<String> EnumerateAllFiles(String path, String searchPattern);
    IEnumerable<String> EnumerateAllFilesRecursive(String path, String searchPattern);
    IEnumerable<String> EnumerateWatchedDirs(String path, String searchPattern);

    Boolean HasLicensedModules { get; }
    IEnumerable<Guid> LicensedModules { get; }
	String? ModuleVersion { get; }
    String GetMainModuleFullPath(String path, String fileName);
}

