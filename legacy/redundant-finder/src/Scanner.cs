using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RedundantFinder.Core
{
    public enum KeepPolicy
    {
        Oldest = 0,
        Newest = 1,
        ShortestPath = 2
    }

    public enum CleanupMode
    {
        Quarantine = 0,
        Delete = 1
    }

    public sealed class ScanOptions
    {
        public List<string> Roots = new List<string>();
        public long MinSizeBytes = 1024L * 1024L;
        public List<string> ExcludedDirNames = new List<string>();
        public List<string> SkipPaths = new List<string>();
        public KeepPolicy Keep = KeepPolicy.Oldest;
        public int PartialHashBytes = 65536;
    }

    public sealed class ScanProgress
    {
        public string Phase = string.Empty;
        public long FilesSeen;
        public long Candidates;
        public long GroupsFound;
        public string CurrentFolder = string.Empty;
    }

    public sealed class FileEntry
    {
        public string Path;
        public long Size;
        public DateTime CreatedUtc;
        public DateTime LastWriteUtc;
    }

    public sealed class DuplicateGroup
    {
        public string Hash;
        public long Size;
        public List<FileEntry> Files = new List<FileEntry>();
        public FileEntry Keeper;

        public long WastedBytes
        {
            get { return Size * (Files.Count - 1); }
        }
    }

    public sealed class ScanResult
    {
        public List<DuplicateGroup> Groups = new List<DuplicateGroup>();
        public long FilesSeen;
        public long Candidates;
        public long FoldersScanned;
        public long ReclaimableBytes;
        public long RedundantCopies;
        public long ErrorCount;
        public TimeSpan Elapsed;
        public List<string> Errors = new List<string>();
    }

    public sealed class CleanupRecord
    {
        public string Source;
        public string Target;
        public bool Success;
        public string Error;
    }

    public sealed class CleanupOutcome
    {
        public List<CleanupRecord> Records = new List<CleanupRecord>();
        public int Succeeded;
        public int Failed;
        public long BytesFreed;
    }

    /// <summary>
    /// Platform-independent duplicate detection: size grouping, then a 64 KB prefix
    /// hash, then a full SHA-256. No UI dependencies, so it can be unit tested anywhere.
    /// </summary>
    public static class Scanner
    {
        private const int ErrorCap = 100;

        private static readonly HashSet<string> SystemFileNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pagefile.sys", "hiberfil.sys", "swapfile.sys", "DumpStack.log.tmp",
                "ntuser.dat", "ntuser.dat.log1", "ntuser.dat.log2"
            };

        public static ScanResult Scan(ScanOptions options, IProgress<ScanProgress> progress, CancellationToken token)
        {
            if (options == null) throw new ArgumentNullException("options");

            var result = new ScanResult();
            var watch = Stopwatch.StartNew();
            var state = new ScanProgress();
            var bySize = new Dictionary<long, List<FileEntry>>();
            var excluded = new HashSet<string>(options.ExcludedDirNames, StringComparer.OrdinalIgnoreCase);
            var skipPaths = new HashSet<string>(
                options.SkipPaths.ConvertAll(p => TrimSeparators(p)), StringComparer.OrdinalIgnoreCase);
            long reportedAt = 0;

            foreach (string rawRoot in options.Roots)
            {
                token.ThrowIfCancellationRequested();
                string root = TrimSeparators(rawRoot);
                if (!Directory.Exists(root)) continue;

                state.Phase = "Scanning";
                state.CurrentFolder = root;
                Report(progress, state, true);

                var stack = new Stack<string>();
                stack.Push(root);

                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string dir = stack.Pop();

                    DirectoryInfo di;
                    try { di = new DirectoryInfo(dir); }
                    catch { continue; }

                    if (!di.Exists) continue;
                    result.FoldersScanned++;
                    state.CurrentFolder = dir;

                    try
                    {
                        foreach (FileInfo file in di.EnumerateFiles())
                        {
                            token.ThrowIfCancellationRequested();
                            state.FilesSeen++;

                            if (state.FilesSeen - reportedAt >= 400)
                            {
                                reportedAt = state.FilesSeen;
                                Report(progress, state, false);
                            }

                            FileEntry entry = ToCandidate(file, options.MinSizeBytes);
                            if (entry == null) continue;

                            state.Candidates++;
                            List<FileEntry> list;
                            if (!bySize.TryGetValue(entry.Size, out list))
                            {
                                list = new List<FileEntry>(2);
                                bySize[entry.Size] = list;
                            }
                            list.Add(entry);
                        }
                    }
                    catch (UnauthorizedAccessException ex) { AddError(result, dir, ex); }
                    catch (IOException ex) { AddError(result, dir, ex); }
                    catch (System.Security.SecurityException ex) { AddError(result, dir, ex); }

                    try
                    {
                        foreach (DirectoryInfo sub in di.EnumerateDirectories())
                        {
                            token.ThrowIfCancellationRequested();

                            if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (excluded.Contains(sub.Name)) continue;
                            if (skipPaths.Contains(TrimSeparators(sub.FullName))) continue;

                            stack.Push(sub.FullName);
                        }
                    }
                    catch (UnauthorizedAccessException ex) { AddError(result, dir, ex); }
                    catch (IOException ex) { AddError(result, dir, ex); }
                    catch (System.Security.SecurityException ex) { AddError(result, dir, ex); }
                }
            }

            Report(progress, state, true);

            // Stage 2: partial hash inside each size bucket
            state.Phase = "Comparing";
            Report(progress, state, true);

            long compared = 0;
            foreach (KeyValuePair<long, List<FileEntry>> bucket in bySize)
            {
                token.ThrowIfCancellationRequested();
                if (bucket.Value.Count < 2) continue;

                var byPrefix = new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);

                foreach (FileEntry entry in bucket.Value)
                {
                    token.ThrowIfCancellationRequested();
                    string prefixHash;
                    try { prefixHash = HashPrefix(entry.Path, options.PartialHashBytes, token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        AddError(result, entry.Path, ex);
                        continue;
                    }

                    compared++;
                    if (compared % 200 == 0)
                    {
                        state.CurrentFolder = entry.Path;
                        Report(progress, state, false);
                    }

                    List<FileEntry> list;
                    if (!byPrefix.TryGetValue(prefixHash, out list))
                    {
                        list = new List<FileEntry>(2);
                        byPrefix[prefixHash] = list;
                    }
                    list.Add(entry);
                }

                foreach (List<FileEntry> prefixGroup in byPrefix.Values)
                {
                    token.ThrowIfCancellationRequested();
                    if (prefixGroup.Count < 2) continue;

                    var byFull = new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);

                    foreach (FileEntry entry in prefixGroup)
                    {
                        token.ThrowIfCancellationRequested();
                        string fullHash;
                        try { fullHash = HashFile(entry.Path, token); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            AddError(result, entry.Path, ex);
                            continue;
                        }

                        List<FileEntry> list;
                        if (!byFull.TryGetValue(fullHash, out list))
                        {
                            list = new List<FileEntry>(2);
                            byFull[fullHash] = list;
                        }
                        list.Add(entry);
                    }

                    foreach (KeyValuePair<string, List<FileEntry>> fullGroup in byFull)
                    {
                        if (fullGroup.Value.Count < 2) continue;

                        var group = new DuplicateGroup
                        {
                            Hash = fullGroup.Key,
                            Size = fullGroup.Value[0].Size,
                            Files = fullGroup.Value
                        };
                        group.Keeper = ChooseKeeper(group.Files, options.Keep);
                        result.Groups.Add(group);
                        state.GroupsFound = result.Groups.Count;
                        result.ReclaimableBytes += group.WastedBytes;
                        result.RedundantCopies += group.Files.Count - 1;
                    }
                }
            }

            result.Groups.Sort(delegate (DuplicateGroup a, DuplicateGroup b)
            {
                int byWaste = b.WastedBytes.CompareTo(a.WastedBytes);
                return byWaste != 0 ? byWaste : b.Files.Count.CompareTo(a.Files.Count);
            });

            result.FilesSeen = state.FilesSeen;
            result.Candidates = state.Candidates;
            watch.Stop();
            result.Elapsed = watch.Elapsed;

            state.Phase = "Done";
            Report(progress, state, true);

            return result;
        }

        public static FileEntry ChooseKeeper(List<FileEntry> files, KeepPolicy policy)
        {
            if (files == null || files.Count == 0) return null;

            FileEntry best = files[0];
            for (int i = 1; i < files.Count; i++)
            {
                if (CompareForKeeper(files[i], best, policy) < 0) best = files[i];
            }
            return best;
        }

        // Negative = a is the better keeper.
        private static int CompareForKeeper(FileEntry a, FileEntry b, KeepPolicy policy)
        {
            int penalty = KeeperPenalty(a).CompareTo(KeeperPenalty(b));
            if (penalty != 0) return penalty;

            if (policy == KeepPolicy.Oldest)
            {
                int created = a.CreatedUtc.CompareTo(b.CreatedUtc);
                if (created != 0) return created;
            }
            else if (policy == KeepPolicy.Newest)
            {
                int created = b.CreatedUtc.CompareTo(a.CreatedUtc);
                if (created != 0) return created;
            }

            int length = a.Path.Length.CompareTo(b.Path.Length);
            if (length != 0) return length;

            return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static int KeeperPenalty(FileEntry entry)
        {
            string p = entry.Path.Replace('/', '\\');
            if (p.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (p.IndexOf("\\tmp\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (p.IndexOf("\\Cache\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (p.IndexOf("\\CrashDumps\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (p.IndexOf("\\Downloads\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (p.IndexOf("\\.Trash\\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 0;
        }

        private static FileEntry ToCandidate(FileInfo file, long minSizeBytes)
        {
            FileAttributes attributes;
            long length;
            DateTime created;
            DateTime modified;

            try
            {
                attributes = file.Attributes;
                length = file.Length;
            }
            catch { return null; }

            if ((attributes & FileAttributes.ReparsePoint) != 0) return null;
            if (length <= 0) return null;
            if (length < minSizeBytes) return null;
            if (SystemFileNames.Contains(file.Name)) return null;

            try { created = file.CreationTimeUtc; } catch { created = DateTime.MinValue; }
            try { modified = file.LastWriteTimeUtc; } catch { modified = DateTime.MinValue; }

            return new FileEntry
            {
                Path = file.FullName,
                Size = length,
                CreatedUtc = created,
                LastWriteUtc = modified
            };
        }

        private static string HashPrefix(string path, int bytes, CancellationToken token)
        {
            byte[] buffer = new byte[bytes];
            int total = 0;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
            {
                while (total < buffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                }
            }

            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(buffer, 0, total));
            }
        }

        private static string HashFile(string path, CancellationToken token)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buffer = new byte[1 << 20];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }
                sha.TransformFinalBlock(buffer, 0, 0);
                return ToHex(sha.Hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static void AddError(ScanResult result, string path, Exception ex)
        {
            result.ErrorCount++;
            if (result.Errors.Count < ErrorCap)
            {
                result.Errors.Add(path + " :: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Report(IProgress<ScanProgress> progress, ScanProgress state, bool force)
        {
            if (progress == null) return;
            if (!force && (state.FilesSeen % 200) != 0) return;

            var snapshot = new ScanProgress
            {
                Phase = state.Phase,
                FilesSeen = state.FilesSeen,
                Candidates = state.Candidates,
                GroupsFound = state.GroupsFound,
                CurrentFolder = state.CurrentFolder
            };
            progress.Report(snapshot);
        }

        private static string TrimSeparators(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>
    /// Moves or deletes everything in a group except the keeper. Quarantine keeps a
    /// mirrored copy of the original path so files can be put back later.
    /// </summary>
    public static class Cleaner
    {
        public static CleanupOutcome Run(IList<DuplicateGroup> groups, CleanupMode mode,
            string quarantineDir, IProgress<int> progress, CancellationToken token)
        {
            var outcome = new CleanupOutcome();
            if (groups == null) return outcome;

            int total = 0;
            foreach (DuplicateGroup group in groups) total += Math.Max(0, group.Files.Count - 1);
            int index = 0;

            foreach (DuplicateGroup group in groups)
            {
                token.ThrowIfCancellationRequested();

                foreach (FileEntry file in group.Files)
                {
                    token.ThrowIfCancellationRequested();
                    if (file == group.Keeper) continue;

                    index++;
                    if (progress != null) progress.Report(index);

                    var record = new CleanupRecord { Source = file.Path };
                    try
                    {
                        if (!File.Exists(file.Path))
                        {
                            record.Success = true;
                            record.Target = string.Empty;
                        }
                        else if (mode == CleanupMode.Quarantine)
                        {
                            string target = BuildQuarantineTarget(quarantineDir, file.Path);
                            string targetDir = Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }
                            SafeMove(file.Path, target);
                            record.Target = target;
                            record.Success = true;
                        }
                        else
                        {
                            File.Delete(file.Path);
                            record.Success = true;
                        }

                        if (record.Success) outcome.BytesFreed += file.Size;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        record.Success = false;
                        record.Error = ex.Message;
                    }

                    outcome.Records.Add(record);
                    if (record.Success) outcome.Succeeded++; else outcome.Failed++;
                }
            }

            return outcome;
        }

        public static string BuildQuarantineTarget(string quarantineDir, string sourcePath)
        {
            string relative;
            if (sourcePath.Length >= 2 && sourcePath[1] == ':')
            {
                // C:\Users\me\a.jpg  ->  C\Users\me\a.jpg
                relative = sourcePath.Substring(0, 1) + sourcePath.Substring(2);
            }
            else if (sourcePath.StartsWith("\\\\", StringComparison.Ordinal))
            {
                relative = "UNC" + sourcePath.Substring(1);
            }
            else
            {
                relative = sourcePath;
            }

            relative = relative.TrimStart('\\', '/');
            string target = Path.Combine(quarantineDir, relative);

            if (File.Exists(target))
            {
                string dir = Path.GetDirectoryName(target);
                string baseName = Path.GetFileNameWithoutExtension(target);
                string extension = Path.GetExtension(target);
                int n = 1;
                string candidate;
                do
                {
                    candidate = Path.Combine(dir, baseName + "_dup" + n.ToString(CultureInfo.InvariantCulture) + extension);
                    n++;
                }
                while (File.Exists(candidate));
                target = candidate;
            }

            return target;
        }

        private static void SafeMove(string source, string target)
        {
            try
            {
                File.Move(source, target);
                return;
            }
            catch (IOException)
            {
                // Usually a different volume: copy, verify, then remove the original.
            }

            File.Copy(source, target, false);
            var copied = new FileInfo(target);
            var original = new FileInfo(source);
            if (!copied.Exists || copied.Length != original.Length)
            {
                throw new IOException("Copy verification failed for " + target);
            }
            File.Delete(source);
        }
    }

    public static class CsvExport
    {
        public static void Write(ScanResult result, string path)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Path,SizeBytes,SizeMB,SHA256,KeptCopy,CopiesInGroup");
                foreach (DuplicateGroup group in result.Groups)
                {
                    foreach (FileEntry file in group.Files)
                    {
                        if (file == group.Keeper) continue;
                        writer.WriteLine(string.Join(",",
                            Quote(file.Path),
                            file.Size.ToString(CultureInfo.InvariantCulture),
                            (file.Size / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture),
                            Quote(group.Hash),
                            Quote(group.Keeper == null ? string.Empty : group.Keeper.Path),
                            group.Files.Count.ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
