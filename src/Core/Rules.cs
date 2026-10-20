using System;
using System.Collections.Generic;
using System.IO;

namespace BinDay.Core
{
    /// <summary>
    /// A directory that is disposable once you know which project it belongs to.
    /// The marker file is what makes the rule safe: without a package.json next to
    /// node_modules we do not touch it.
    /// </summary>
    public sealed class DirRule
    {
        public string Id;
        public string Title;
        public string Why;
        public Category Category;
        public Safety Level;
        public string[] Names;
        public string[] Markers;

        public DirRule(string id, string title, Category category, Safety level, string why, string[] names, string[] markers)
        {
            Id = id;
            Title = title;
            Category = category;
            Level = level;
            Why = why;
            Names = names;
            Markers = markers;
        }
    }

    public static class RuleSet
    {
        private static readonly string[] NodeMarkers = { "package.json" };
        private static readonly string[] DotNetMarkers = { "*.csproj", "*.fsproj", "*.vbproj", "*.sln" };
        private static readonly string[] PythonMarkers = { "pyproject.toml", "requirements.txt", "setup.py", "setup.cfg", "Pipfile" };

        public static readonly DirRule[] Directories = new DirRule[]
        {
            new DirRule("node_modules", "node_modules (npm)", Category.Dev, Safety.Safe,
                "Installed packages. Your project rebuilds them with a single 'npm install'.",
                new[] { "node_modules" }, NodeMarkers),

            new DirRule("next", ".next (Next.js)", Category.Dev, Safety.Safe,
                "Generated on every build. Re-running the build recreates it.",
                new[] { ".next" }, NodeMarkers),

            new DirRule("nuxt", ".nuxt (Nuxt)", Category.Dev, Safety.Safe,
                "Generated on every build. Re-running the build recreates it.",
                new[] { ".nuxt" }, NodeMarkers),

            new DirRule("sveltekit", ".svelte-kit (Svelte)", Category.Dev, Safety.Safe,
                "Generated on every build. Re-running the build recreates it.",
                new[] { ".svelte-kit" }, NodeMarkers),

            new DirRule("angular", ".angular (Angular)", Category.Dev, Safety.Safe,
                "Angular's local build cache. Rebuilt automatically on the next build.",
                new[] { ".angular" }, NodeMarkers),

            new DirRule("turbo", ".turbo (Turborepo)", Category.Dev, Safety.Safe,
                "Task cache for turbo. Rebuilt automatically on the next build.",
                new[] { ".turbo" }, NodeMarkers),

            new DirRule("parcel", ".parcel-cache (Parcel)", Category.Dev, Safety.Safe,
                "Parcel's build cache. Rebuilt automatically on the next build.",
                new[] { ".parcel-cache" }, NodeMarkers),

            new DirRule("coverage", "coverage (test report)", Category.Dev, Safety.Safe,
                "Generated test report. Regenerated the next time you run the tests.",
                new[] { "coverage" }, NodeMarkers),

            new DirRule("dotnet_obj", "obj (.NET build)", Category.Dev, Safety.Safe,
                "Intermediate build files for a .NET project. The next build recreates them.",
                new[] { "obj" }, DotNetMarkers),

            new DirRule("dotnet_bin", "bin (.NET build)", Category.Dev, Safety.Safe,
                "Compiled output for a .NET project. The next build recreates it.",
                new[] { "bin" }, DotNetMarkers),

            new DirRule("vs", ".vs (Visual Studio)", Category.Dev, Safety.Caution,
                "Mostly cached build data, but it can hold a few of your own window layouts. Visual Studio recreates it.",
                new[] { ".vs" }, DotNetMarkers),

            new DirRule("rust_target", "target (Rust build)", Category.Dev, Safety.Safe,
                "Compiled Rust artifacts. 'cargo build' recreates them.",
                new[] { "target" }, new[] { "Cargo.toml" }),

            new DirRule("venv", ".venv (Python)", Category.Dev, Safety.Safe,
                "Installed Python packages for this project. 'pip install -r requirements.txt' recreates it.",
                new[] { ".venv", "venv" }, PythonMarkers),

            new DirRule("pycache", "__pycache__ (Python)", Category.Dev, Safety.Safe,
                "Compiled Python bytecode. Python regenerates it the moment the code runs again.",
                new[] { "__pycache__" }, null),

            new DirRule("pytest_cache", "Python tool caches (.pytest_cache)", Category.Dev, Safety.Safe,
                "Test run cache. Regenerated on the next test run.",
                new[] { ".pytest_cache", ".mypy_cache", ".ruff_cache" }, null),

            new DirRule("dart_tool", ".dart_tool (Flutter)", Category.Dev, Safety.Safe,
                "Generated package metadata and build cache. 'flutter pub get' recreates it.",
                new[] { ".dart_tool" }, new[] { "pubspec.yaml" }),

            new DirRule("gradle_project", ".gradle (project)", Category.Dev, Safety.Safe,
                "Project-local Gradle cache. The next build recreates it. (Your global cache in the home folder is listed separately.)",
                new[] { ".gradle" }, new[] { "settings.gradle", "settings.gradle.kts", "build.gradle", "build.gradle.kts" }),

            new DirRule("zig", ".zig-cache (Zig)", Category.Dev, Safety.Safe,
                "Zig's local build cache. Rebuilt automatically.",
                new[] { ".zig-cache", "zig-out" }, new[] { "build.zig" })
        };

