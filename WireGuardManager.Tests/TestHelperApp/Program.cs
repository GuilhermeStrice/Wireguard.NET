using System;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "sleep" && args.Length > 1 && int.TryParse(args[1], out int sleepMs))
        {
            Console.WriteLine($"TestHelperApp: Sleeping for {sleepMs}ms...");
            Thread.Sleep(sleepMs);
            Console.WriteLine("TestHelperApp: Sleep finished.");
            Environment.ExitCode = 0; // Success
        }
        else if (args.Length > 0 && args[0] == "echo")
        {
            Console.WriteLine("TestHelperApp: Echoing arguments.");
            for (int i = 1; i < args.Length; i++)
            {
                Console.WriteLine(args[i]);
            }
            Environment.ExitCode = 0; // Success
        }
        else
        {
            Console.WriteLine("TestHelperApp: Invalid arguments. Use 'sleep <ms>' or 'echo <args...>'");
            Environment.ExitCode = 1; // Failure
        }
    }
}
