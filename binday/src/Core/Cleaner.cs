using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BinDay.Core
{
    /// <summary>Anything that can take something away: the Recycle Bin, a test bin, etc.</summary>
    public interface IRecycler
    {
        /// <summary>Returns true when the path was taken. The error explains a refusal.</summary>
        bool Recycle(string path, out string error);

        /// <summary>Human readable name of where things go.</summary>
        string Destination { get; }
    }

    /// <summary>Sends files to the Windows Recycle Bin so the user can undo the cleanup.</summary>
    public sealed class WindowsRecycler : IRecycler
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        private const uint FO_DELETE = 3;
        private const ushort FOF_SILENT = 0x0004;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOERRORUI = 0x0400;
        private const ushort FOF_NOCONFIRMMKDIR = 0x0200;

        public string Destination { get { return "the Recycle Bin"; } }

        public bool Recycle(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Empty path";
                return false;
            }

            try
            {
                SHFILEOPSTRUCT operation = new SHFILEOPSTRUCT();
                operation.wFunc = FO_DELETE;
                operation.pFrom = path + "\0";
                operation.pTo = null;
                operation.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT | FOF_NOCONFIRMMKDIR;

                int code = SHFileOperation(ref operation);
                if (code == 0) return true;

                if (operation.fAnyOperationsAborted)
                {
                    error = "Cancelled by Windows";
                    return false;
                }

                error = "Windows refused the delete (code " + code + ")";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Test-friendly recycler: files are moved into a folder instead of the Recycle Bin.
    /// </summary>
    public sealed class FolderRecycler : IRecycler
    {
        private readonly string _bin;
        private int _counter;

        public FolderRecycler(string binFolder)
        {
            _bin = binFolder;
        }

        public string Destination { get { return _bin; } }

        public bool Recycle(string path, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(_bin);
                string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name)) name = "item";
                string target = Path.Combine(_bin, name + "." + (++_counter).ToString(CultureInfo.InvariantCulture));
                if (Directory.Exists(path)) Directory.Move(path, target);
                else File.Move(path, target);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public sealed class CleanupProgress
    {
        public string Current = string.Empty;
        public int Done;
        public int Total;
        public long BytesFreed;
    }

    public static class Cleaner
    {
        /// <summary>
        /// Removes the selected findings. Permanent deletion is only ever allowed for
        /// findings marked Safe; everything else goes to the Recycle Bin or is refused.
        /// </summary>
        public static CleanupReport Run(List<Finding> findings, DeleteMode mode, IRecycler recycler, IProgress<CleanupProgress> progress, CancellationToken token)
        {
            CleanupReport report = new CleanupReport();
            if (findings == null) return report;

            CleanupProgress state = new CleanupProgress { Total = findings.Count };

            for (int i = 0; i < findings.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                Finding finding = findings[i];
                state.Current = finding.Path;
                state.Done = i;
                if (progress != null) progress.Report(state);

                if (mode == DeleteMode.Permanent && finding.Level != Safety.Safe)
                {
                    report.Refused++;
                    report.Items.Add(new CleanupItem
                    {
                        Path = finding.Path,
                        Bytes = 0,
                        Success = false,
                        Skipped = true,
                        Message = "Only items marked Safe can be deleted permanently. Use the Recycle Bin instead."
                    });
                    continue;
                }

                bool anySuccess = false;
                List<string> paths = new List<string>(finding.DeletePaths);

                for (int p = 0; p < paths.Count; p++)
                {
                    token.ThrowIfCancellationRequested();
                    string path = paths[p];

                    string guardReason;
                    if (PathGuard.IsProtected(path, out guardReason))
                    {
                        report.Refused++;
                        report.Items.Add(new CleanupItem
                        {
                            Path = path,
                            Bytes = 0,
                            Success = false,
                            Skipped = true,
                            Message = "Protected: " + guardReason
                        });
                        continue;
                    }

                    bool exists = Directory.Exists(path) || File.Exists(path);
                    if (!exists)
                    {
                        report.Items.Add(new CleanupItem
                        {
                            Path = path,
                            Bytes = 0,
                            Success = false,
                            Skipped = true,
                            Message = "Already gone"
                        });
                        continue;
                    }

                    long bytes;
                    int files;
                    DiskScanner.Measure(path, out bytes, out files, token);

                    string error;
                    bool ok;
                    if (mode == DeleteMode.Recycle)
                    {
                        ok = recycler.Recycle(path, out error);
                    }
                    else
                    {
                        error = null;
                        ok = DeletePermanently(path, out error);
                    }

                    if (ok)
                    {
                        anySuccess = true;
                        report.BytesFreed += bytes;
                        report.Items.Add(new CleanupItem
                        {
                            Path = path,
                            Bytes = bytes,
                            Success = true,
                            Skipped = false,
                            Message = mode == DeleteMode.Recycle ? "Moved to " + recycler.Destination : "Deleted"
                        });
                    }
                    else
                    {
                        report.Failed++;
                        report.Items.Add(new CleanupItem
                        {
                            Path = path,
                            Bytes = 0,
                            Success = false,
                            Skipped = false,
                            Message = error ?? "Unknown error"
                        });
                    }
                }

                if (anySuccess) report.Removed++;
            }

            if (progress != null)
            {
                state.Done = findings.Count;
                progress.Report(state);
            }

            return report;
        }

        /// <summary>Deletes without the safety net. Only ever called for Safe findings.</summary>
        public static bool DeletePermanently(string path, out string error)
        {
            error = null;
            string target = PathGuard.LongPath(path);

            try
            {
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                    return true;
                }

                if (File.Exists(target))
                {
                    File.Delete(target);
                    return true;
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Read-only bits are the usual reason; clear them and try once more.
                try
                {
                    ClearReadOnly(target);
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                    else if (File.Exists(target)) File.Delete(target);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void ClearReadOnly(string path)
        {
            if (File.Exists(path))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                return;
            }

            if (!Directory.Exists(path)) return;

            DirectoryInfo info = new DirectoryInfo(path);
            if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                info.Attributes &= ~FileAttributes.ReadOnly;

            try
            {
                foreach (FileInfo file in info.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        file.Attributes &= ~FileAttributes.ReadOnly;
                }
                foreach (DirectoryInfo dir in info.GetDirectories())
                {
                    ClearReadOnly(dir.FullName);
                }
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// A plain text log of past cleanups, one JSON object per line, in %LOCALAPPDATA%\BinDay.
    /// </summary>
    public static class History
    {
        public static string DefaultPath
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(local)) local = Path.GetTempPath();
                return Path.Combine(local, "BinDay", "history.jsonl");
            }
        }

        public static void Append(CleanupReport report, DeleteMode mode, List<Finding> findings, string filePath)
        {
            if (report == null) return;
            if (string.IsNullOrEmpty(filePath)) filePath = DefaultPath;

            try
            {
                string folder = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                StringBuilder line = new StringBuilder();
                line.Append('{');
                line.Append("\"at\":\"").Append(Json(DateTime.Now.ToString("o", CultureInfo.InvariantCulture))).Append('"');
                line.Append(",\"mode\":\"").Append(mode == DeleteMode.Recycle ? "Recycle" : "Permanent").Append('"');
                line.Append(",\"removed\":").Append(report.Removed.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"failed\":").Append(report.Failed.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"refused\":").Append(report.Refused.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"bytes\":").Append(report.BytesFreed.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"paths\":[");
                for (int i = 0; i < report.Items.Count; i++)
                {
                    if (i > 0) line.Append(',');
                    line.Append('"').Append(Json(report.Items[i].Path)).Append('"');
                    if (i >= 499) break;
                }
                line.Append("]}");

                using (FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(line.ToString());
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string Json(string value)
        {
            if (value == null) return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}
