using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using BinDay.Core;

namespace BinDay.Tests
{
    internal static class Program
    {
        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            Run("node_modules needs a package.json", NodeModulesNeedsMarker);
            Run("node_modules with a package.json is found", NodeModulesFound);
            Run(".NET bin and obj are found", DotNetBinObj);
            Run("Rust target is found", RustTarget);
            Run("Python virtualenv is found", PythonVenv);
            Run("Python virtualenv without a marker is ignored", PythonVenvNoMarker);
            Run("__pycache__ needs no marker", PycacheUnconditional);
            Run("Gradle project cache is found", GradleProjectCache);
            Run("Dart .dart_tool is found", DartTool);
            Run("known cache locations are reported", CacheLocations);
            Run("old big installers are reported", OldInstallers);
            Run("small installers are ignored", SmallInstallers);
            Run("recent installers are ignored", RecentInstallers);
            Run("duplicates are found", DuplicatesFound);
            Run("same size different bytes are not duplicates", DifferentContent);
            Run("duplicates below the size floor are ignored", DuplicateSizeFloor);
            Run("duplicates inside build output are ignored", DuplicatesInsideArtifacts);
            Run("the keeper is the shortest path", DuplicateKeeper);
            Run("sizes are measured correctly", MeasureSizes);
            Run("excluded folders are never walked", ExcludedFolders);
            Run("artifacts are not counted twice", NoDoubleCount);
            Run("protected paths are refused", PathGuardRules);
            Run("cleanup moves files to the bin", CleanupRecycles);
            Run("permanent cleanup deletes files", CleanupPermanent);
            Run("permanent cleanup refuses unsafe items", CleanupRefusesUnsafe);
            Run("cleanup refuses protected paths", CleanupRefusesProtected);
            Run("history is written as json", HistoryJson);
            Run("history can be read back", HistoryRoundTrip);
            Run("cancelling a scan works", Cancellation);
            Run("byte formatting", FormatBytes);

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? string.Format("ALL PASSED ({0} checks)", _checks)
                : string.Format("FAILED: {0} of {1} checks", _failures, _checks));

            return _failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ tests

