using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinDay.App
{
    /// <summary>
    /// Checks GitHub for a newer release. This is the only thing in BinDay that
    /// touches the network, and it only happens when the user asks for it.
    /// </summary>
    internal static class Updater
    {
        public const string ReleaseApi = "https://api.github.com/repos/khansarim007/binday/releases/latest";
        public const string DownloadUrl = "https://github.com/khansarim007/binday/releases/latest/download/BinDay.exe";
        public const string ReleasePage = "https://github.com/khansarim007/binday/releases/latest";

        /// <summary>The version of the running app, e.g. "1.1.0".</summary>
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    if (version == null) return "0.0.0";
                    return version.Major + "." + version.Minor + "." + version.Build;
                }
                catch (Exception)
                {
                    return "0.0.0";
                }
            }
        }

        public sealed class Result
        {
            /// <summary>True when the check itself worked.</summary>
            public bool Ok;

            /// <summary>The newest published version, e.g. "1.2.0".</summary>
            public string Latest;

            /// <summary>Where the newest exe can be fetched.</summary>
            public string Url;

            /// <summary>Why the check failed, when it did.</summary>
            public string Error;

            public bool NewerThan(string current)
            {
                if (!Ok || string.IsNullOrEmpty(Latest)) return false;

                Version mine;
                Version theirs;
                if (!Version.TryParse(Result.Normalise(current), out mine)) return false;
                if (!Version.TryParse(Result.Normalise(Latest), out theirs)) return false;

                return theirs > mine;
            }

            public static string Normalise(string version)
            {
                if (string.IsNullOrEmpty(version)) return "0.0.0";

                string trimmed = version.Trim();
                if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(1);
                if (trimmed.StartsWith("V", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(1);

                return trimmed;
            }
        }

        public static async Task<Result> CheckAsync(CancellationToken token)
        {
            Result result = new Result();

            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2

                string json;
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "BinDay");
                    client.Encoding = System.Text.Encoding.UTF8;
                    json = await client.DownloadStringTaskAsync(ReleaseApi);
                }

                result.Latest = Result.Normalise(JsonValue(json, "tag_name"));
                result.Ok = !string.IsNullOrEmpty(result.Latest);

                if (!result.Ok) result.Error = "The release information could not be read.";
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Downloads the newest exe into the user's Downloads folder. If that file is
        /// the one currently running it cannot be overwritten, so a versioned name is
        /// used instead.
        /// </summary>
        public static async Task<string> DownloadAsync(string version, CancellationToken token)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

                string downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                downloads = System.IO.Path.Combine(downloads ?? string.Empty, "Downloads");

                if (!System.IO.Directory.Exists(downloads)) System.IO.Directory.CreateDirectory(downloads);

                string target = System.IO.Path.Combine(downloads, "BinDay.exe");
                string running = Application.ExecutablePath;

                if (string.Equals(target, running, StringComparison.OrdinalIgnoreCase) || IsLocked(target))
                {
                    target = System.IO.Path.Combine(downloads, "BinDay-" + version + ".exe");
                }

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "BinDay");
                    await client.DownloadFileTaskAsync(DownloadUrl, target);
                }

                return target;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLocked(string path)
        {
            if (!System.IO.File.Exists(path)) return false;

            try
            {
                using (System.IO.FileStream stream = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Pulls one string value out of the GitHub release JSON.</summary>
        internal static string JsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return null;

            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;

            int close = json.IndexOf('"', open + 1);
            if (close < 0) return null;

            return json.Substring(open + 1, close - open - 1);
        }
    }
}
