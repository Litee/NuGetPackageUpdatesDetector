# NuGetPackageUpdatesDetector
Command-line tool for detecting available updates of NuGet packages.

Usage: NuGetPackageUpdatesDetector.exe (path-to-packages-config-file | path-to-visual-studio-solution-file | path-to-solution-cop-config) [ -Prerelease ] [ -Verbose ] [ -Source nuget-feed-url ]

SolutionCop config file format allows to specify version ranges. More details here: https://github.com/Litee/SolutionCop/wiki/NuGetPackageVersions

### How to get?

NuGet.exe Install NuGetPackageUpdatesDetector

### TODOs

* Take platform into account
* Support multiple NuGet sources

### Recent changes

0.1.0 - First stable version