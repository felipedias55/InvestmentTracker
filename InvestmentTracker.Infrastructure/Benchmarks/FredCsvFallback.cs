using System.Diagnostics;
using System.ComponentModel;

namespace InvestmentTracker.Infrastructure.Benchmarks
{
    public interface IFredCsvFallback
    {
        Task<string> FetchAsync(DateOnly start, DateOnly end, CancellationToken ct);
    }
    // FRED's edge can reset managed TLS connections on Windows. The native HTTPS client
    // is a transport fallback only; all parsing, validation and calculations stay shared.
    public sealed class FredCsvFallback : IFredCsvFallback
    {
        public async Task<string> FetchAsync(DateOnly start, DateOnly end, CancellationToken ct)
        {
            if (start < new DateOnly(1999, 1, 1) || end <= start || end.DayNumber - start.DayNumber > 1840) throw new InvalidOperationException();
            var executable = OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe") : "curl";
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "--fail", "--silent", "--show-error", "--connect-timeout", "5", "--max-time", "15", "--max-filesize", "1048576", "--proto", "=https", "--user-agent", "FDInvestmentTracker/1.0", "--url", $"https://fred.stlouisfed.org/graph/fredgraph.csv?id=SP500&cosd={start:yyyy-MM-dd}&coed={end:yyyy-MM-dd}" })
                info.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = info };
            try
            {
                if (!process.Start()) throw new IOException();
                // Drain stderr concurrently, without logging remote content or command arguments.
                var errors = DrainAsync(process.StandardError, ct);
                var text = new System.Text.StringBuilder(); var buffer = new char[8192]; int count;
                while ((count = await process.StandardOutput.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    if (text.Length + count > 1048576) throw new IOException();
                    text.Append(buffer, 0, count);
                }
                await process.WaitForExitAsync(ct); await errors;
                if (process.ExitCode != 0) throw new IOException("CSV transport unavailable.");
                return text.ToString();
            }
            catch (Win32Exception) { throw new IOException("Native HTTPS transport unavailable."); }
            finally { try { if (process.Id > 0 && !process.HasExited) process.Kill(); } catch (Exception error) when (error is InvalidOperationException or Win32Exception) { } }
        }
        private static async Task DrainAsync(StreamReader reader, CancellationToken ct)
        {
            var buffer = new char[4096]; while (await reader.ReadAsync(buffer.AsMemory(), ct) > 0) { }
        }
    }
}
