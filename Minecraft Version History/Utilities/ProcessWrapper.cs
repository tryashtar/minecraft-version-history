using System.Threading;
using System.Threading.Tasks;

namespace MinecraftVersionHistory;

public record ProcessResult(int ExitCode, string Output, string Error);
public class ProcessWrapper
{
    public ProcessResult Result => Pending.Result;
    private readonly Task<ProcessResult> Pending;
    private readonly TextWriter Output;
    private readonly TextWriter Error;
    public ProcessWrapper(string directory, string filename, string arguments, TextWriter output, TextWriter error)
    {
        Output = output;
        Error = error;
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = filename,
                WorkingDirectory = directory,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        Pending = StartProcess(process);
    }
    public ProcessWrapper(string directory, string filename, string arguments) : this(directory, filename, arguments, Console.Out, Console.Out)
    { }

    private async Task<ProcessResult> StartProcess(Process process)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (sender, e) =>
        {
            output.AppendLine(e.Data);
            if (Output != null)
                Output.WriteLine(e.Data);
        };
        process.OutputDataReceived += (sender, e) =>
        {
            error.AppendLine(e.Data);
            if (Error != null)
                Error.WriteLine(e.Data);
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }
}
