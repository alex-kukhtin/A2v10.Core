using System;

namespace A2v10.Cli;

internal static class PathExtension
{
    internal static String NormalizePath(this String path)
    {
        return path.Replace('\\', '/');
    }
}
