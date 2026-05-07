using System.Text;

namespace CbsContractsDesktopClient.Services
{
    public static class DiagnosticsFileLogger
    {
        private static readonly object SyncRoot = new();

        public static string LogFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CbsContractsDesktopClient",
            "diagnostics.log");

        public static string FnsLogFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CbsContractsDesktopClient",
            "logs",
            "fns.log");

        public static void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Write($"{line}{Environment.NewLine}");
        }

        public static void Clear()
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                    File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static void AppendBlock(string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Write(
                LogFilePath,
                $"{Environment.NewLine}" +
                $"===== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {title} ====={Environment.NewLine}" +
                $"{text}{Environment.NewLine}");
        }

        public static void AppendFnsBlock(string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Write(
                FnsLogFilePath,
                $"{Environment.NewLine}" +
                $"===== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {title} ====={Environment.NewLine}" +
                $"{text}{Environment.NewLine}");
        }

        private static void Write(string text)
        {
            Write(LogFilePath, text);
        }

        private static void Write(string logFilePath, string text)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
                    File.AppendAllText(logFilePath, text, Encoding.UTF8);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
