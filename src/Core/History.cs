using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BinDay.Core
{
    public sealed class HistoryEntry
    {
        public DateTime At;
        public string Mode = "";
        public int Removed;
        public int Failed;
        public int Refused;
        public long Bytes;
        public List<string> Paths = new List<string>();

        public string ModeLabel
        {
            get { return Mode == "Permanent" ? "Deleted" : "Recycled"; }
        }

        public string When
        {
            get { return At.ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture); }
        }
    }

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

        /// <summary>Reads past cleanups, newest first. A missing file is not an error.</summary>
        public static List<HistoryEntry> ReadAll(string filePath)
        {
            List<HistoryEntry> entries = new List<HistoryEntry>();
            if (string.IsNullOrEmpty(filePath)) filePath = DefaultPath;
            if (!File.Exists(filePath)) return entries;

            string[] lines;
            try { lines = File.ReadAllLines(filePath); }
            catch (IOException) { return entries; }
            catch (UnauthorizedAccessException) { return entries; }

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                HistoryEntry entry = ParseLine(lines[i]);
                if (entry != null) entries.Add(entry);
            }

            return entries;
        }

        public static void Clear(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) filePath = DefaultPath;
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>Reads one line of our own flat JSON back into an entry.</summary>
        public static HistoryEntry ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            if (line[0] != '{') return null;

            HistoryEntry entry = new HistoryEntry();

            string at = JsonString(line, "at");
            DateTime parsed;
            if (at != null && DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                entry.At = parsed;

            string mode = JsonString(line, "mode");
            if (mode != null) entry.Mode = mode;

            entry.Removed = JsonNumber(line, "removed");
            entry.Failed = JsonNumber(line, "failed");
            entry.Refused = JsonNumber(line, "refused");
            entry.Bytes = JsonNumber(line, "bytes");

            entry.Paths = JsonStringArray(line, "paths");
            return entry;
        }

        private static string JsonString(string line, string key)
        {
            string marker = "\"" + key + "\":";
            int index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return null;

            index += marker.Length;
            while (index < line.Length && line[index] == ' ') index++;
            if (index >= line.Length || line[index] != '"') return null;

            index++;
            StringBuilder value = new StringBuilder();
            while (index < line.Length)
            {
                char c = line[index];
                if (c == '\\' && index + 1 < line.Length)
                {
                    index++;
                    char escaped = line[index];
                    switch (escaped)
                    {
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        default: value.Append(escaped); break;
                    }
                    index++;
                    continue;
                }

                if (c == '"') return value.ToString();
                value.Append(c);
                index++;
            }

            return null;
        }

        private static int JsonNumber(string line, string key)
        {
            string marker = "\"" + key + "\":";
            int index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return 0;

            index += marker.Length;
            while (index < line.Length && line[index] == ' ') index++;

            int start = index;
            while (index < line.Length && (char.IsDigit(line[index]) || line[index] == '-')) index++;
            if (index == start) return 0;

            long value;
            if (!long.TryParse(line.Substring(start, index - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return 0;
            return (int)value;
        }

        private static List<string> JsonStringArray(string line, string key)
        {
            List<string> values = new List<string>();
            string marker = "\"" + key + "\":";
            int index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return values;

            index += marker.Length;
            while (index < line.Length && line[index] == ' ') index++;
            if (index >= line.Length || line[index] != '[') return values;

            index++;
            while (index < line.Length)
            {
                while (index < line.Length && (line[index] == ' ' || line[index] == ',')) index++;
                if (index >= line.Length || line[index] == ']') break;
                if (line[index] != '"') break;

                int start = index + 1;
                index++;
                while (index < line.Length)
                {
                    if (line[index] == '\\' && index + 1 < line.Length) { index += 2; continue; }
                    if (line[index] == '"') break;
                    index++;
                }

                values.Add(line.Substring(start, index - start).Replace("\\\"", "\"").Replace("\\\\", "\\"));
                index++;
            }

            return values;
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
