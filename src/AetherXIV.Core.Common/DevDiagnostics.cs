using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using NLog;

namespace AetherXIV.Core.Common
{
    public static class DevDiagnostics
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly object sync = new object();
        private static string serverName = "Unknown";
        private static string tracePath;

        public static bool Enabled { get; private set; }

        public static void Configure(string server, string[] args)
        {
            serverName = String.IsNullOrEmpty(server) ? "Unknown" : server;
            Enabled =
                HasFlag(args, "dev-diagnostics") ||
                IsEnabledEnvironmentValue(Environment.GetEnvironmentVariable("AETHERXIV_DEV_DIAGNOSTICS"));

            if (!Enabled)
                return;

            string outputDir = Environment.GetEnvironmentVariable("AETHERXIV_DEV_DIAGNOSTICS_DIR");
            if (String.IsNullOrEmpty(outputDir))
                outputDir = Path.Combine(".", "dev-diagnostics");

            Directory.CreateDirectory(outputDir);
            tracePath = Path.Combine(outputDir, String.Format("{0}-{1:yyyyMMdd-HHmmss}.jsonl", serverName.ToLowerInvariant(), DateTime.UtcNow));
            Trace("diagnostics.enabled", "path", tracePath);
        }

        public static bool IsFlag(string arg)
        {
            return arg != null && arg.Trim().TrimStart('-').Equals("dev-diagnostics", StringComparison.OrdinalIgnoreCase);
        }

        public static void Trace(string category, params object[] keyValues)
        {
            if (!Enabled)
                return;

            string line = BuildJsonLine(category, keyValues);

            lock (sync)
            {
                logger.Info("[DEVTRACE] {0}", line);

                if (!String.IsNullOrEmpty(tracePath))
                    File.AppendAllText(tracePath, line + Environment.NewLine);
            }
        }

        public static void TraceSubPacketClassification(string context, SubPacket subpacket)
        {
            if (!Enabled || subpacket == null)
                return;

            string classification = PacketClassificationRegistry.Classify(context, subpacket);
            if (String.IsNullOrEmpty(classification))
                return;

            Trace(
                "packet.classification",
                "context", context,
                "classification", classification,
                "type", FormatHex(subpacket.header.type),
                "opcode", FormatHex(subpacket.gameMessage.opcode),
                "source", FormatHex(subpacket.header.sourceId),
                "target", FormatHex(subpacket.header.targetId),
                "size", subpacket.header.subpacketSize,
                "payloadLength", subpacket.data == null ? 0 : subpacket.data.Length);
        }

        private static string BuildJsonLine(string category, object[] keyValues)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonProperty(builder, "timestamp", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            builder.Append(",");
            AppendJsonProperty(builder, "server", serverName);
            builder.Append(",");
            AppendJsonProperty(builder, "category", category);

            if (keyValues != null)
            {
                for (int i = 0; i + 1 < keyValues.Length; i += 2)
                {
                    string key = Convert.ToString(keyValues[i], CultureInfo.InvariantCulture);
                    if (String.IsNullOrEmpty(key))
                        continue;

                    builder.Append(",");
                    AppendJsonProperty(builder, key, keyValues[i + 1]);
                }
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendJsonProperty(StringBuilder builder, string key, object value)
        {
            builder.Append("\"");
            builder.Append(Escape(key));
            builder.Append("\":");
            AppendJsonValue(builder, value);
        }

        private static void AppendJsonValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is bool)
            {
                builder.Append(((bool)value) ? "true" : "false");
                return;
            }

            if (value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            builder.Append("\"");
            builder.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture)));
            builder.Append("\"");
        }

        private static string Escape(string value)
        {
            if (value == null)
                return String.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string FormatHex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }

        private static bool HasFlag(string[] args, string flagName)
        {
            if (args == null)
                return false;

            foreach (string arg in args)
            {
                if (arg != null && arg.Trim().TrimStart('-').Equals(flagName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsEnabledEnvironmentValue(string value)
        {
            if (String.IsNullOrEmpty(value))
                return false;

            string normalized = value.Trim().ToLowerInvariant();
            return normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on";
        }
    }
}
