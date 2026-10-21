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
        Duplicate = 4,
        Video = 5,
        Image = 6,
        Audio = 7,
        AppPackage = 8,
        LargeFile = 9,
        Redundant = 10
    }

    /// <summary>The shape and colour used to draw an item. Deliberately simple.</summary>
    public enum ItemKind
    {
        Folder = 0,
        Build = 1,
        Cache = 2,
        Temp = 3,
        Installer = 4,
        Duplicate = 5,
        Video = 6,
        Photo = 7,
        Audio = 8,
        Package = 9,
        File = 10,
        Redundant = 11
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

        /// <summary>Plain-English name of the thing, e.g. "npm dependency folder".</summary>
        public string Kind;

        /// <summary>What the user can do to get it back after removal.</summary>
        public string Recovery;

        /// <summary>Used for sorting by date. For folders this is the folder's own stamp.</summary>
        public DateTime Modified = DateTime.MinValue;

        /// <summary>Which glyph the UI draws for this item.</summary>
        public ItemKind Shape = ItemKind.File;

        /// <summary>The project file (package.json, *.csproj...) that justified this finding.</summary>
        public string MarkerPath;

        public bool IsDuplicateGroup;
        public string KeeperPath;

        /// <summary>Exactly what gets removed. One directory for artifacts, N-1 files for duplicates.</summary>
        public List<string> DeletePaths = new List<string>();

        public bool IsFolder
        {
            get
            {
                return Shape == ItemKind.Folder || Shape == ItemKind.Build || Shape == ItemKind.Cache ||
                       Shape == ItemKind.Temp || Shape == ItemKind.Duplicate;
            }
        }

        public string CategoryLabel
        {
            get { return CategoryInfo.Name(Category); }
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
        public string Kind;
        public string Recovery;
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
        public bool ScanMedia = true;
        public bool ScanLargeFiles = true;

        public long DuplicateMinBytes = 1024L * 1024L;
        public long InstallerMinBytes = 50L * 1024L * 1024L;
        public int InstallerMinAgeDays = 30;
        public int PartialHashBytes = 65536;

        // Media is only listed for review: it is the user's own stuff, never junk.
        public long VideoMinBytes = 25L * 1024L * 1024L;
        public long AudioMinBytes = 10L * 1024L * 1024L;
        public long ImageMinBytes = 8L * 1024L * 1024L;
        public long AppPackageMinBytes = 1024L * 1024L;
        public long LargeFileMinBytes = 300L * 1024L * 1024L;

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

    /// <summary>Names and one-line explanations for the category rail.</summary>
    public static class CategoryInfo
    {
        public static readonly Category[] InRailOrder = new[]
        {
            Category.Dev, Category.Cache, Category.Temp, Category.Installer, Category.Duplicate,
            Category.Video, Category.Image, Category.Audio, Category.AppPackage, Category.LargeFile,
            Category.Redundant
        };

        public static string Name(Category category)
        {
            switch (category)
            {
                case Category.Dev: return "Dev junk";
                case Category.Cache: return "Caches";
                case Category.Temp: return "Temp files";
                case Category.Installer: return "Old installers";
                case Category.Duplicate: return "Duplicate files";
                case Category.Video: return "Videos";
                case Category.Image: return "Photos";
                case Category.Audio: return "Music & audio";
                case Category.AppPackage: return "App packages";
                case Category.LargeFile: return "Large files";
                case Category.Redundant: return "Redundant installs";
                default: return "Other";
            }
        }

        public static string Blurb(Category category)
        {
            switch (category)
            {
                case Category.Dev: return "Dependency folders and build output you can rebuild.";
                case Category.Cache: return "Downloaded packages and app caches.";
                case Category.Temp: return "Scratch files Windows and apps leave behind.";
                case Category.Installer: return "Setup files you have probably already used.";
                case Category.Duplicate: return "Byte-identical copies of a file you still have.";
                case Category.Video: return "Big video files you may have forgotten about.";
                case Category.Image: return "Large photos, screenshots and exports.";
                case Category.Audio: return "Music, podcasts and recordings taking up space.";
                case Category.AppPackage: return "APKs and app packages left over after installing.";
                case Category.LargeFile: return "Single files over 300 MB, whatever they are.";
                case Category.Redundant: return "Older copies of an installer you already have a newer one of.";
                default: return string.Empty;
            }
        }

        public static ItemKind Shape(Category category)
        {
            switch (category)
            {
                case Category.Dev: return ItemKind.Build;
                case Category.Cache: return ItemKind.Cache;
                case Category.Temp: return ItemKind.Temp;
                case Category.Installer: return ItemKind.Installer;
                case Category.Duplicate: return ItemKind.Duplicate;
                case Category.Video: return ItemKind.Video;
                case Category.Image: return ItemKind.Photo;
                case Category.Audio: return ItemKind.Audio;
                case Category.AppPackage: return ItemKind.Package;
                case Category.LargeFile: return ItemKind.File;
                case Category.Redundant: return ItemKind.Redundant;
                default: return ItemKind.File;
            }
        }

        /// <summary>True for the categories that hold the user's own files rather than junk.</summary>
        public static bool IsPersonal(Category category)
        {
            return category == Category.Video || category == Category.Image || category == Category.Audio ||
                   category == Category.AppPackage || category == Category.LargeFile;
        }
    }

    /// <summary>
    /// Turns a finding into the sentences the app shows when you select it.
    /// Kept out of the UI so the wording can be tested and stays consistent.
    /// </summary>
    public static class Narrative
    {
        public static string Summary(Finding finding)
        {
            if (finding == null) return string.Empty;
            if (!string.IsNullOrEmpty(finding.Kind)) return finding.Kind;
            if (finding.IsDuplicateGroup) return "duplicate group";
            return finding.IsFolder ? "folder" : "file";
        }

        public static string IfYouRemove(Finding finding, DeleteMode mode)
        {
            if (finding == null) return string.Empty;

            string verb = mode == DeleteMode.Permanent ? "permanently delete" : "move";
            string destination = mode == DeleteMode.Permanent ? string.Empty : " to the Recycle Bin";

            string subject;
            if (finding.IsDuplicateGroup)
            {
                int copies = finding.DeletePaths.Count;
                subject = string.Format(CultureInfo.CurrentCulture,
                    "{0} duplicate cop{1} ({2})", copies, copies == 1 ? "y" : "ies", Format.Bytes(finding.Bytes));
            }
            else if (finding.IsFolder)
            {
                subject = string.Format(CultureInfo.CurrentCulture,
                    "the folder {0} and the {1} file{2} inside it ({3})",
                    finding.Path, Format.Count(finding.FileCount), finding.FileCount == 1 ? "" : "s", Format.Bytes(finding.Bytes));
            }
            else
            {
                subject = string.Format(CultureInfo.CurrentCulture, "{0} ({1})", finding.Path, Format.Bytes(finding.Bytes));
            }

            StringBuilder text = new StringBuilder();
            if (CategoryInfo.IsPersonal(finding.Category))
            {
                text.Append("This is one of your own files, not system junk. ");
            }

            text.Append("BinDay will ").Append(verb).Append(' ').Append(subject).Append(destination).Append('.');

            if (finding.IsDuplicateGroup && !string.IsNullOrEmpty(finding.KeeperPath))
            {
                text.Append(" The original at ").Append(finding.KeeperPath).Append(" stays where it is.");
            }

            if (mode == DeleteMode.Permanent)
            {
                if (finding.Level == Safety.Safe)
                {
                    text.Append(" This cannot be undone.");
                }
                else
                {
                    text.Append(" BinDay will refuse this one: only items marked Safe can be deleted permanently. Use the Recycle Bin instead.");
                }
            }
            else
            {
                text.Append(" Nothing outside it is touched, and you can restore it from the Recycle Bin.");
            }

            return text.ToString();
        }

        public static string GetItBack(Finding finding)
        {
            if (finding == null) return string.Empty;
            if (!string.IsNullOrEmpty(finding.Recovery)) return finding.Recovery;
            if (CategoryInfo.IsPersonal(finding.Category)) return "Restore it from the Recycle Bin if you change your mind.";
            return "Restore it from the Recycle Bin if you change your mind.";
        }

        /// <summary>One line for the inspector heading: size, contents, age.</summary>
        public static string Meta(Finding finding)
        {
            if (finding == null) return string.Empty;

            StringBuilder text = new StringBuilder();
            text.Append(Format.Bytes(finding.Bytes));

            if (finding.FileCount > 1)
            {
                text.Append(" in ").Append(Format.Count(finding.FileCount)).Append(" files");
            }

            if (finding.Modified != DateTime.MinValue)
            {
                text.Append("  |  changed ").Append(Format.Relative(finding.Modified));
            }

            return text.ToString();
        }
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

        public static string Date(DateTime when)
        {
            if (when == DateTime.MinValue) return "-";
            return when.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }

        public static string Relative(DateTime when)
        {
            if (when == DateTime.MinValue) return "unknown";

            TimeSpan age = DateTime.Now - when;
            if (age.TotalMinutes < 2) return "just now";
            if (age.TotalHours < 1) return (int)age.TotalMinutes + " min ago";
            if (age.TotalDays < 1) return (int)age.TotalHours + " h ago";
            if (age.TotalDays < 2) return "yesterday";
            if (age.TotalDays < 31) return (int)age.TotalDays + " days ago";
            if (age.TotalDays < 365) return (int)(age.TotalDays / 30.44) + " months ago";
            return (int)(age.TotalDays / 365.25) + " years ago";
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
