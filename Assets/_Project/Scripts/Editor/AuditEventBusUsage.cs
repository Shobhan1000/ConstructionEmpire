using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ConstructionEmpire.Editor
{
    public static class AuditEventBusUsage
    {
        public const string ScriptsPath = "Assets/_Project/Scripts";
        private static readonly HashSet<string> LegacyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ProjectStarted", "ProjectCompleted", "ProjectDelayed", "BidSubmitted", "BidWon", "BidLost",
            "PaymentReceived", "EquipmentBreakdown", "MaterialDelivered", "MaterialDelayed",
            "InspectionFailed", "WorkerAbsent", "SafetyIncident", "BudgetExceeded", "ReputationChanged",
            "BalanceChanged", "TransactionRejected", "HourlyTick", "DailyTick", "WeeklyTick", "MonthlyTick"
        };

        // Mask comments/literals so examples and log messages cannot report themselves.
        // This is a source-pattern audit, not a full C# parser (aliased Action types are not resolved).
        private static readonly Regex Ignored = new Regex(
            "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
            RegexOptions.Compiled);
        private static readonly Regex Declaration = new Regex(
            @"\bevent\s+(?:(?:global::)?System\s*\.\s*)?Action\b\s*(?:<[^;{}]*?>\s*)?(?<names>@?\w+(?:\s*,\s*@?\w+)*)\s*(?=[;={])",
            RegexOptions.Compiled);

        public readonly struct Finding
        {
            public int Line { get; }
            public string EventName { get; }
            public Finding(int line, string eventName) { Line = line; EventName = eventName; }
        }

        public static List<Finding> FindLegacyDeclarations(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string masked = Ignored.Replace(source, match => Regex.Replace(match.Value, @"[^\r\n]", " "));
            var findings = new List<Finding>();
            foreach (Match match in Declaration.Matches(masked))
            {
                int line = 1;
                for (int i = 0; i < match.Index; i++) if (masked[i] == '\n') line++;
                foreach (string item in match.Groups["names"].Value.Split(','))
                {
                    string name = item.Trim().TrimStart('@');
                    string normalized = name.StartsWith("On", StringComparison.Ordinal) ? name.Substring(2) : name;
                    if (normalized.EndsWith("Event", StringComparison.Ordinal))
                        normalized = normalized.Substring(0, normalized.Length - 5);
                    if (LegacyNames.Contains(normalized)) findings.Add(new Finding(line, name));
                }
            }
            return findings;
        }

        [MenuItem("Tools/Project Setup/Audit Event Bus Usage")]
        public static void Audit()
        {
            if (!Directory.Exists(ScriptsPath))
            {
                Debug.Log($"[Event Bus Audit] FAIL: Scripts folder missing: {ScriptsPath}");
                return;
            }
            string[] files = Directory.GetFiles(ScriptsPath, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            int count = 0, unreadable = 0;
            foreach (string file in files)
            {
                try
                {
                    foreach (Finding finding in FindLegacyDeclarations(File.ReadAllText(file)))
                    {
                        count++;
                        Debug.Log($"[Event Bus Audit] LEGACY: {file.Replace('\\', '/')}:{finding.Line} " +
                            $"declares Action event '{finding.EventName}'. Use typed GameEvents.Publish/Subscribe.");
                    }
                }
                catch (IOException exception) { unreadable++; Debug.Log($"[Event Bus Audit] Cannot read {file}: {exception.Message}"); }
                catch (UnauthorizedAccessException exception) { unreadable++; Debug.Log($"[Event Bus Audit] Cannot read {file}: {exception.Message}"); }
            }
            Debug.Log($"[Event Bus Audit] {(count == 0 && unreadable == 0 ? "PASS" : "FAIL")}: " +
                $"{files.Length} scripts scanned, {count} legacy declarations, {unreadable} unreadable files. " +
                "Report only; no files changed. Checks direct Action declarations, including On/Event name variants; aliases require manual review.");
        }
    }
}
