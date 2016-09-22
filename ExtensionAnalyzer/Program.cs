using System;
using System.IO;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Out.WriteLine("Analyzer started.");
            string extensionsDir = @"C:\Extensions";
            string extractDir = Path.Combine(extensionsDir, "extracted");

            Processor processor = new Processor();
//            processor.ProcessAll(extensionsDir, extractDir);
            processor.FetchAssemblyList(extractDir);
        }
    }
}