using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using AetherXIV.Core.Common;
using MySql.Data.MySqlClient;
using NLog;

namespace AetherXIV.Core.Lobby
{
    class Program
    {
        private const int EXIT_OK = 0;
        private const int EXIT_CONFIG = 10;
        private const int EXIT_DATABASE = 20;
        private const int EXIT_STARTUP = 30;
        private const int EXIT_UNHANDLED = 50;

        public static Logger Log;

        static int Main(string[] args)
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            // set up logging
            Log = LogManager.GetCurrentClassLogger();
#if DEBUG
            TextWriterTraceListener myWriter = new TextWriterTraceListener(System.Console.Out);
            Trace.Listeners.Add(myWriter);
#endif
            bool smoke = HasFlag(args, "smoke");
            DevDiagnostics.Configure("Lobby", args);

            Log.Info("==================================");
            Log.Info("AetherXIV Core v2.0: Lobby Server");
            Log.Info("Version: 2.0 (build 21991)");
            Log.Info("==================================");

            try
            {
                ConfigConstants.Load();
                ConfigConstants.ApplyLaunchArgs(FilterLaunchArgs(args));
            }
            catch (Exception e)
            {
                return ExitOrPrompt(smoke, SmokeFail("Lobby", "config", e.Message, EXIT_CONFIG));
            }

            try
            {
                TestDatabaseConnection();
            }
            catch (MySqlException e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Lobby", "database", e.Message, EXIT_DATABASE));
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Lobby", "unhandled", e.Message, EXIT_UNHANDLED));
            }

            try
            {
                Server server = new Server();
                server.StartServer();

                if (smoke)
                    return SmokeOk("Lobby", GetEndpoint());

                while (true)
                {
                    String input = Console.ReadLine();
                    if (input == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (!IsShutdownCommand(input))
                    {
                        Log.Warn("Ignoring console command while supervised; only shutdown is accepted.");
                        continue;
                    }

                    Log.Info("Graceful shutdown command received.");
                    server.StopServer();
                    Log.Info("Lobby Server stopped cleanly.");
                    LogManager.Shutdown();
                    return EXIT_OK;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Lobby", "startup", e.Message, EXIT_STARTUP));
            }
        }

        private static void TestDatabaseConnection()
        {
            Log.Info("Testing DB connection to \"{0}\"... ", ConfigConstants.DATABASE_HOST);
            using (MySqlConnection conn = new MySqlConnection(String.Format("Server={0}; Port={1}; Database={2}; UID={3}; Password={4}", ConfigConstants.DATABASE_HOST, ConfigConstants.DATABASE_PORT, ConfigConstants.DATABASE_NAME, ConfigConstants.DATABASE_USERNAME, ConfigConstants.DATABASE_PASSWORD)))
            {
                conn.Open();
                conn.Close();
                Log.Info("Connection ok.");
            }
        }

        private static bool HasFlag(string[] args, string flagName)
        {
            foreach (string arg in args)
            {
                if (arg.Trim().TrimStart('-').Equals(flagName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsShutdownCommand(string input)
        {
            return input != null
                && input.Trim().Equals("shutdown", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] FilterLaunchArgs(string[] args)
        {
            List<string> filtered = new List<string>();
            foreach (string arg in args)
            {
                if (!arg.Trim().TrimStart('-').Equals("smoke", StringComparison.OrdinalIgnoreCase) && !DevDiagnostics.IsFlag(arg))
                    filtered.Add(arg);
            }

            return filtered.ToArray();
        }

        private static string GetEndpoint()
        {
            return String.Format("{0}:{1}", ConfigConstants.OPTIONS_BINDIP, ConfigConstants.OPTIONS_PORT);
        }

        private static int SmokeOk(string serverName, string endpoint)
        {
            Console.WriteLine("SMOKE_OK {0} {1}", serverName, endpoint);
            return EXIT_OK;
        }

        private static int SmokeFail(string serverName, string category, string message, int exitCode)
        {
            Console.WriteLine("SMOKE_FAIL {0} {1}: {2}", serverName, category, Sanitize(message));
            return exitCode;
        }

        private static string Sanitize(string message)
        {
            if (String.IsNullOrEmpty(message))
                return "unknown";

            return message.Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ");
        }

        private static int ExitOrPrompt(bool smoke, int exitCode)
        {
            if (smoke || Console.IsInputRedirected)
                return exitCode;

            Log.Info("Press any key to continue...");
            Console.ReadKey();
            return exitCode;
        }
    }
}
