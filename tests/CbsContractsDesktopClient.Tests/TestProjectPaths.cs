using System;
using System.IO;

namespace CbsContractsDesktopClient.Tests;

internal static class TestProjectPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string FromRepositoryRoot(params string[] parts)
    {
        var pathParts = new string[parts.Length + 1];
        pathParts[0] = RepositoryRoot;
        Array.Copy(parts, 0, pathParts, 1, parts.Length);

        return Path.Combine(pathParts);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CbsContractsDesktopClient.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find repository root from '{AppContext.BaseDirectory}'.");
    }
}
