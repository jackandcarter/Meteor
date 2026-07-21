using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AetherXIV.Core.Common;
using MySql.Data.MySqlClient;
using NLog;

namespace AetherXIV.Core.Map
{
    class Program
    {
        private const int EXIT_OK = 0;
        private const int EXIT_CONFIG = 10;
        private const int EXIT_DATABASE = 20;
        private const int EXIT_STARTUP = 30;
        private const int EXIT_RUNTIME = 40;
        private const int EXIT_UNHANDLED = 50;

        public static Logger Log;
        public static Server Server;
        public static Random Random;
        public static DateTime LastTick = DateTime.Now;
        public static DateTime Tick = DateTime.Now;

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
            bool noConsole = HasFlag(args, "no-console");
            DevDiagnostics.Configure("Map", args);

            Log.Info("==================================");
            Log.Info("AetherXIV Core v2.0: Map Server");
            Log.Info("Version: 2.0 (build 21985)");
            Log.Info("==================================");

            try
            {
                ConfigConstants.Load();
                ConfigConstants.ApplyLaunchArgs(FilterLaunchArgs(args));
            }
            catch (Exception e)
            {
                return ExitOrPrompt(smoke, SmokeFail("Map", "config", e.Message, EXIT_CONFIG));
            }

            try
            {
                TestDatabaseConnection();
            }
            catch (MySqlException e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Map", "database", e.Message, EXIT_DATABASE));
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Map", "unhandled", e.Message, EXIT_UNHANDLED));
            }

            if (!File.Exists(AetherXIV.Core.Map.Server.STATIC_ACTORS_PATH))
                return ExitOrPrompt(smoke, SmokeFail("Map", "runtime prerequisite", AetherXIV.Core.Map.Server.STATIC_ACTORS_PATH + " is missing", EXIT_RUNTIME));

            try
            {
                Random = new Random();
                Server = new Server();
                Tick = DateTime.Now;
                Server.StartServer();

                if (smoke)
                    return SmokeOk("Map", GetEndpoint());

                while (true)
                {
                    String input = Console.ReadLine();
                    if (input == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (IsShutdownCommand(input))
                    {
                        Log.Info("Graceful shutdown command received.");
                        Server.StopServer();
                        Log.Info("Map Server stopped cleanly.");
                        LogManager.Shutdown();
                        return EXIT_OK;
                    }

                    if (noConsole)
                    {
                        Log.Warn("Ignoring console command while supervised; only shutdown is accepted.");
                        continue;
                    }

                    Log.Info("[Console Input] " + input);
                    Server.GetCommandProcessor().DoCommand(input, null);
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("Map", "startup", e.Message, EXIT_STARTUP));
            }
        }

        private static void TestDatabaseConnection()
        {
            Log.Info("Testing DB connection... ");
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
                if (!IsRuntimeFlag(arg) && !DevDiagnostics.IsFlag(arg))
                    filtered.Add(arg);
            }

            return filtered.ToArray();
        }

        private static bool IsRuntimeFlag(string arg)
        {
            string name = arg.Trim().TrimStart('-');
            return name.Equals("smoke", StringComparison.OrdinalIgnoreCase)
                || name.Equals("no-console", StringComparison.OrdinalIgnoreCase);
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
