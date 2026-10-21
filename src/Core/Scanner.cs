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
        /// known cache locations, old installers, duplicate files and oversized media.
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
                            Kind = cache.Kind,
                            Recovery = cache.Recovery,
                            Shape = CategoryInfo.Shape(cache.Category),
                            Modified = SafeFolderTime(cache.Path),
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

                HashSet<string> duplicateFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (options.ScanDuplicates || options.ScanMedia || options.ScanLargeFiles)
                {
                    reporter.Phase = "Reading the files in these folders";
                    FileInventory inventory = FileInventory.Build(options, result, reporter, token);

                    if (options.ScanDuplicates)
                    {
                        reporter.Phase = "Comparing files for duplicates";
                        DuplicateScanner.Analyse(inventory, options, result, reporter, token, duplicateFiles);
                    }

                    if (options.ScanMedia || options.ScanLargeFiles)
                    {
                        reporter.Phase = "Looking for big media and leftover packages";
                        MediaScanner.Analyse(inventory, options, result, reporter, token, duplicateFiles);
                    }
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
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                string dir = pending.Pop();
                if (!visited.Add(dir)) continue;

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
                                    Kind = rule.Kind,
                                    Recovery = rule.Recovery,
                                    Shape = CategoryInfo.Shape(rule.Category),
                                    Modified = SafeFolderTime(dir),
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

        // ------------------------------------------------------------- installers

        private sealed class InstallerCandidate
        {
            public string Path;
            public long Bytes;
            public DateTime Modified;
            public string Key;
        }

        private static readonly string[] InstallerExtensions = { ".msi", ".iso", ".dmg", ".pkg", ".exe", ".msix", ".appx" };

        private static void ScanInstallers(ScanOptions options, ScanResult result, HashSet<string> seen, Reporter reporter, CancellationToken token)
        {
            DateTime cutoff = DateTime.Now.AddDays(-options.InstallerMinAgeDays);
            List<InstallerCandidate> candidates = new List<InstallerCandidate>();

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

                    if (!IsInstallerFile(file)) continue;

                    FileInfo info;
                    try { info = new FileInfo(file); }
                    catch (IOException) { result.ErrorCount++; continue; }

                    if (info.Length < options.InstallerMinBytes) continue;
                    if (info.LastWriteTime > cutoff) continue;

                    string key = Normalise(file);
                    if (!seen.Add(key)) continue;

                    candidates.Add(new InstallerCandidate
                    {
                        Path = file,
                        Bytes = info.Length,
                        Modified = info.LastWriteTime,
                        Key = InstallerKey(file) + "|" + Path.GetExtension(file).ToLowerInvariant()
                    });
                }
            }

            Dictionary<string, List<InstallerCandidate>> groups = new Dictionary<string, List<InstallerCandidate>>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                List<InstallerCandidate> group;
                if (!groups.TryGetValue(candidates[i].Key, out group))
                {
                    group = new List<InstallerCandidate>();
                    groups.Add(candidates[i].Key, group);
                }
                group.Add(candidates[i]);
            }

            foreach (KeyValuePair<string, List<InstallerCandidate>> pair in groups)
            {
                List<InstallerCandidate> group = pair.Value;
                group.Sort(delegate (InstallerCandidate a, InstallerCandidate b) { return b.Modified.CompareTo(a.Modified); });

                for (int i = 0; i < group.Count; i++)
                {
                    InstallerCandidate candidate = group[i];
                    bool redundant = group.Count > 1 && i > 0;
                    int days = Math.Max(0, (int)(DateTime.Now - candidate.Modified).TotalDays);
                    string extension = Path.GetExtension(candidate.Path).TrimStart('.').ToUpperInvariant();

                    Finding finding = new Finding
                    {
                        Id = (redundant ? "redundant|" : "installer|") + Normalise(candidate.Path),
                        Path = candidate.Path,
                        Title = Path.GetFileName(candidate.Path),
                        Category = redundant ? Category.Redundant : Category.Installer,
                        Level = Safety.Review,
                        Bytes = candidate.Bytes,
                        FileCount = 1,
                        Kind = redundant ? "older copy of an installer (" + extension + ")" : "installer (" + extension + ")",
                        Recovery = "Download the installer again if you ever need to reinstall the program.",
                        Shape = redundant ? ItemKind.Redundant : ItemKind.Installer,
                        Modified = candidate.Modified,
                        DeletePaths = new List<string> { candidate.Path }
                    };

                    finding.Why = redundant
                        ? string.Format("You already have a newer copy of this installer ({0}). This copy was downloaded {1} days ago.",
                            Path.GetFileName(group[0].Path), days)
                        : string.Format("Installer downloaded {0} days ago and not needed to run the program it installed.", days);

                    result.Findings.Add(finding);
                    reporter.AddFinding(candidate.Bytes);
                }
            }
        }

        /// <summary>
        /// Strips versions and noise words so "VLC-3.0.20-win64.exe" and
        /// "vlc-3.0.18.exe" land in the same group. Falls back to the raw name when
        /// everything would be stripped, so two unrelated "setup.exe" files never pair up.
        /// </summary>
        internal static string InstallerKey(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            char[] separators = { ' ', '-', '_', '.', '(', ')', '[', ']', '{', '}', ',', '+', '~' };
            string[] parts = stem.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            List<string> keep = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (IsNoiseWord(part)) continue;
                if (IsVersionToken(part)) continue;
                keep.Add(part);
            }

            if (keep.Count == 0) return stem;
            return string.Join(string.Empty, keep.ToArray());
        }

        private static bool IsNoiseWord(string word)
        {
            switch (word)
            {
                case "setup":
                case "install":
                case "installer":
                case "portable":
                case "win":
                case "win32":
                case "win64":
                case "windows":
                case "x64":
                case "x86":
                case "amd64":
                case "arm64":
                case "latest":
                case "final":
                case "release":
                case "full":
                case "offline":
                case "online":
                case "web":
                case "exe":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsVersionToken(string word)
        {
            if (word.Length == 0) return false;

            if (word[0] == 'v' || word[0] == 'V')
            {
                if (word.Length == 1) return false;
                bool restIsVersion = true;
                for (int i = 1; i < word.Length; i++)
                {
                    if (!char.IsDigit(word[i]) && word[i] != '.') { restIsVersion = false; break; }
                }
                if (restIsVersion) return true;
            }

            bool anyDigit = false;
            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];
                if (char.IsDigit(c)) { anyDigit = true; continue; }
                if (c != '.') return false;
            }
            return anyDigit;
        }

        internal static bool IsInstallerFile(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < InstallerExtensions.Length; i++)
            {
                if (string.Equals(extension, InstallerExtensions[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // ----------------------------------------------------------------- helpers

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

        private static DateTime SafeFolderTime(string path)
        {
            try { return Directory.GetLastWriteTime(path); }
            catch (Exception) { return DateTime.MinValue; }
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

        internal static string ExtensionLabel(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return "file";
            return extension.TrimStart('.').ToUpperInvariant();
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

    // ---------------------------------------------------------------- inventory

    /// <summary>One file that is interesting to at least one collector.</summary>
    internal sealed class FileEntry
    {
        public string Path;
        public long Bytes;
        public DateTime Modified;
        public Category Category;
    }

    /// <summary>
    /// A single pass over the folders. Duplicate detection and the media scan both
    /// work from this, so a full scan only touches the disk once per root.
    /// </summary>
    internal sealed class FileInventory
    {
        public readonly Dictionary<long, List<string>> BySize = new Dictionary<long, List<string>>();
        public readonly List<FileEntry> Media = new List<FileEntry>();
        public readonly List<FileEntry> Large = new List<FileEntry>();
        public long FilesScanned;

        private static readonly HashSet<string> VideoExtensions = Extensions(
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".m2ts");

        private static readonly HashSet<string> ImageExtensions = Extensions(
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".raw", ".cr2", ".nef", ".arw", ".psd", ".ai", ".xcf");

        private static readonly HashSet<string> AudioExtensions = Extensions(
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".aiff", ".alac", ".opus");

        private static readonly HashSet<string> PackageExtensions = Extensions(
            ".apk", ".xapk", ".apks", ".aab", ".ipa");

        private static HashSet<string> Extensions(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        internal static FileInventory Build(ScanOptions options, ScanResult result, DiskScanner.Reporter reporter, CancellationToken token)
        {
            FileInventory inventory = new FileInventory();
            if (options == null || options.Roots == null) return inventory;

            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in options.Roots)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                Stack<string> pending = new Stack<string>();
                pending.Push(root);

                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string dir = pending.Pop();
                    if (!visited.Add(dir)) continue;

                    string name = DiskScanner.SafeName(dir);
                    if (DiskScanner.IsExcluded(name, options)) continue;

                    // Do not look inside build output we already offer to remove.
                    DirRule rule = RuleSet.Match(name);
                    if (rule != null && RuleSet.HasMarker(DiskScanner.SafeParent(dir), rule.Markers)) continue;

                    bool reparse;
                    if (DiskScanner.IsReparsePoint(dir, out reparse) && reparse) continue;

                    reporter.Current = dir;

                    string[] files;
                    try { files = Directory.GetFiles(dir); }
                    catch (IOException) { result.ErrorCount++; continue; }
                    catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }

                    for (int i = 0; i < files.Length; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        FileInfo info;
                        try { info = new FileInfo(files[i]); }
                        catch (IOException) { continue; }
                        catch (UnauthorizedAccessException) { continue; }

                        if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;
                        if (info.Length <= 0) continue;

                        inventory.FilesScanned++;

                        long size = info.Length;
                        string extension = info.Extension;

                        bool video = VideoExtensions.Contains(extension) && size >= options.VideoMinBytes;
                        bool image = ImageExtensions.Contains(extension) && size >= options.ImageMinBytes;
                        bool audio = AudioExtensions.Contains(extension) && size >= options.AudioMinBytes;
                        bool package = PackageExtensions.Contains(extension) && size >= options.AppPackageMinBytes;

                        if (options.ScanDuplicates && size >= options.DuplicateMinBytes)
                        {
                            List<string> bucket;
                            if (!inventory.BySize.TryGetValue(size, out bucket))
                            {
                                bucket = new List<string>();
                                inventory.BySize.Add(size, bucket);
                            }
                            bucket.Add(info.FullName);
                        }

                        if (options.ScanMedia)
                        {
                            if (video) inventory.Media.Add(Entry(info, Category.Video));
                            else if (image) inventory.Media.Add(Entry(info, Category.Image));
                            else if (audio) inventory.Media.Add(Entry(info, Category.Audio));
                            else if (package) inventory.Media.Add(Entry(info, Category.AppPackage));
                        }

                        bool listedAsMedia = video || image || audio || package;

                        if (options.ScanLargeFiles && !listedAsMedia &&
                            size >= options.LargeFileMinBytes && !DiskScanner.IsInstallerFile(info.FullName))
                        {
                            inventory.Large.Add(Entry(info, Category.LargeFile));
                        }
                    }

                    result.FilesScanned = inventory.FilesScanned;

                    string[] dirs;
                    try { dirs = Directory.GetDirectories(dir); }
                    catch (IOException) { result.ErrorCount++; continue; }
                    catch (UnauthorizedAccessException) { result.ErrorCount++; continue; }

                    for (int i = 0; i < dirs.Length; i++) pending.Push(dirs[i]);
                }
            }

            return inventory;
        }

        private static FileEntry Entry(FileInfo info, Category category)
        {
            return new FileEntry
            {
                Path = info.FullName,
                Bytes = info.Length,
                Modified = info.LastWriteTime,
                Category = category
            };
        }
    }

    // --------------------------------------------------------------- duplicates

    /// <summary>
    /// Finds byte-identical files: group by size, then by a cheap prefix hash, then by
    /// the full SHA-256. Only groups that agree on all three are real duplicates.
    /// </summary>
    public static class DuplicateScanner
    {
        internal static void Analyse(FileInventory inventory, ScanOptions options, ScanResult result,
            DiskScanner.Reporter reporter, CancellationToken token, HashSet<string> duplicateFiles)
        {
            if (inventory == null) return;

            foreach (KeyValuePair<long, List<string>> pair in inventory.BySize)
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
                        if (duplicateFiles != null) duplicateFiles.Add(group[i]);
                        if (string.Equals(group[i], keeper, StringComparison.OrdinalIgnoreCase)) continue;
                        extras.Add(group[i]);
                        try { bytes += new FileInfo(group[i]).Length; }
                        catch (IOException) { }
                    }
                    if (extras.Count == 0) continue;

                    string title = group.Count == 2
                        ? "Duplicate of " + Path.GetFileName(keeper)
                        : group.Count + " copies of " + Path.GetFileName(keeper);

                    DateTime when = DateTime.MinValue;
                    try { when = new FileInfo(keeper).LastWriteTime; }
                    catch (IOException) { }

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
                        Kind = "duplicate group (" + extras.Count + " extra cop" + (extras.Count == 1 ? "y" : "ies") + ")",
                        Recovery = "The original at " + keeper + " is left untouched.",
                        Shape = ItemKind.Duplicate,
                        Modified = when,
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

    // ------------------------------------------------------------------- media

    /// <summary>
    /// Oversized videos, photos, audio, APKs and any other very large file. These are
    /// never called junk: every one of them is Review-only and mentioned separately.
    /// </summary>
    public static class MediaScanner
    {
        internal static void Analyse(FileInventory inventory, ScanOptions options, ScanResult result,
            DiskScanner.Reporter reporter, CancellationToken token, HashSet<string> alreadyListed)
        {
            if (inventory == null) return;

            for (int i = 0; i < inventory.Media.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                FileEntry entry = inventory.Media[i];
                if (alreadyListed != null && alreadyListed.Contains(entry.Path)) continue;

                string extension = DiskScanner.ExtensionLabel(entry.Path);
                string kind;
                string why;

                switch (entry.Category)
                {
                    case Category.Video:
                        kind = "video file (" + extension + ")";
                        why = "A large video. BinDay does not know whether you have watched it, so this is listed for you to decide.";
                        break;
                    case Category.Image:
                        kind = "image file (" + extension + ")";
                        why = "A large image or export. It may be a photo, a screenshot or a project export you still need.";
                        break;
                    case Category.Audio:
                        kind = "audio file (" + extension + ")";
                        why = "A large audio file, such as music, a podcast or a recording.";
                        break;
                    default:
                        kind = "app package (" + extension + ")";
                        why = "An Android or app package. Once the app is installed, the file on the PC is only a leftover copy.";
                        break;
                }

                Add(result, reporter, entry, kind, why);
            }

            for (int i = 0; i < inventory.Large.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                FileEntry entry = inventory.Large[i];
                if (alreadyListed != null && alreadyListed.Contains(entry.Path)) continue;

                Add(result, reporter, entry, "large file (" + DiskScanner.ExtensionLabel(entry.Path) + ")",
                    "One of the biggest single files in the folders you chose. BinDay cannot tell what it is for, so it only lists it.");
            }
        }

        private static void Add(ScanResult result, DiskScanner.Reporter reporter, FileEntry entry, string kind, string why)
        {
            result.Findings.Add(new Finding
            {
                Id = "media|" + DiskScanner.Normalise(entry.Path),
                Path = entry.Path,
                Title = Path.GetFileName(entry.Path),
                Why = why,
                Category = entry.Category,
                Level = Safety.Review,
                Bytes = entry.Bytes,
                FileCount = 1,
                Kind = kind,
                Recovery = "Restore it from the Recycle Bin if you change your mind.",
                Shape = CategoryInfo.Shape(entry.Category),
                Modified = entry.Modified,
                DeletePaths = new List<string> { entry.Path }
            });
            reporter.AddFinding(entry.Bytes);
        }
    }
}
