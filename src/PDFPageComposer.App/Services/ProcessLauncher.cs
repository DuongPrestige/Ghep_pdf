using System.Diagnostics;
using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.App.Services;

public sealed class ProcessLauncher : IProcessLauncher
{
    public void StartFile(string filePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    public void StartExecutable(string executablePath, string argument)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            ArgumentList = { argument },
            UseShellExecute = false
        });
    }

    public void StartProcess(string fileName, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
    }
}
