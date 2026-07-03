using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace moreStrafts
{
    public static class Log
    {
        static ManualLogSource? source;
        static StreamWriter? writer;
        static readonly object gate = new object();

        public static void Init(ManualLogSource logSource, bool perInstanceFile)
        {
            source = logSource;
            if (!perInstanceFile) return;
            try
            {
                string dir = Path.Combine(Paths.BepInExRootPath, "moreStrafts_debug");
                Directory.CreateDirectory(dir);
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                string file = Path.Combine(dir, $"moreStrafts_{DateTime.Now:yyyyMMdd_HHmmss}_pid{pid}.log");
                writer = new StreamWriter(file, false) { AutoFlush = true };
            }
            catch (Exception e)
            {
                logSource.LogWarning($"per-instance log unavailable: {e.Message}");
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);

        static void Write(string level, string message)
        {
            lock (gate)
            {
                try { writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}"); } catch { }
            }
            switch (level)
            {
                case "ERROR": source?.LogError(message); break;
                case "WARN": source?.LogWarning(message); break;
                default: source?.LogInfo(message); break;
            }
        }
    }
}
