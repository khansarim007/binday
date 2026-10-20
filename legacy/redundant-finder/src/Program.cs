using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace RedundantFinder
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Report(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Report(e.ExceptionObject as Exception); };

            Application.Run(new MainForm());
        }

        private static void Report(Exception ex)
        {
            if (ex == null) return;

            string log = Path.Combine(Path.GetTempPath(), "RedundantFinder-crash.log");
            try
            {
                File.AppendAllText(log, DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine);
            }
            catch { }

            MessageBox.Show(
                "Something went wrong:\r\n\r\n" + ex.Message +
                "\r\n\r\nDetails were written to:\r\n" + log,
                "Redundant File Finder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
