using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using RedundantFinder.Core;

namespace RedundantFinder.Tests
{
    internal static class Program
    {
        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            Run("detects exact duplicates", DetectsDuplicates);
            Run("same size, different bytes are not duplicates", SameSizeDifferentContent);
            Run("minimum size filter", MinimumSizeFilter);
            Run("zero byte files ignored", ZeroByteIgnored);
            Run("excluded directory names (case insensitive)", ExcludedDirectories);
            Run("skip paths", SkipPaths);
            Run("keeper policy: oldest", KeeperOldest);
            Run("keeper policy: newest", KeeperNewest);
            Run("keeper policy: shortest path", KeeperShortest);
            Run("keeper penalty for temp/download folders", KeeperPenalty);
            Run("quarantine moves extras and keeps one", QuarantineMoves);
            Run("quarantine target mirrors the drive", QuarantineMirror);
            Run("quarantine target avoids collisions", QuarantineCollision);
            Run("delete removes extras only", DeleteRemoves);
            Run("csv export", CsvReport);
            Run("cancellation", Cancellation);
            Run("progress reporting", ProgressReporting);

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? string.Format("ALL PASSED ({0} checks)", _checks)
                : string.Format("FAILED: {0} of {1} checks", _failures, _checks));

            return _failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ tests

        private static void DetectsDuplicates()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(8192, 1);
                Write(root, "a.bin", payload);
                Write(root, "sub/b-copy.bin", payload);
                Write(root, "sub/deep/c-copy.bin", payload);
                Write(root, "other.bin", Payload(8192, 2)); // same size, different bytes

                ScanResult result = Scan(root, 1);

