using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BinDay.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate (object sender, ThreadExceptionEventArgs e)
            {
                Report(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                Report(e.ExceptionObject as Exception);
            };

            Application.Run(new MainForm(args));
        }

        private static void Report(Exception error)
        {
            if (error == null) return;

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinDay");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "error.log"),
                    DateTime.Now.ToString("o") + Environment.NewLine + error.ToString() + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception) { }

            try
            {
                MessageBox.Show("Something went wrong:\n\n" + error.Message, "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception) { }
        }
    }
}
