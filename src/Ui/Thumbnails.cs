using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BinDay.App
{
    /// <summary>
    /// Thumbnails for photos, videos, installers and app packages.
    ///
    /// On Windows the shell does the hard work (including video frames), so BinDay
    /// gets the same previews Explorer shows. Everything is loaded off the UI thread
    /// and cached with a small LRU. Anywhere the shell is unavailable (Wine, broken
    /// file associations) the caller simply draws its own glyph instead.
    /// </summary>
    internal static class Thumbnails
    {
        private const int CacheLimit = 220;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Order = new LinkedList<string>();
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static BlockingCollection<Request> _queue;
        private static readonly object StartGate = new object();

        private sealed class Request
        {
            public string Key;
            public string Path;
            public int Size;
            public Action Ready;
        }

        /// <summary>
        /// Returns the cached thumbnail, or null and starts loading it. When it is
        /// ready, <paramref name="ready"/> is called from a worker thread.
        /// </summary>
        public static Image Get(string path, int size, Action ready)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string key = path + "|" + size;

            lock (Gate)
            {
                Image cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    Touch(key);
                    return cached;
                }

                if (Pending.Contains(key)) return null;
                Pending.Add(key);
            }

            EnsureWorkers();
            _queue.Add(new Request { Key = key, Path = path, Size = size, Ready = ready });
            return null;
        }

        public static void Clear()
        {
            lock (Gate)
            {
                foreach (KeyValuePair<string, Image> pair in Cache)
                {
                    try { pair.Value.Dispose(); } catch (Exception) { }
                }
                Cache.Clear();
                Order.Clear();
                Pending.Clear();
            }
        }

        private static void EnsureWorkers()
        {
            if (_queue != null) return;

            lock (StartGate)
            {
                if (_queue != null) return;

                BlockingCollection<Request> queue = new BlockingCollection<Request>();
                for (int i = 0; i < 2; i++)
                {
                    Thread thread = new Thread(Work);
                    thread.IsBackground = true;
                    thread.Name = "BinDay thumbnail " + i;
                    thread.Start(queue);
                }
                _queue = queue;
            }
        }

        private static void Work(object state)
        {
            BlockingCollection<Request> queue = (BlockingCollection<Request>)state;

            foreach (Request request in queue.GetConsumingEnumerable())
            {
                Image image = null;
                try
                {
                    image = Load(request.Path, request.Size);
                }
                catch (Exception) { }

                lock (Gate)
                {
                    Pending.Remove(request.Key);
                    if (image != null && !Cache.ContainsKey(request.Key))
                    {
                        Cache[request.Key] = image;
                        Touch(request.Key);
                        Trim();
                    }
                    else if (image != null)
                    {
                        image.Dispose();
                    }
                }

                if (image != null && request.Ready != null)
                {
                    try { request.Ready(); } catch (Exception) { }
                }
            }
        }

        private static void Touch(string key)
        {
            Order.Remove(key);
            Order.AddFirst(key);
        }

        private static void Trim()
        {
            while (Order.Count > CacheLimit)
            {
                string oldest = Order.Last.Value;
                Order.RemoveLast();

                Image image;
                if (Cache.TryGetValue(oldest, out image))
                {
                    Cache.Remove(oldest);
                    try { image.Dispose(); } catch (Exception) { }
                }
            }
        }

        // ------------------------------------------------------------- loading

        private static Image Load(string path, int size)
        {
            if (!File.Exists(path)) return null;

            Image shell = LoadShell(path, size);
            if (shell != null) return shell;

            return LoadImageFile(path, size);
        }

        private static Image LoadImageFile(string path, int size)
        {
            string extension = Path.GetExtension(path);
            if (!IsImage(extension)) return null;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image original = Image.FromStream(stream, false, false))
                {
                    int width = original.Width;
                    int height = original.Height;
                    if (width <= 0 || height <= 0) return null;

                    if (width < 16 || height < 16) return null;

                    Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(result))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);

                        float scale = Math.Max((float)size / width, (float)size / height);
                        int drawWidth = (int)Math.Ceiling(width * scale);
                        int drawHeight = (int)Math.Ceiling(height * scale);
                        int x = (size - drawWidth) / 2;
                        int y = (size - drawHeight) / 2;

                        g.DrawImage(original, new Rectangle(x, y, drawWidth, drawHeight));
                    }
                    return result;
                }
            }
            catch (Exception) { return null; }
        }

        private static bool IsImage(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".tif":
                case ".tiff":
                case ".webp":
                    return true;
                default:
                    return false;
            }
        }

        // --------------------------------------------------------- shell bridge

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int cx;
            public int cy;

            public NativeSize(int width, int height)
            {
                cx = width;
                cy = height;
            }
        }

        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
            CropToSquare = 0x20,
            WideThumbnails = 0x40,
            IconBackground = 0x80,
            ScaleUp = 0x100
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(NativeSize size, SIIGBF flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string path, IntPtr bindContext, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static Image LoadShell(string path, int size)
        {
            IShellItemImageFactory factory = null;
            IntPtr bitmap = IntPtr.Zero;

            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                if (factory == null) return null;

                int hr = factory.GetImage(new NativeSize(size, size),
                    SIIGBF.BiggerSizeOk | SIIGBF.ResizeToFit | SIIGBF.CropToSquare, out bitmap);

                if (hr != 0 || bitmap == IntPtr.Zero) return null;

                using (Bitmap wrapped = Image.FromHbitmap(bitmap))
                {
                    if (wrapped.Width < 16 || wrapped.Height < 16) return null;
                    return new Bitmap(wrapped);
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (bitmap != IntPtr.Zero)
                {
                    try { DeleteObject(bitmap); } catch (Exception) { }
                }

                if (factory != null)
                {
                    try { Marshal.ReleaseComObject(factory); } catch (Exception) { }
                }
            }
        }
    }
}
