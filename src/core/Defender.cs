// ShieldGate 2.0 - lop quet thu hai: goi Windows Defender (MpCmdRun.exe) khi
// nguoi dung yeu cau. Khong chay nen, khong gui du lieu ra mang.
using System;
using System.Collections.Generic;
using System.IO;

namespace ShieldGate
{
    internal sealed class DefenderResult
    {
        public bool Ran;
        public bool Clean;
        public int ExitCode = -1;
        public string Path = "";
        public string Output = "";
        public string Error = "";
    }

    internal static class Defender
    {
        // Defender dat MpCmdRun trong Platform\<version>\ tren Windows 10/11;
        // may cu dat thang trong thu muc Windows Defender.
        public static string FindMpCmdRun()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string legacy = Path.Combine(programFiles, "Windows Defender", "MpCmdRun.exe");
                if (File.Exists(legacy)) return legacy;

                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string platform = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");
                if (Directory.Exists(platform))
                {
                    string[] dirs = Directory.GetDirectories(platform);
                    Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                    for (int i = dirs.Length - 1; i >= 0; i--)
                    {
                        string exe = Path.Combine(dirs[i], "MpCmdRun.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
            }
            catch { }
            return "";
        }

        public static bool Available { get { return FindMpCmdRun().Length > 0; } }

        // -ScanType 3: quet mot tep/thu muc cu the. Defender tu xu ly theo chinh sach
        // cua may; ShieldGate chi doc ket qua va ghi timeline.
        public static DefenderResult Scan(string path, int timeoutMs)
        {
            DefenderResult r = new DefenderResult();
            r.Path = path ?? "";
            string exe = FindMpCmdRun();
            if (exe.Length == 0)
            {
                r.Error = S.Repair.DefenderMissing;
                return r;
            }
            if (string.IsNullOrEmpty(path))
            {
                r.Error = S.Repair.NoPath;
                return r;
            }
            try
            {
                string args = "-Scan -ScanType 3 -File \"" + path + "\"";
                string output;
                int code = Native.Run(exe, args, timeoutMs <= 0 ? 600000 : timeoutMs, out output);
                r.Ran = true;
                r.ExitCode = code;
                r.Output = output ?? "";
                r.Clean = code == 0;
                if (code != 0 && code != 2)
                    r.Error = S.Repair.DefenderExit(code);
                Events.Add(Events.KindScan, r.Clean ? "INFO" : "WARN", path,
                           r.Clean ? S.Events.DefenderClean : S.Events.DefenderThreat);
                Log.Write(S.Log.DefenderScan(path, code), r.Clean ? "INFO" : "WARN");
            }
            catch (Exception e)
            {
                r.Error = e.Message;
            }
            return r;
        }
    }
}
