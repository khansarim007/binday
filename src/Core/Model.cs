using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BinDay.Core
{
    public enum Category
    {
        Dev = 0,
        Cache = 1,
        Temp = 2,
        Installer = 3,
        Duplicate = 4
    }

    /// <summary>How much thought the user needs to give before removing something.</summary>
    public enum Safety
    {
        Safe = 0,      // regenerated automatically, permanent delete allowed
        Caution = 1,   // regenerable but inconvenient; Recycle Bin only
        Review = 2     // could be something you want to keep; Recycle Bin only
    }

    public enum DeleteMode
    {
        Recycle = 0,
        Permanent = 1
    }

    public sealed class Finding
    {
        public string Id;
        public string Path;
        public string Title;
        public string Why;
        public Category Category;
        public Safety Level;
        public long Bytes;
        public int FileCount;

        /// <summary>The project file (package.json, *.csproj...) that justified this finding.</summary>
        public string MarkerPath;

        public bool IsDuplicateGroup;
        public string KeeperPath;

        /// <summary>Exactly what gets removed. One directory for artifacts, N-1 files for duplicates.</summary>
        public List<string> DeletePaths = new List<string>();

        public string CategoryLabel
        {
            get
            {
                switch (Category)
                {
                    case Category.Dev: return "Dev junk";
                    case Category.Cache: return "Cache";
                    case Category.Temp: return "Temp files";
                    case Category.Installer: return "Installer";
                    default: return "Duplicate";
                }
            }
        }

        public string SafetyLabel
        {
            get
            {
                switch (Level)
                {
                    case Safety.Safe: return "Safe";
                    case Safety.Caution: return "Check";
                    default: return "Review";
                }
            }
        }
    }

    public sealed class CacheLocation
    {
        public string Path;
        public string Title;
        public string Why;
        public Safety Level;
        public Category Category = Category.Cache;
    }

    public sealed class ScanOptions
    {
        public List<string> Roots = new List<string>();
        public List<CacheLocation> CacheLocations = new List<CacheLocation>();
        public List<string> InstallerFolders = new List<string>();

        public bool ScanDevArtifacts = true;
        public bool ScanCaches = true;
        public bool ScanInstallers = true;
        public bool ScanDuplicates = true;

        public long DuplicateMinBytes = 1024L * 1024L;
        public long InstallerMinBytes = 50L * 1024L * 1024L;
        public int InstallerMinAgeDays = 30;
        public int PartialHashBytes = 65536;

        public List<string> ExtraExcludedNames = new List<string>();
    }

    public sealed class ScanProgress
    {
        public string Phase = string.Empty;
        public string Current = string.Empty;
        public long Folders;
        public long Files;
        public long Findings;
        public long Bytes;
    }

    public sealed class ScanResult
    {
        public List<Finding> Findings = new List<Finding>();
        public long FoldersScanned;
        public long FilesScanned;
        public long ErrorCount;
        public TimeSpan Elapsed;

        public long TotalBytes
        {
            get
            {
                long total = 0;
                foreach (Finding f in Findings) total += f.Bytes;
                return total;
            }
        }
    }

    public sealed class CleanupItem
    {
        public string Path;
        public long Bytes;
        public bool Success;
        public bool Skipped;
        public string Message;
    }

    public sealed class CleanupReport
    {
        public List<CleanupItem> Items = new List<CleanupItem>();
        public int Removed;
        public int Failed;
        public int Refused;
        public long BytesFreed;
    }

    public static class Format
    {
        public static string Bytes(long bytes)
        {
            double value = bytes;
            if (value >= 1099511627776d) return (value / 1099511627776d).ToString("0.00", CultureInfo.CurrentCulture) + " TB";
            if (value >= 1073741824d) return (value / 1073741824d).ToString("0.00", CultureInfo.CurrentCulture) + " GB";
            if (value >= 1048576d) return (value / 1048576d).ToString("0.0", CultureInfo.CurrentCulture) + " MB";
            if (value >= 1024d) return (value / 1024d).ToString("0.0", CultureInfo.CurrentCulture) + " KB";
            return bytes.ToString("N0", CultureInfo.CurrentCulture) + " B";
        }

        public static string Count(long value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// Last line of defence. Anything that looks like an operating system folder, a drive
    /// root or the user profile root itself is refused before a delete is attempted.
    /// </summary>
    public static class PathGuard
    {
        private static readonly string[] ProtectedRootNames =
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData",
            "System Volume Information", "$Recycle.Bin", "Recovery", "PerfLogs", "WinSxS"
        };

        public static bool IsProtected(string path, out string reason)
        {
            reason = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                reason = "Empty path";
                return true;
            }

            string full;
            try { full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception ex) { reason = "Unreadable path: " + ex.Message; return true; }

            if (full.Length == 0)
            {
                reason = "Empty path";
                return true;
            }

            // Drive root (C:) or UNC share root
            if (full.Length <= 2 && full.Length >= 1 && full[full.Length - 1] == ':')
            {
                reason = "Refusing to delete a drive root";
                return true;
            }
            string root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) &&
                string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Refusing to delete a drive root";
                return true;
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile) &&
                string.Equals(profile.TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Refusing to delete the user profile itself";
                return true;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                string localRoot = localAppData.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(localRoot, full, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Refusing to delete the AppData folder itself";
                    return true;
                }
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows))
            {
                string winRoot = windows.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(winRoot, full, StringComparison.OrdinalIgnoreCase) ||
                    IsInside(full, winRoot))
                {
                    reason = "Inside the Windows folder";
                    return true;
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                string pf = programFiles.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(pf, full, StringComparison.OrdinalIgnoreCase) || IsInside(full, pf))
                {
                    reason = "Inside Program Files";
                    return true;
                }
            }

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                string pf86 = programFilesX86.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(pf86, full, StringComparison.OrdinalIgnoreCase) || IsInside(full, pf86))
                {
                    reason = "Inside Program Files (x86)";
                    return true;
                }
            }

            foreach (string name in ProtectedRootNames)
            {
                string candidate = Path.Combine(Path.GetPathRoot(full) ?? string.Empty, name);
                if (string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Protected system folder: " + name;
                    return true;
                }
            }

            return false;
        }

        public static bool IsProtected(string path)
        {
            string reason;
            return IsProtected(path, out reason);
        }

        public static bool IsInside(string child, string parent)
        {
            if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent)) return false;

            string c = child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string p = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (c.Length <= p.Length) return false;
            if (!c.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return false;

            return c[p.Length] == Path.DirectorySeparatorChar || c[p.Length] == Path.AltDirectorySeparatorChar;
        }

        public static string LongPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 240) return path;
            if (path.StartsWith(@"\\?\")) return path;
            if (path.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + path.Substring(2);
            }
            return @"\\?\" + path;
        }
    }
}
