using System;
using System.IO;

namespace AetherXIV.Core.Common
{
    public static class StartupReadySignal
    {
        private const string ReadyFileEnvironmentVariable = "AETHERXIV_READY_FILE";

        public static void TryWrite(string serviceName, string endpoint)
        {
            string readyFile = Environment.GetEnvironmentVariable(ReadyFileEnvironmentVariable);
            if (String.IsNullOrWhiteSpace(readyFile))
                return;

            try
            {
                string directory = Path.GetDirectoryName(readyFile);
                if (!String.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(
                    readyFile,
                    String.Format(
                        "service={0}{1}endpoint={2}{1}utc={3:o}{1}",
                        serviceName,
                        Environment.NewLine,
                        endpoint,
                        DateTime.UtcNow));

                DevDiagnostics.Trace(
                    "server.ready",
                    "service", serviceName,
                    "endpoint", endpoint,
                    "readyFile", readyFile);
            }
            catch (Exception e)
            {
                DevDiagnostics.Trace(
                    "server.ready.failed",
                    "service", serviceName,
                    "endpoint", endpoint,
                    "readyFile", readyFile,
                    "error", e.Message);
            }
        }
    }
}