        private static void NodeModulesNeedsMarker()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "node_modules/pkg/index.js", "// nothing");

                ScanResult result = Scan(root);

                Assert.Equal(0, result.Findings.Count, "no findings without package.json");
            }
            finally { Delete(root); }
        }

        private static void NodeModulesFound()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");
                Write(root, "node_modules/pkg/big.bin", Payload(2048, 7));

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "one finding");
                Assert.Equal("node_modules (npm)", result.Findings[0].Title, "title names the folder");
                Assert.Equal(Safety.Safe, result.Findings[0].Level, "level");
                Assert.Equal(2, result.Findings[0].FileCount, "file count");
                Assert.True(result.Findings[0].Bytes >= 2048, "size");
                Assert.True(result.Findings[0].MarkerPath != null && result.Findings[0].MarkerPath.EndsWith("package.json"), "marker recorded");
            }
            finally { Delete(root); }
        }

        private static void DotNetBinObj()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "App.csproj", "<Project />");
                Write(root, "obj/Debug/x.tmp", "0123456789");
                Write(root, "bin/Release/app.dll", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(2, result.Findings.Count, "bin and obj found");
                Assert.True(result.Findings.All(f => f.Category == Category.Dev), "dev category");
            }
            finally { Delete(root); }
        }

        private static void RustTarget()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "Cargo.toml", "[package]");
                Write(root, "target/debug/app.bin", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "target found");
            }
            finally { Delete(root); }
        }

        private static void PythonVenv()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "requirements.txt", "flask");
                Write(root, ".venv/lib/site.py", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "venv found");
            }
            finally { Delete(root); }
        }

        private static void PythonVenvNoMarker()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "random/venv/lib/site.py", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(0, result.Findings.Count, "venv without a marker is ignored");
            }
            finally { Delete(root); }
        }

        private static void PycacheUnconditional()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "somewhere/deep/__pycache__/mod.pyc", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "__pycache__ found");
            }
            finally { Delete(root); }
        }

        private static void GradleProjectCache()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "settings.gradle", "rootProject.name='x'");
                Write(root, ".gradle/8.0/cache.bin", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "project .gradle found");
            }
            finally { Delete(root); }
        }

        private static void DartTool()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "pubspec.yaml", "name: app");
                Write(root, ".dart_tool/package_config.json", "{}");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, ".dart_tool found");
            }
            finally { Delete(root); }
        }

        private static void CacheLocations()
        {
            string root = NewTempRoot();
            try
            {
                string cache = Path.Combine(root, "npm-cache");
                Directory.CreateDirectory(cache);
                Write(root, "npm-cache/_cacache/index", "0123456789");

                ScanOptions options = NewOptions(root);
                options.ScanCaches = true;
                options.CacheLocations.Add(new CacheLocation
                {
                    Path = cache,
                    Title = "npm download cache",
                    Why = "Re-downloaded automatically.",
                    Level = Safety.Safe,
                    Category = Category.Cache
                });

                ScanResult result = DiskScanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(1, result.Findings.Count, "cache found");
                Assert.Equal(Category.Cache, result.Findings[0].Category, "cache category");
                Assert.Equal("npm download cache", result.Findings[0].Title, "cache title");
                Assert.Equal(1, result.Findings[0].DeletePaths.Count, "one path to delete");
            }
            finally { Delete(root); }
        }

        private static void OldInstallers()
        {
            string root = NewTempRoot();
            try
            {
                string downloads = Path.Combine(root, "Downloads");
                Directory.CreateDirectory(downloads);
                string file = Path.Combine(downloads, "setup.exe");
                File.WriteAllBytes(file, Payload(1024, 3));
                File.SetLastWriteTime(file, DateTime.Now.AddDays(-90));

                ScanOptions options = NewOptions(root);
                options.ScanInstallers = true;
                options.InstallerMinBytes = 1000;
                options.InstallerFolders.Add(downloads);

                ScanResult result = DiskScanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(1, result.Findings.Count, "installer found");
                Assert.Equal(Category.Installer, result.Findings[0].Category, "installer category");
                Assert.Equal(Safety.Review, result.Findings[0].Level, "installers are only reviewed");
            }
            finally { Delete(root); }
        }

        private static void SmallInstallers()
        {
            string root = NewTempRoot();
            try
            {
                string downloads = Path.Combine(root, "Downloads");
                Directory.CreateDirectory(downloads);
                string file = Path.Combine(downloads, "tiny.exe");
                File.WriteAllBytes(file, Payload(16, 3));
                File.SetLastWriteTime(file, DateTime.Now.AddDays(-90));

                ScanOptions options = NewOptions(root);
                options.InstallerFolders.Add(downloads);

                ScanResult result = DiskScanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(0, result.Findings.Count, "small installer ignored");
            }
            finally { Delete(root); }
        }

        private static void RecentInstallers()
        {
            string root = NewTempRoot();
            try
            {
                string downloads = Path.Combine(root, "Downloads");
                Directory.CreateDirectory(downloads);
                string file = Path.Combine(downloads, "fresh.exe");
                File.WriteAllBytes(file, Payload(1024, 3));

                ScanOptions options = NewOptions(root);
                options.InstallerFolders.Add(downloads);

                ScanResult result = DiskScanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(0, result.Findings.Count, "recent installer ignored");
            }
            finally { Delete(root); }
        }

        private static void DuplicatesFound()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(4096, 11);
                Write(root, "a.bin", payload);
                Write(root, "sub/b.bin", payload);
                Write(root, "sub/deep/c.bin", payload);
                Write(root, "other.bin", Payload(4096, 12));

                ScanResult result = Scan(root, true);

                Finding duplicates = result.Findings.FirstOrDefault(f => f.IsDuplicateGroup);
                Assert.NotNull(duplicates, "duplicate group found");
                Assert.Equal(2, duplicates.DeletePaths.Count, "two copies to remove");
                Assert.Equal(2, duplicates.FileCount, "two extra files");
            }
            finally { Delete(root); }
        }

        private static void DifferentContent()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "a.bin", Payload(4096, 11));
                Write(root, "b.bin", Payload(4096, 12));

                ScanResult result = Scan(root, true);

                Assert.Equal(0, result.Findings.Count(f => f.IsDuplicateGroup), "no duplicates");
            }
            finally { Delete(root); }
        }

        private static void DuplicateSizeFloor()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(128, 11);
                Write(root, "a.bin", payload);
                Write(root, "b.bin", payload);

                ScanOptions options = NewOptions(root);
                options.DuplicateMinBytes = 1024;

                ScanResult result = DiskScanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(0, result.Findings.Count(f => f.IsDuplicateGroup), "below the floor");
            }
            finally { Delete(root); }
        }

        private static void DuplicatesInsideArtifacts()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                byte[] payload = Payload(4096, 31);
                Write(root, "node_modules/pkg/a.bin", payload);
                Write(root, "node_modules/pkg/b.bin", payload);
                Write(root, "kept/a.bin", payload);
                Write(root, "kept/b.bin", payload);

                ScanResult result = Scan(root, true);

                Finding duplicates = result.Findings.FirstOrDefault(f => f.IsDuplicateGroup);
                Assert.NotNull(duplicates, "duplicate group found");
                Assert.Equal(1, duplicates.DeletePaths.Count, "only the copy outside build output is offered");
                Assert.True(!duplicates.DeletePaths[0].Contains("node_modules"), "node_modules copy is left alone");
            }
            finally { Delete(root); }
        }

        private static void DuplicateKeeper()        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(4096, 21);
                Write(root, "a.bin", payload);
                Write(root, "deeper/nested/b.bin", payload);

                ScanResult result = Scan(root, true);

                Finding duplicates = result.Findings.FirstOrDefault(f => f.IsDuplicateGroup);
                Assert.NotNull(duplicates, "duplicate group found");
                Assert.True(duplicates.KeeperPath.EndsWith("a.bin"), "shallowest path is kept");
            }
            finally { Delete(root); }
        }

        private static void MeasureSizes()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "one.bin", Payload(100, 1));
                Write(root, "sub/two.bin", Payload(200, 2));

                long bytes;
                int files;
                DiskScanner.Measure(root, out bytes, out files, CancellationToken.None);

                Assert.Equal(300L, bytes, "total bytes");
                Assert.Equal(2, files, "file count");
            }
            finally { Delete(root); }
        }

        private static void ExcludedFolders()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "AppData/package.json", "{ }");
                Write(root, "AppData/node_modules/pkg/index.js", "0123456789");
                Write(root, "Windows/package.json", "{ }");
                Write(root, "Windows/node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(0, result.Findings.Count, "nothing inside excluded folders");
            }
            finally { Delete(root); }
        }

        private static void NoDoubleCount()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/inner/package.json", "{ }");
                Write(root, "node_modules/inner/node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);

                Assert.Equal(1, result.Findings.Count, "counted once");
            }
            finally { Delete(root); }
        }

        private static void PathGuardRules()
        {
            Assert.True(PathGuard.IsProtected(Path.GetPathRoot(Environment.CurrentDirectory) ?? "/"), "drive root refused");
            Assert.True(PathGuard.IsProtected(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "profile root refused");
            Assert.True(PathGuard.IsProtected(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory) ?? "/", "Windows")), "Windows folder refused");
            Assert.True(PathGuard.IsProtected(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory) ?? "/", "Program Files")), "Program Files refused");

            string root = NewTempRoot();
            try
            {
                Assert.True(!PathGuard.IsProtected(root), "ordinary folder allowed");
                Assert.True(!PathGuard.IsProtected(Path.Combine(root, "node_modules")), "ordinary subfolder allowed");
            }
            finally { Delete(root); }
        }

        private static void CleanupRecycles()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);
                Assert.Equal(1, result.Findings.Count, "one finding");

                string bin = Path.Combine(root, "bin");
                CleanupReport report = Cleaner.Run(result.Findings, DeleteMode.Recycle, new FolderRecycler(bin), null, CancellationToken.None);

                Assert.Equal(1, report.Removed, "one item removed");
                Assert.Equal(0, report.Failed, "no failures");
                Assert.True(report.BytesFreed > 0, "bytes freed reported");
                Assert.True(!Directory.Exists(Path.Combine(root, "node_modules")), "folder gone");
                Assert.True(Directory.GetFileSystemEntries(bin).Length == 1, "one item in the bin");
            }
            finally { Delete(root); }
        }

        private static void CleanupPermanent()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);

                CleanupReport report = Cleaner.Run(result.Findings, DeleteMode.Permanent, new FolderRecycler(Path.Combine(root, "bin")), null, CancellationToken.None);

                Assert.Equal(1, report.Removed, "one item removed");
                Assert.True(!Directory.Exists(Path.Combine(root, "node_modules")), "folder gone");
            }
            finally { Delete(root); }
        }

        private static void CleanupRefusesUnsafe()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);
                result.Findings[0].Level = Safety.Caution;

                CleanupReport report = Cleaner.Run(result.Findings, DeleteMode.Permanent, new FolderRecycler(Path.Combine(root, "bin")), null, CancellationToken.None);

                Assert.Equal(0, report.Removed, "nothing removed");
                Assert.Equal(1, report.Refused, "refused for safety");
                Assert.True(Directory.Exists(Path.Combine(root, "node_modules")), "still there");
            }
            finally { Delete(root); }
        }

        private static void CleanupRefusesProtected()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");

                ScanResult result = Scan(root);
                result.Findings[0].DeletePaths = new List<string> { Path.GetPathRoot(Environment.CurrentDirectory) ?? "/" };

                CleanupReport report = Cleaner.Run(result.Findings, DeleteMode.Permanent, new FolderRecycler(Path.Combine(root, "bin")), null, CancellationToken.None);

                Assert.Equal(0, report.Removed, "nothing removed");
                Assert.Equal(1, report.Refused, "refused");
            }
            finally { Delete(root); }
        }

        private static void HistoryJson()
        {
            string root = NewTempRoot();
            try
            {
                CleanupReport report = new CleanupReport();
                report.Removed = 2;
                report.BytesFreed = 1234;
                report.Items.Add(new CleanupItem { Path = "C:\\thing\\x", Bytes = 1234, Success = true });

                string file = Path.Combine(root, "history.jsonl");
                History.Append(report, DeleteMode.Recycle, null, file);

                Assert.True(File.Exists(file), "history written");

                string line = File.ReadAllText(file).Trim();
                Assert.True(line.StartsWith("{"), "json object");
                Assert.True(line.Contains("\"mode\":\"Recycle\""), "mode recorded");
                Assert.True(line.Contains("\"bytes\":1234"), "bytes recorded");
                Assert.True(line.Contains("C:\\\\thing\\\\x"), "path escaped");
            }
            finally { Delete(root); }
        }

        private static void HistoryRoundTrip()
        {
            string root = NewTempRoot();
            try
            {
                string file = Path.Combine(root, "history.jsonl");

                CleanupReport report = new CleanupReport();
                report.Removed = 2;
                report.BytesFreed = 4096;
                report.Items.Add(new CleanupItem { Path = "C:\\work\\node_modules", Bytes = 4096, Success = true });
                report.Items.Add(new CleanupItem { Path = "C:\\work\\target", Bytes = 0, Success = true });

                History.Append(report, DeleteMode.Recycle, null, file);

                List<HistoryEntry> entries = History.ReadAll(file);

                Assert.Equal(1, entries.Count, "one entry");
                Assert.Equal(2, entries[0].Removed, "removed count");
                Assert.Equal(4096L, entries[0].Bytes, "bytes");
                Assert.Equal("Recycle", entries[0].Mode, "mode");
                Assert.Equal(2, entries[0].Paths.Count, "paths");
                Assert.True(entries[0].Paths[0].Contains("node_modules"), "path kept");
                Assert.True(entries[0].At > DateTime.Now.AddMinutes(-5), "timestamp");

                History.Clear(file);
                Assert.Equal(0, History.ReadAll(file).Count, "history cleared");
            }
            finally { Delete(root); }
        }

        private static void Cancellation()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "package.json", "{ }");
                Write(root, "node_modules/pkg/index.js", "0123456789");

                CancellationTokenSource source = new CancellationTokenSource();
                source.Cancel();

                bool threw = false;
                try
                {
                    DiskScanner.Scan(NewOptions(root), null, source.Token);
                }
                catch (OperationCanceledException)
                {
                    threw = true;
                }

                Assert.True(threw, "scan cancelled");
            }
            finally { Delete(root); }
        }

        private static void FormatBytes()
        {
            Assert.Equal("0 B", Format.Bytes(0), "zero");
            Assert.Equal("1.0 KB", Format.Bytes(1024), "kilobytes");
            Assert.Equal("1.0 MB", Format.Bytes(1024 * 1024), "megabytes");
            Assert.Equal("1.00 GB", Format.Bytes(1024L * 1024L * 1024L), "gigabytes");
        }

        // ---------------------------------------------------------------- helpers

        private static ScanOptions NewOptions(string root)
        {
            ScanOptions options = new ScanOptions();
            options.Roots.Add(root);
            options.ScanDevArtifacts = true;
            options.ScanCaches = false;
            options.ScanInstallers = false;
            options.ScanDuplicates = false;
            options.DuplicateMinBytes = 1024;
            return options;
        }

        private static ScanResult Scan(string root)
        {
            return DiskScanner.Scan(NewOptions(root), null, CancellationToken.None);
        }

        private static ScanResult Scan(string root, bool duplicates)
        {
            ScanOptions options = NewOptions(root);
            options.ScanDuplicates = duplicates;
            return DiskScanner.Scan(options, null, CancellationToken.None);
        }

        private static string NewTempRoot()
        {
            string path = Path.Combine(Path.GetTempPath(), "binday-test-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Delete(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void Write(string root, string relative, string content)
        {
            string path = Path.Combine(root, relative);
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, content);
        }

        private static void Write(string root, string relative, byte[] content)
        {
            string path = Path.Combine(root, relative);
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, content);
        }

        private static byte[] Payload(int size, int seed)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++) data[i] = (byte)((i * 31 + seed * 17) & 0xFF);
            return data;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("  ok    " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("  FAIL  " + name);
                Console.WriteLine("        " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static class Assert
        {
            public static void True(bool condition, string what)
            {
                _checks++;
                if (!condition) throw new Exception("expected true: " + what);
            }

            public static void Equal<T>(T expected, T actual, string what)
            {
                _checks++;
                if (!EqualityComparer<T>.Default.Equals(expected, actual))
                {
                    throw new Exception(string.Format("{0}: expected [{1}] but got [{2}]", what, expected, actual));
                }
            }

            public static void NotNull(object value, string what)
            {
                _checks++;
                if (value == null) throw new Exception("expected not null: " + what);
            }
        }
    }
}
