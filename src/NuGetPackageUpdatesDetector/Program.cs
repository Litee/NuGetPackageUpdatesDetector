using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Common.Logging;
using Common.Logging.Simple;
using NuGet;

namespace NuGetPackageUpdatesDetector
{
    class Program
    {
        private static readonly Dictionary<Tuple<string, IVersionSpec>, IPackage> LookupCache = new Dictionary<Tuple<string, IVersionSpec>, IPackage>();
        private readonly ILog Log = LogManager.GetLogger(typeof (Program));

        static void Main(string[] args)
        {
            var logShouldBeVerbose = args.Any(x => x == "-Verbose");
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(logShouldBeVerbose ? LogLevel.All : LogLevel.Info, false, false, true, null);
            try
            {
                new Program().Run(args);
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("[ERROR] {0}", e.Message);
                Console.Out.WriteLine("[ERROR] {0}", e.StackTrace);
            }
        }

        private void Run(string[] args)
        {
            Log.Info("Starting...");
            if (args.Length == 0)
            {
                Log.Error("No parameters specified!");
                PrintUsage();
                Environment.Exit(-1);
            }
            var allowPrereleaseVersions = args.Any(x => x.StartsWith("-Prerelease"));
            Log.InfoFormat("Prerelease versions: {0}", allowPrereleaseVersions ? "Allowed" :"Not allowed");
            var sourceRepository = args.SkipWhile(x => x != "-Source").FirstOrDefault(x => !x.StartsWith("-")) ?? "https://www.nuget.org/api/v2/";
            Log.InfoFormat("Source repository: {0}", sourceRepository);
            var targetFile = Path.Combine(Environment.CurrentDirectory, args[0]);
            Log.InfoFormat("File to process: {0}", targetFile);
            if (!File.Exists(targetFile))
            {
                Log.ErrorFormat("File not found: {0}", targetFile);
                PrintUsage();
                Environment.Exit(-1);
            }
            else
            {
                IPackageRepository sourcePackageRepository = PackageRepositoryFactory.Default.CreateRepository(sourceRepository);
                var fileType = FileTypeDetector.Detect(targetFile);
                switch (fileType)
                {
                    case FileType.PackagesConfig:
                        ProcessPackageConfigFile(targetFile, sourcePackageRepository, allowPrereleaseVersions);
                        break;
                    case FileType.VisualStudioSolution:
                        string[] packagesConfigFiles = Directory.GetFiles(Path.GetDirectoryName(targetFile), "*.config", SearchOption.AllDirectories)
                            .Where(s => Path.GetFileName(s).StartsWith("packages.", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        foreach (var packagesConfigFile in packagesConfigFiles)
                        {
                            ProcessPackageConfigFile(packagesConfigFile, sourcePackageRepository, allowPrereleaseVersions);
                        }
                        break;
                    case FileType.SolutionCopConfig:
                        var xmlNuGetPackageVersions = XDocument.Load(targetFile).Element("Rules").Element("NuGetPackageVersions");
                        if (xmlNuGetPackageVersions == null)
                        {
                            Log.WarnFormat("NuGetPackageVersions section is missing in {0}", targetFile);
                        }
                        else
                        {
                            var xmlPackages = xmlNuGetPackageVersions.Elements("Package");
                            if (xmlPackages.Any())
                            {
                                foreach (var xmlPackage in xmlPackages)
                                {
                                    var packageId = xmlPackage.Attribute("id").Value;
                                    var packageVersionSpec = xmlPackage.Attribute("version").Value;
                                    var packageVersionConstraint = VersionUtility.ParseVersionSpec(packageVersionSpec);
                                    FindAvailableUpdatesForOnePackage(packageId, packageVersionConstraint.MinVersion, packageVersionConstraint, sourcePackageRepository, allowPrereleaseVersions, targetFile);
                                }

                            }
                            else
                            {
                                Log.WarnFormat("No package version rules defined in {0}", targetFile);
                            }
                        }
                    break;
                    default:
                        Log.ErrorFormat("Unknown target file format: {0}", targetFile);
                        PrintUsage();
                        Environment.Exit(-1);
                        break;
                }
            }
            Log.Info("Finished!");
        }

        private void ProcessPackageConfigFile(string packagesConfigFile, IPackageRepository sourcePackageRepository, bool allowPrereleaseVersions)
        {
            Log.DebugFormat("Processing file {0}", packagesConfigFile);
            var packageReferenceFile = new PackageReferenceFile(packagesConfigFile);
            foreach (var localPackage in packageReferenceFile.GetPackageReferences())
            {
                FindAvailableUpdatesForOnePackage(localPackage.Id, localPackage.Version, localPackage.VersionConstraint, sourcePackageRepository, allowPrereleaseVersions, packagesConfigFile);
            }
        }

        private void PrintUsage()
        {
            var assembly = Assembly.GetEntryAssembly();
            var customAttributes = assembly.GetCustomAttributes(false);
            Console.Out.WriteLine();
            Console.Out.WriteLine(customAttributes.OfType<AssemblyTitleAttribute>().First().Title + " " + FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion);
            Console.Out.WriteLine(customAttributes.OfType<AssemblyCopyrightAttribute>().First().Copyright);
            Console.Out.WriteLine("This is free software distributed under Apache License 2.0");
            Console.Out.WriteLine();
            Console.Out.WriteLine("Usage: NuGetPackageUpdatesDetector.exe (<path-to-packages-config-file> | <path-to-visual-studio-solution-file> | <path-to-solution-cop-config>) [ -Prerelease ] [ -Verbose ] [ -Source nuget-feed-url ]");
        }

        private void FindAvailableUpdatesForOnePackage(string packageId, SemanticVersion packageVersion, IVersionSpec packageVersionConstraint, IPackageRepository sourcePackageRepository, bool allowPrereleaseVersions, string contextDisplayName)
        {
            Log.Debug(x => x("Processing package {0}...", packageId));
            var cacheKey = Tuple.Create(packageId, packageVersionConstraint);
            IPackage remotePackage;
            if (!LookupCache.TryGetValue(cacheKey, out remotePackage))
            {
                remotePackage = sourcePackageRepository.FindPackage(packageId, packageVersionConstraint, allowPrereleaseVersions, true);
                LookupCache.Add(cacheKey, remotePackage);
            }
            if (remotePackage == null)
            {
                Log.WarnFormat("Package not found {0}", packageId);
            }
            else if (remotePackage.Version > packageVersion)
            {
                Log.Info(x => x("Update {0} -> {1} found for package {2} in {3}", packageVersion, remotePackage.Version, packageId, contextDisplayName));
            }
            else
            {
                Log.DebugFormat("No new versions found for package {0} in {1}", packageId, contextDisplayName);
            }
        }
    }
}
