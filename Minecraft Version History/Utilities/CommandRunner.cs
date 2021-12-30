namespace MinecraftVersionHistory;

public static class CommandRunner
{
    public static CommandResult RunCommand(string cd, string input, bool output = false, bool suppress_errors = false)
    {
#if DEBUG
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Running this command: {input}");
#endif
        Console.ForegroundColor = ConsoleColor.DarkGray;
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
        Console.ResetColor();
        return new CommandResult { ExitCode = cmd.ExitCode, Output = result };
    }

    public static CommandResult RunJavaCommand(string cd, IEnumerable<string> java, string input, bool output = false, bool suppress_errors = false)
    {
        return RunJavaCommands(cd, java.Select(x => (x, input)), output, suppress_errors);
    }

    public static CommandResult RunJavaCombos(string cd, IEnumerable<string> java, IEnumerable<string> inputs, bool output = false, bool suppress_errors = false)
    {
        var combinations = from x in java from y in inputs select (x, y);
        return RunJavaCommands(cd, combinations, output, suppress_errors);
    }

    public static CommandResult RunJavaCommands(string cd, IEnumerable<(string java, string input)> commands, bool output = false, bool suppress_errors = false)
    {
        CommandResult result = default;
#if DEBUG
        int i = 0;
#endif
        foreach (var (java, input) in commands)
        {
#if DEBUG
            i++;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Attempt #{i} of {commands.Count()}");
            Console.WriteLine($"Install: {java}");
            Console.WriteLine($"Input: {input}");
#endif
            result = RunCommand(cd, $"\"{java}\" {input}", output, suppress_errors);
            if (result.ExitCode == 0)
                return result;
        }
        return result;
    }

    public struct CommandResult
    {
        public string Output;
        public int ExitCode;
    }
}
