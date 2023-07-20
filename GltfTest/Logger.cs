using Microsoft.Build.Framework;
using WolvenKit.Core.Interfaces;

namespace GltfTest;

public class Logger : ILoggerService
{
    public void Info(string s)
    {
        Console.WriteLine(s);
    }

    public void Warning(string s)
    {
        Console.WriteLine(s);
    }

    public void Error(string msg)
    {
        Console.WriteLine(msg);
    }

    public void Success(string msg)
    {
        Console.WriteLine(msg);
    }

    public void Debug(string msg)
    {
        Console.WriteLine(msg);
    }

    public void Error(Exception exception)
    {
    }

    public LoggerVerbosity LoggerVerbosity { get; }
    public void SetLoggerVerbosity(LoggerVerbosity verbosity)
    {
    }
}