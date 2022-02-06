namespace MinecraftVersionHistory;

public static class CommandRunner
{
    public static ProcessResult RunCommand(string directory, string input, TextWriter output, TextWriter error)
    {
#if DEBUG
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Running this command: {input}");
#endif
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var result = new ProcessWrapper(directory, "cmd.exe", $"/S /C \"{input}\"", output, error).Result;
        Console.ResetColor();
        return result;
    }

    public static ProcessResult RunCommand(string directory, string input)
    {
        return RunCommand(directory, input, Console.Out, Console.Out);
    }

    public static ProcessResult RunJavaCommand(string cd, IEnumerable<string> java, string input)
    {
        return RunJavaCommands(cd, java.Select(x => (x, input)));
    }

    public static ProcessResult RunJavaCombos(string cd, IEnumerable<string> java, IEnumerable<string> inputs)
    {
        var combinations = from x in java from y in inputs select (x, y);
        return RunJavaCommands(cd, combinations);
    }

    public static ProcessResult RunJavaCommands(string cd, IEnumerable<(string java, string input)> commands)
    {
        ProcessResult result = default;
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
            result = RunCommand(cd, $"\"{java}\" {input}");
            if (result.ExitCode == 0)
                return result;
        }
        return result;
    }
}
