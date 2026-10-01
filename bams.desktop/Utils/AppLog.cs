using System.Diagnostics;
using System.IO;
using System.Text;

namespace bams.desktop.Utils;

/// <summary>Writes application diagnostics to a local file that can be collected without a debugger.</summary>
public static class AppLog
{
    private static readonly object Sync = new();

    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BAMS",
        "Logs",
        "bams-desktop.log");

    public static void WriteInformation(string message) => Write("INFO", message, null);

    public static void WriteError(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var entry = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("O"))
            .Append(" [").Append(level).Append("] ")
            .AppendLine(message);
        if (exception is not null)
        {
            entry.AppendLine(exception.ToString());
        }

        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(LogFilePath, entry.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch (Exception loggingException)
        {
            // Diagnostics must never be the reason the application fails.
            Debug.WriteLine($"Could not write application log '{LogFilePath}': {loggingException}");
            Debug.WriteLine(entry.ToString());
        }
    }
}
