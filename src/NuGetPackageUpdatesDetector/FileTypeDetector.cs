using System;
using System.IO;
using System.Xml.Linq;

namespace NuGetPackageUpdatesDetector
{
    internal static class FileTypeDetector
    {
        public static FileType Detect(string filePath)
        {
            if (Path.GetFileName(filePath).ToLower() == "packages.config")
            {
                return FileType.PackagesConfig;
            }
            if (Path.GetExtension(filePath).ToLower() == ".sln")
            {
                return FileType.VisualStudioSolution;
            }
            if (Path.GetExtension(filePath).ToLower() == ".xml")
            {
                try
                {
                    if (XDocument.Load(filePath).Element("Rules") != null)
                    {
                        return FileType.SolutionCopConfig;
                    }
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
            return FileType.Unknown;
        }
    }

    internal enum FileType
    {
        Unknown,
        PackagesConfig,
        VisualStudioSolution,
        SolutionCopConfig
    }
}