        /// <summary>Folder names we never walk into, never offer to delete.</summary>
        public static readonly string[] ExcludedNames = new[]
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData",
            "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
            "WinSxS", "AppData", "Application Data", "Cookies", "Microsoft",
            ".git", ".svn", ".hg", ".bzr",
            "venv.bak", "site-packages"
        };

        public static DirRule Match(string directoryName)
        {
            for (int i = 0; i < Directories.Length; i++)
            {
                string[] names = Directories[i].Names;
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.Equals(names[n], directoryName, StringComparison.OrdinalIgnoreCase))
                        return Directories[i];
                }
            }
            return null;
        }

        /// <summary>
        /// True when the parent folder holds one of the marker files that make the
        /// rule safe to apply. An empty marker list means the rule is unconditional.
        /// </summary>
        public static bool HasMarker(string parentDirectory, string[] markers)
        {
            if (markers == null || markers.Length == 0) return true;
            if (string.IsNullOrEmpty(parentDirectory)) return false;

            for (int i = 0; i < markers.Length; i++)
            {
                string marker = markers[i];

                if (marker.IndexOf('*') >= 0)
                {
                    string suffix = marker.Substring(marker.IndexOf('*') + 1);
                    try
                    {
                        foreach (string file in Directory.EnumerateFiles(parentDirectory, "*" + suffix))
                            return true;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    continue;
                }

                string candidate = Path.Combine(parentDirectory, marker);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return true;
            }

            return false;
        }

        /// <summary>
        /// Well-known cache locations that are worth checking regardless of which
        /// folders the user asked to scan.
        /// </summary>
        public static List<CacheLocation> DefaultCacheLocations()
        {
            List<CacheLocation> list = new List<CacheLocation>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrEmpty(local))
            {
                Add(list, Path.Combine(local, "npm-cache"), "npm download cache", Category.Cache, Safety.Safe,
                    "Packages npm has already downloaded. Re-downloaded automatically if you ever need them again.");
                Add(list, Path.Combine(local, "Yarn", "Cache"), "Yarn download cache", Category.Cache, Safety.Safe,
                    "Packages Yarn has already downloaded. Re-downloaded automatically if you ever need them again.");
                Add(list, Path.Combine(local, "pnpm", "store"), "pnpm package store", Category.Cache, Safety.Safe,
                    "Packages pnpm has already downloaded. Re-downloaded automatically if you ever need them again.");
                Add(list, Path.Combine(local, "pip", "Cache"), "pip download cache", Category.Cache, Safety.Safe,
                    "Python wheels pip has already downloaded. Re-downloaded automatically if you ever need them again.");
                Add(list, Path.Combine(local, "Temp"), "Temporary files", Category.Temp, Safety.Safe,
                    "Scratch space that applications are supposed to clean up themselves. In-use files are skipped.");
            }

            if (!string.IsNullOrEmpty(profile))
            {
                Add(list, Path.Combine(profile, ".nuget", "packages"), "NuGet package cache", Category.Cache, Safety.Caution,
                    "Every NuGet package on this machine. Projects restore what they need, but the next build will need the network.");
                Add(list, Path.Combine(profile, ".gradle", "caches"), "Gradle cache", Category.Cache, Safety.Caution,
                    "Every Gradle dependency on this machine. Projects re-download what they need, but the next build will need the network.");
                Add(list, Path.Combine(profile, ".m2", "repository"), "Maven repository", Category.Cache, Safety.Caution,
                    "Every Maven artifact on this machine. Projects re-download what they need, but the next build will need the network.");
            }

            return list;
        }

        private static void Add(List<CacheLocation> list, string path, string title, Category category, Safety level, string why)
        {
            list.Add(new CacheLocation { Path = path, Title = title, Why = why, Level = level, Category = category });
        }

        /// <summary>Downloads and Desktop: where installers pile up.</summary>
        public static List<string> DefaultInstallerFolders()
        {
            List<string> list = new List<string>();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
            {
                list.Add(Path.Combine(profile, "Downloads"));
                list.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            return list;
        }
    }
}
