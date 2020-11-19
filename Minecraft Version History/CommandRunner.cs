using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public static class CommandRunner
    {
        public static CommandResult RunCommand(string cd, string input, bool output = false, bool suppress_errors = false)
        {
#if DEBUG
            Console.WriteLine($"Running this command: {input}");
#endif
            var cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = cd;
            cmd.StartInfo.Arguments = $"/S /C \"{input}\"";
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardError = suppress_errors;
            cmd.StartInfo.RedirectStandardOutput = output;
            cmd.Start();
            string result = null;
            if (output)
                result = cmd.StandardOutput.ReadToEnd();
            cmd.WaitForExit();
            return new CommandResult { ExitCode = cmd.ExitCode, Output = result };
        }

        public struct CommandResult
        {
            public string Output;
            public int ExitCode;
        }
    }
}
