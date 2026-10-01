using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace bams.desktop.Utils;

/// <summary>Writes application diagnostics to a local file that can be collected without a debugger.</summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly object ErrorSync = new();
    private static readonly ConditionalWeakTable<Exception, object> LoggedExceptions = new();

    /// <summary>Gets the file path for the current local calendar day's log.</summary>
    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BAMS",
        "Logs",
        $"bams-desktop-{DateTime.Now:yyyyMMdd}.log");

    public static void WriteInformation(string message) => Write("INFO", message, null);

    public static void WriteError(string message, Exception exception) => Write("ERROR", message, exception);

    /// <summary>Returns whether the application has explicitly enabled diagnostic logging.</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                return Application.Current?.Properties["Debug Log"] is true;
            }
            catch (Exception)
            {
                // Logging configuration must never cause an application failure or bypass the gate.
                return false;
            }
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (exception is not null)
        {
            lock (ErrorSync)
            {
                if (LoggedExceptions.TryGetValue(exception, out _))
                {
                    return;
                }

                LoggedExceptions.Add(exception, new object());
            }
        }

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
