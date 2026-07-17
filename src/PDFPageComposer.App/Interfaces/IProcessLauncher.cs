namespace PDFPageComposer.App.Interfaces;

public interface IProcessLauncher
{
    void StartFile(string filePath);

    void StartExecutable(string executablePath, string argument);

    void StartProcess(string fileName, IEnumerable<string> arguments);
}
