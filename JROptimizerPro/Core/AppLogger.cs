using System.Text;

namespace JROptimizerPro.Core;

internal static class AppLogger
{
    private static readonly object Sync = new();

    public static string CurrentLogFile => Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warning(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERRO", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            AppPaths.EnsureCreated();
            var builder = new StringBuilder();
            builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
                .Append('[').Append(level).Append("] ").Append(message);

            if (exception is not null)
                builder.AppendLine().Append(exception);

            lock (Sync)
                File.AppendAllText(CurrentLogFile, builder + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // O sistema de log nunca deve derrubar o aplicativo.
        }
    }
}
