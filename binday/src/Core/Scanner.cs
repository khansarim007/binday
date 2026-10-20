using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace BinDay.Core
{
    public static class DiskScanner
    {
        /// <summary>
        /// Walks the requested roots looking for disposable build output, then checks the
        /// known cache locations, old installers and duplicate files.
        /// </summary>
        public static ScanResult Scan(ScanOptions options, IProgress<ScanProgress> progress, CancellationToken token)
        {
            if (options == null) throw new ArgumentNullException("options");

            ScanResult result = new ScanResult();
            Stopwatch watch = Stopwatch.StartNew();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Reporter reporter = new Reporter(progress);

            try
            {
                if (options.ScanDevArtifacts)
                {
                    reporter.Phase = "Looking for build junk";
                    foreach (string root in options.Roots)
                    {
                        token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(root)) continue;
                        if (!Directory.Exists(root)) continue;
                        ScanRoot(root, options, result, seen, reporter, token);
                    }
                }

                if (options.ScanCaches)
                {
                    reporter.Phase = "Checking known caches";
                    foreach (CacheLocation cache in options.CacheLocations)
                    {
                        token.ThrowIfCancellationRequested();
                        if (cache == null || string.IsNullOrWhiteSpace(cache.Path)) continue;
                        if (!Directory.Exists(cache.Path)) continue;

                        string key = Normalise(cache.Path);
                        if (!seen.Add(key)) continue;

                        long bytes;
                        int files;
                        if (!Measure(cache.Path, out bytes, out files, token)) continue;

                        result.Findings.Add(new Finding
                        {
                            Id = "cache|" + key,
                            Path = cache.Path,
                            Title = cache.Title,
                            Why = cache.Why,
                            Category = cache.Category,
                            Level = cache.Level,
                            Bytes = bytes,
                            FileCount = files,
                            MarkerPath = null,
                            DeletePaths = new List<string> { cache.Path }
                        });
                        reporter.AddFinding(bytes);
                    }
                }

                if (options.ScanInstallers)
                {
                    reporter.Phase = "Looking for old installers";
                    ScanInstallers(options, result, seen, reporter, token);
                }

                if (options.ScanDuplicates)
                {
                    reporter.Phase = "Looking for duplicate files";
                    DuplicateScanner.Find(options.Roots, options, result, reporter, token);
                }
            }
            catch (OperationCanceledException)
            {
                watch.Stop();
                result.Elapsed = watch.Elapsed;
                throw;
            }

            watch.Stop();
            result.Elapsed = watch.Elapsed;

            result.Findings.Sort(delegate (Finding a, Finding b) { return b.Bytes.CompareTo(a.Bytes); });
            return result;
        }

        private static void ScanRoot(string root, ScanOptions options, ScanResult result, HashSet<string> seen, Reporter reporter, CancellationToken token)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                string dir = pending.Pop();
                string name = SafeName(dir);

                if (IsExcluded(name, options)) continue;

                bool reparse;
                if (IsReparsePoint(dir, out reparse))
                {
                    if (reparse) continue;
                }

                DirRule rule = RuleSet.Match(name);
                if (rule != null)
                {
                    string parent = SafeParent(dir);
                    if (RuleSet.HasMarker(parent, rule.Markers))
                    {
                        string key = Normalise(dir);
                        if (seen.Add(key))
                        {
                            long bytes;
                            int files;
                            if (Measure(dir, out bytes, out files, token))
                            {
                                result.Findings.Add(new Finding
                                {
                                    Id = rule.Id + "|" + key,
                                    Path = dir,
                                    Title = rule.Title,
                                    Why = rule.Why,
                                    Category = rule.Category,
                                    Level = rule.Level,
                                    Bytes = bytes,
                                    FileCount = files,
                                    MarkerPath = MarkerIn(parent, rule.Markers),
                                    DeletePaths = new List<string> { dir }
                                });
                                reporter.AddFinding(bytes);
                            }
                        }
                        // Never walk inside an artifact we already know about.
                        continue;
                    }
                }

                result.FoldersScanned++;
                reporter.Touch(dir);

                string[] children;
                try
                {
                    children = Directory.GetDirectories(dir);
                }
                catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }
                catch (IOException) { result.ErrorCount++; continue; }

                for (int i = 0; i < children.Length; i++)
                {
                    pending.Push(children[i]);
                }
            }
        }

        private static void ScanInstallers(ScanOptions options, ScanResult result, HashSet<string> seen, Reporter reporter, CancellationToken token)
        {
            DateTime cutoff = DateTime.Now.AddDays(-options.InstallerMinAgeDays);
            string[] extensions = { ".msi", ".iso", ".dmg", ".pkg", ".exe" };

            foreach (string folder in options.InstallerFolders)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;

                string[] files;
                try { files = Directory.GetFiles(folder); }
                catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }
                catch (IOException) { result.ErrorCount++; continue; }

                for (int i = 0; i < files.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string file = files[i];

                    string ext = Path.GetExtension(file);
                    bool wanted = false;
                    for (int e = 0; e < extensions.Length; e++)
                    {
                        if (string.Equals(ext, extensions[e], StringComparison.OrdinalIgnoreCase)) { wanted = true; break; }
                    }
                    if (!wanted) continue;

                    FileInfo info;
                    try { info = new FileInfo(file); }
                    catch (IOException) { result.ErrorCount++; continue; }

                    if (info.Length < options.InstallerMinBytes) continue;
                    if (info.LastWriteTime > cutoff) continue;

                    string key = Normalise(file);
                    if (!seen.Add(key)) continue;

                    int days = (int)(DateTime.Now - info.LastWriteTime).TotalDays;
                    result.Findings.Add(new Finding
                    {
                        Id = "installer|" + key,
                        Path = file,
                        Title = Path.GetFileName(file),
                        Why = "Installer downloaded " + days + " days ago and is not needed to run the program it installed.",
                        Category = Category.Installer,
                        Level = Safety.Review,
                        Bytes = info.Length,
                        FileCount = 1,
                        DeletePaths = new List<string> { file }
                    });
                    reporter.AddFinding(info.Length);
                }
            }
        }

        /// <summary>Total size and file count of a tree. Links are never followed.</summary>
        public static bool Measure(string path, out long bytes, out int files, CancellationToken token)
        {
            bytes = 0;
            files = 0;

            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path) && !File.Exists(path)) return false;

            if (File.Exists(path))
            {
                try
                {
                    bytes = new FileInfo(path).Length;
                    files = 1;
                    return true;
                }
                catch (IOException) { return false; }
            }

            Stack<string> pending = new Stack<string>();
            pending.Push(path);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                string dir = pending.Pop();

                bool reparse;
                if (IsReparsePoint(dir, out reparse) && reparse) continue;

                try
                {
                    string[] children = Directory.GetFiles(dir);
                    for (int i = 0; i < children.Length; i++)
                    {
                        try
                        {
                            bytes += new FileInfo(children[i]).Length;
                            files++;
                        }
                        catch (IOException) { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }

                try
                {
                    string[] dirs = Directory.GetDirectories(dir);
                    for (int i = 0; i < dirs.Length; i++) pending.Push(dirs[i]);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            return true;
        }

        public static bool IsExcluded(string name, ScanOptions options)
        {
            if (string.IsNullOrEmpty(name)) return true;
            for (int i = 0; i < RuleSet.ExcludedNames.Length; i++)
            {
                if (string.Equals(RuleSet.ExcludedNames[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (options != null)
            {
                for (int i = 0; i < options.ExtraExcludedNames.Count; i++)
                {
                    if (string.Equals(options.ExtraExcludedNames[i], name, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        public static bool IsReparsePoint(string path, out bool isReparse)
        {
            isReparse = false;
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                isReparse = (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static string MarkerIn(string parent, string[] markers)
        {
            if (markers == null || markers.Length == 0 || string.IsNullOrEmpty(parent)) return null;

            for (int i = 0; i < markers.Length; i++)
            {
                string marker = markers[i];
                if (marker.IndexOf('*') >= 0)
                {
                    string suffix = marker.Substring(marker.IndexOf('*') + 1);
                    try
                    {
                        foreach (string file in Directory.EnumerateFiles(parent, "*" + suffix))
                            return file;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    continue;
                }

                string candidate = Path.Combine(parent, marker);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        internal static string SafeName(string path)
        {
            try { return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
            catch (ArgumentException) { return string.Empty; }
        }

        internal static string SafeParent(string path)
        {
            try
            {
                string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                DirectoryInfo info = Directory.GetParent(trimmed);
                return info == null ? null : info.FullName;
            }
            catch (Exception) { return null; }
        }

        internal static string Normalise(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception) { return path; }
        }

        /// <summary>Throttled progress reporter shared by every phase of a scan.</summary>
        internal sealed class Reporter
        {
            private readonly IProgress<ScanProgress> _sink;
            private readonly Stopwatch _watch = Stopwatch.StartNew();
            private long _folders;
            private long _findings;
            private long _bytes;

            public string Phase = string.Empty;
            public string Current = string.Empty;

            public Reporter(IProgress<ScanProgress> sink)
            {
                _sink = sink;
            }

            public void Touch(string path)
            {
                _folders++;
                Current = path;
                Publish(true);
            }

            public void AddFinding(long bytes)
            {
                _findings++;
                _bytes += bytes;
                Publish(true);
            }

            public void Publish(bool force)
            {
                if (_sink == null) return;
                if (!force && _watch.ElapsedMilliseconds < 120) return;

                ScanProgress progress = new ScanProgress
                {
                    Phase = Phase,
                    Current = Current,
                    Folders = _folders,
                    Files = 0,
                    Findings = _findings,
                    Bytes = _bytes
                };
                _sink.Report(progress);
            }
        }
    }

    /// <summary>
    /// Finds byte-identical files: group by size, then by a cheap prefix hash, then by
    /// the full SHA-256. Only groups that agree on all three are real duplicates.
    /// </summary>
    public static class DuplicateScanner
    {
        internal static void Find(List<string> roots, ScanOptions options, ScanResult result, DiskScanner.Reporter reporter, CancellationToken token)
        {
            if (roots == null) return;

            Dictionary<long, List<string>> bySize = new Dictionary<long, List<string>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in roots)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                Stack<string> pending = new Stack<string>();
                pending.Push(root);

                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string dir = pending.Pop();

                    string name = DiskScanner.SafeName(dir);
                    if (DiskScanner.IsExcluded(name, options)) continue;

                    // Do not look inside build output we already offer to remove.
                    DirRule rule = RuleSet.Match(name);
                    if (rule != null && RuleSet.HasMarker(DiskScanner.SafeParent(dir), rule.Markers)) continue;

                    bool reparse;
                    if (DiskScanner.IsReparsePoint(dir, out reparse) && reparse) continue;

                    string[] files;
                    try { files = Directory.GetFiles(dir); }
                    catch (IOException) { result.ErrorCount++; continue; }
                    catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }

                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            FileInfo info = new FileInfo(files[i]);
                            if (info.Length < options.DuplicateMinBytes) continue;
                            if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;

                            List<string> bucket;
                            if (!bySize.TryGetValue(info.Length, out bucket))
                            {
                                bucket = new List<string>();
                                bySize.Add(info.Length, bucket);
                            }
                            bucket.Add(files[i]);
                            result.FilesScanned++;
                        }
                        catch (IOException) { }
                    }

                    string[] dirs;
                    try { dirs = Directory.GetDirectories(dir); }
                    catch (IOException) { result.ErrorCount++; continue; }
                    catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }

                    for (int i = 0; i < dirs.Length; i++) pending.Push(dirs[i]);
                }
            }

            foreach (KeyValuePair<long, List<string>> pair in bySize)
            {
                token.ThrowIfCancellationRequested();
                if (pair.Value.Count < 2) continue;

                List<List<string>> exact = FullHashes(pair.Value, options.PartialHashBytes, token);
                for (int g = 0; g < exact.Count; g++)
                {
                    List<string> group = exact[g];
                    if (group.Count < 2) continue;

                    string keeper = PickKeeper(group);
                    List<string> extras = new List<string>();
                    long bytes = 0;
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (string.Equals(group[i], keeper, StringComparison.OrdinalIgnoreCase)) continue;
                        extras.Add(group[i]);
                        try { bytes += new FileInfo(group[i]).Length; }
                        catch (IOException) { }
                    }
                    if (extras.Count == 0) continue;

                    string title = group.Count == 2
                        ? "Duplicate of " + Path.GetFileName(keeper)
                        : group.Count + " copies of " + Path.GetFileName(keeper);

                    result.Findings.Add(new Finding
                    {
                        Id = "dup|" + keeper + "|" + group.Count,
                        Path = keeper,
                        Title = title,
                        Why = "Identical content, confirmed by SHA-256. The copy with the shortest path is kept; the rest are removed.",
                        Category = Category.Duplicate,
                        Level = Safety.Safe,
                        Bytes = bytes,
                        FileCount = extras.Count,
                        IsDuplicateGroup = true,
                        KeeperPath = keeper,
                        DeletePaths = extras
                    });
                    reporter.AddFinding(bytes);
                }
            }
        }

        private static List<List<string>> FullHashes(List<string> files, int prefixBytes, CancellationToken token)
        {
            Dictionary<string, List<string>> byPrefix = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < files.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string hash = HashPrefix(files[i], prefixBytes);
                if (hash == null) continue;

                List<string> bucket;
                if (!byPrefix.TryGetValue(hash, out bucket))
                {
                    bucket = new List<string>();
                    byPrefix.Add(hash, bucket);
                }
                bucket.Add(files[i]);
            }

            List<List<string>> result = new List<List<string>>();
            foreach (KeyValuePair<string, List<string>> pair in byPrefix)
            {
                token.ThrowIfCancellationRequested();
                if (pair.Value.Count < 2) continue;

                Dictionary<string, List<string>> byFull = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    string hash = HashFull(pair.Value[i]);
                    if (hash == null) continue;

                    List<string> bucket;
                    if (!byFull.TryGetValue(hash, out bucket))
                    {
                        bucket = new List<string>();
                        byFull.Add(hash, bucket);
                    }
                    bucket.Add(pair.Value[i]);
                }

                foreach (KeyValuePair<string, List<string>> full in byFull)
                {
                    if (full.Value.Count >= 2) result.Add(full.Value);
                }
            }

            return result;
        }

        private static string HashPrefix(string path, int bytes)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] buffer = new byte[bytes];
                    int read = 0;
                    while (read < bytes)
                    {
                        int n = stream.Read(buffer, read, bytes - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read == 0) return null;
                    byte[] hash = sha.ComputeHash(buffer, 0, read);
                    return ToHex(hash);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static string HashFull(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    return ToHex(hash);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static string ToHex(byte[] bytes)
        {
            char[] hex = new char[bytes.Length * 2];
            const string digits = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                hex[i * 2] = digits[bytes[i] >> 4];
                hex[i * 2 + 1] = digits[bytes[i] & 0x0F];
            }
            return new string(hex);
        }

        /// <summary>Keeps the copy that sits in the least awkward place.</summary>
        private static string PickKeeper(List<string> group)
        {
            string best = group[0];
            int bestDepth = Depth(best);
            int bestLength = best.Length;

            for (int i = 1; i < group.Count; i++)
            {
                string candidate = group[i];
                int depth = Depth(candidate);
                int length = candidate.Length;

                if (depth < bestDepth || (depth == bestDepth && length < bestLength))
                {
                    best = candidate;
                    bestDepth = depth;
                    bestLength = length;
                }
            }

            return best;
        }

        private static int Depth(string path)
        {
            return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        }
    }
}
