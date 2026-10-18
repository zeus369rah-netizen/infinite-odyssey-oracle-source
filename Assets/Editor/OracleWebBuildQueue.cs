#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-owned Editor queue: one finite job, detached before BuildPlayer.
    // Reports live under this workspace's work directory. No runtime component,
    // asynchronous Editor API access, transport polling or automatic retry.
    public static class OracleWebBuildQueue
    {
        static string reportPath;
        static double readyAt;
        public static bool Pending => reportPath != null;

        public static string Queue(string absoluteReportPath)
        {
            if (Pending || BuildPipeline.isBuildingPlayer || EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("A stable stopped Editor and no existing build job are required.");
            string work = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../work")) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(absoluteReportPath);
            if (!path.StartsWith(work, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.GetDirectoryName(path)))
                throw new ArgumentException("Use an existing workspace work folder.");
            reportPath = path;
            readyAt = EditorApplication.timeSinceStartup + 1d;
            File.WriteAllText(path, "QUEUED " + DateTime.UtcNow.ToString("o"));
            EditorApplication.update += Tick;
            return "Queued one web build.";
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= Tick;
            string path = reportPath;
            reportPath = null;
            try
            {
                if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                    throw new InvalidOperationException("Editor state changed before the queued build.");
                File.WriteAllText(path, "BUILDING " + DateTime.UtcNow.ToString("o"));
                OdysseyProjectSetup.BuildWeb();
                BuildReport report = BuildReport.GetLatestReport();
                var lines = new StringBuilder();
                if (report != null)
                    foreach (BuildStep step in report.steps)
                        foreach (BuildStepMessage message in step.messages)
                            if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                                lines.AppendLine(message.type + ": " + message.content);
                File.WriteAllText(Path.ChangeExtension(path, ".messages.txt"), lines.ToString());
                File.WriteAllText(path, "FINISHED " + DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex) { File.WriteAllText(path, "FAILED " + DateTime.UtcNow.ToString("o") + Environment.NewLine + ex); }
        }
    }
}
#endif