                Assert.Equal(1, result.Groups.Count, "one duplicate group");
                Assert.Equal(3, result.Groups[0].Files.Count, "three identical files");
                Assert.Equal(2, result.RedundantCopies, "two redundant copies");
                Assert.Equal(8192L * 2, result.ReclaimableBytes, "reclaimable bytes");
                Assert.NotNull(result.Groups[0].Keeper, "keeper chosen");
                Assert.Equal("a.bin", Path.GetFileName(Shortest(result.Groups[0])), "shortest path kept");
            }
            finally { Cleanup(root); }
        }

        private static void SameSizeDifferentContent()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "x.bin", Payload(4096, 7));
                Write(root, "y.bin", Payload(4096, 8));

                ScanResult result = Scan(root, 1);

                Assert.Equal(0, result.Groups.Count, "no groups");
                Assert.Equal(2, result.Candidates, "both files considered");
            }
            finally { Cleanup(root); }
        }

        private static void MinimumSizeFilter()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "small1.bin", Payload(256, 3));
                Write(root, "small2.bin", Payload(256, 3));

                Assert.Equal(0, Scan(root, 1024).Groups.Count, "ignored below minimum");
                Assert.Equal(1, Scan(root, 1).Groups.Count, "found when minimum is low");
            }
            finally { Cleanup(root); }
        }

        private static void ZeroByteIgnored()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "empty1", new byte[0]);
                Write(root, "empty2", new byte[0]);
                Assert.Equal(0, Scan(root, 0).Groups.Count, "empty files ignored");
            }
            finally { Cleanup(root); }
        }

        private static void ExcludedDirectories()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(4096, 9);
                Write(root, "keep/file.bin", payload);
                Write(root, "Windows/file.bin", payload);
                Write(root, "program files/file.bin", payload); // lower case on purpose

                var options = Options(root, 0);
                options.ExcludedDirNames.AddRange(new[] { "Windows", "Program Files" });

                ScanResult result = Scanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(1, result.FilesSeen, "only the non-excluded file was seen");
                Assert.Equal(0, result.Groups.Count, "no duplicates");
            }
            finally { Cleanup(root); }
        }

        private static void SkipPaths()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(4096, 10);
                Write(root, "data/file.bin", payload);
                Write(root, "reports/file.bin", payload);

                var options = Options(root, 0);
                options.SkipPaths.Add(Path.Combine(root, "reports"));

                ScanResult result = Scanner.Scan(options, null, CancellationToken.None);

                Assert.Equal(1, result.FilesSeen, "skipped folder not scanned");
                Assert.Equal(0, result.Groups.Count, "no duplicates");
            }
            finally { Cleanup(root); }
        }

        private static void KeeperOldest()
        {
            var files = new List<FileEntry>
            {
                Entry("c:/x/old.bin", new DateTime(2020, 1, 1)),
                Entry("c:/x/new.bin", new DateTime(2024, 1, 1)),
                Entry("c:/x/mid.bin", new DateTime(2022, 1, 1))
            };

            Assert.Equal("old.bin", Path.GetFileName(Scanner.ChooseKeeper(files, KeepPolicy.Oldest).Path), "oldest wins");
        }

        private static void KeeperNewest()
        {
            var files = new List<FileEntry>
            {
                Entry("c:/x/old.bin", new DateTime(2020, 1, 1)),
                Entry("c:/x/new.bin", new DateTime(2024, 1, 1)),
                Entry("c:/x/mid.bin", new DateTime(2022, 1, 1))
            };

            Assert.Equal("new.bin", Path.GetFileName(Scanner.ChooseKeeper(files, KeepPolicy.Newest).Path), "newest wins");
        }

        private static void KeeperShortest()
        {
            var files = new List<FileEntry>
            {
                Entry("c:/x/very/deep/folder/file.bin", new DateTime(2019, 1, 1)),
                Entry("c:/x/short.bin", new DateTime(2024, 1, 1))
            };

            Assert.Equal("short.bin", Path.GetFileName(Scanner.ChooseKeeper(files, KeepPolicy.ShortestPath).Path), "shortest path wins");
        }

        private static void KeeperPenalty()
        {
            var files = new List<FileEntry>
            {
                Entry(@"c:\Users\me\Downloads\photo.jpg", new DateTime(2019, 1, 1)),
                Entry(@"c:\Users\me\Pictures\photo.jpg", new DateTime(2024, 1, 1))
            };

            // Oldest would normally keep Downloads, but temp/download copies are penalised.
            string keeper = Scanner.ChooseKeeper(files, KeepPolicy.Oldest).Path;
            Assert.True(keeper.EndsWith(@"Pictures\photo.jpg", StringComparison.Ordinal), "Downloads copy is not kept");
        }

        private static void QuarantineMoves()
        {
            string root = NewTempRoot();
            string quarantine = Path.Combine(root, "_quarantine");
            try
            {
                byte[] payload = Payload(5000, 11);
                Write(root, "scan/a.bin", payload);
                Write(root, "scan/sub/b.bin", payload);

                ScanResult result = Scan(Path.Combine(root, "scan"), 0);
                Assert.Equal(1, result.Groups.Count, "one group");

                CleanupOutcome outcome = Cleaner.Run(result.Groups, CleanupMode.Quarantine, quarantine, null, CancellationToken.None);

                Assert.Equal(1, outcome.Succeeded, "one file quarantined");
                Assert.Equal(0, outcome.Failed, "no failures");
                Assert.Equal(5000L, outcome.BytesFreed, "bytes freed");

                Assert.Equal(1, Directory.GetFiles(Path.Combine(root, "scan"), "*", SearchOption.AllDirectories).Length, "one copy left behind");
                Assert.Equal(1, Directory.GetFiles(quarantine, "*", SearchOption.AllDirectories).Length, "one file in quarantine");

                string survivor = Directory.GetFiles(Path.Combine(root, "scan"), "*", SearchOption.AllDirectories)[0];
                Assert.Equal("a.bin", Path.GetFileName(survivor), "keeper survived");
            }
            finally { Cleanup(root); }
        }

        private static void QuarantineMirror()
        {
            string target = Cleaner.BuildQuarantineTarget(@"C:\Q", @"C:\Users\me\photo.jpg");
            Assert.Equal(@"C:/Q/C/Users/me/photo.jpg", Normalize(target), "drive letter becomes a folder");
        }

        private static void QuarantineCollision()
        {
            string root = NewTempRoot();
            try
            {
                string quarantine = Path.Combine(root, "q");
                string source = Path.Combine(root, "a.jpg");
                File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

                string first = Cleaner.BuildQuarantineTarget(quarantine, source);
                Directory.CreateDirectory(Path.GetDirectoryName(first));
                File.WriteAllBytes(first, new byte[] { 9 });

                string second = Cleaner.BuildQuarantineTarget(quarantine, source);
                Assert.True(second != first, "second target differs");
                Assert.True(second.Contains("_dup1"), "collision suffix added");
            }
            finally { Cleanup(root); }
        }

        private static void DeleteRemoves()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(7000, 12);
                Write(root, "a.bin", payload);
                Write(root, "b.bin", payload);
                Write(root, "c.bin", payload);

                ScanResult result = Scan(root, 0);
                Assert.Equal(1, result.Groups.Count, "one group");

                CleanupOutcome outcome = Cleaner.Run(result.Groups, CleanupMode.Delete, null, null, CancellationToken.None);

                Assert.Equal(2, outcome.Succeeded, "two files deleted");
                Assert.Equal(2 * 7000L, outcome.BytesFreed, "bytes freed");
                Assert.Equal(1, Directory.GetFiles(root).Length, "exactly one file remains");
            }
            finally { Cleanup(root); }
        }

        private static void CsvReport()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(3000, 13);
                Write(root, "a,with,commas.bin", payload);
                Write(root, "copy.bin", payload);

                ScanResult result = Scan(root, 0);
                string csv = Path.Combine(root, "out.csv");
                CsvExport.Write(result, csv);

                string[] lines = File.ReadAllLines(csv);
                Assert.Equal(2, lines.Length, "header plus one row");
                Assert.True(lines[0].StartsWith("Path,SizeBytes"), "header present");
                Assert.True(lines[1].StartsWith("\""), "path field is quoted");
                Assert.True(lines[1].Contains("a,with,commas.bin"), "commas survive inside the quoted field");
            }
            finally { Cleanup(root); }
        }

        private static void Cancellation()
        {
            string root = NewTempRoot();
            try
            {
                Write(root, "a.bin", Payload(1000, 14));

                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    bool threw = false;
                    try { Scanner.Scan(Options(root, 0), null, cts.Token); }
                    catch (OperationCanceledException) { threw = true; }
                    Assert.True(threw, "scan honours cancellation");
                }
            }
            finally { Cleanup(root); }
        }

        private static void ProgressReporting()
        {
            string root = NewTempRoot();
            try
            {
                byte[] payload = Payload(2048, 15);
                Write(root, "a.bin", payload);
                Write(root, "b.bin", payload);
                Write(root, "c.bin", payload);

                var progress = new SyncProgress();
                Scanner.Scan(Options(root, 0), progress, CancellationToken.None);

                Assert.True(progress.Items.Count > 0, "progress reported");
                Assert.True(progress.Items.Any(p => p.Phase == "Scanning"), "scanning phase");
                Assert.True(progress.Items.Any(p => p.Phase == "Comparing"), "comparing phase");
                Assert.Equal("Done", progress.Items[progress.Items.Count - 1].Phase, "finishes with Done");
                Assert.Equal(1, progress.Items[progress.Items.Count - 1].GroupsFound, "group count reported");
            }
            finally { Cleanup(root); }
        }

        // ------------------------------------------------------------------ helpers

        private sealed class SyncProgress : IProgress<ScanProgress>
        {
            public readonly List<ScanProgress> Items = new List<ScanProgress>();

            public void Report(ScanProgress value)
            {
                Items.Add(value);
            }
        }

        private static ScanOptions Options(string root, long minBytes)
        {
            return new ScanOptions
            {
                Roots = new List<string> { root },
                MinSizeBytes = minBytes,
                Keep = KeepPolicy.ShortestPath
            };
        }

        private static ScanResult Scan(string root, long minBytes)
        {
            return Scanner.Scan(Options(root, minBytes), null, CancellationToken.None);
        }

        private static FileEntry Entry(string path, DateTime created)
        {
            return new FileEntry { Path = path, Size = 10, CreatedUtc = created, LastWriteUtc = created };
        }

        private static byte[] Payload(int size, int seed)
        {
            var data = new byte[size];
            var random = new Random(seed);
            random.NextBytes(data);
            return data;
        }

        private static string Shortest(DuplicateGroup group)
        {
            return group.Keeper.Path;
        }

        private static string NewTempRoot()
        {
            string path = Path.Combine(Path.GetTempPath(), "rf-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Write(string root, string relative, byte[] data)
        {
            string full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, data);
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void Cleanup(string root)
        {
            try { Directory.Delete(root, true); } catch { }
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
