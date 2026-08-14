using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace cdisc_dataset.Utils;

public static class DefinePreviewDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "cdisc_dataset", "define-preview.log");

    public static string LogPath => LogFilePath;

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath)!;
            Directory.CreateDirectory(directory);
            var entry = $"{DateTimeOffset.Now:O} [{level}] [Thread:{Environment.CurrentManagedThreadId}] {message}";
            if (exception != null)
                entry += $"{Environment.NewLine}{exception}";

            lock (SyncRoot)
                File.AppendAllText(LogFilePath, entry + Environment.NewLine);

            Debug.WriteLine(entry);
        }
        catch
        {
            // Diagnostics must not change preview behavior.
        }
    }
}
