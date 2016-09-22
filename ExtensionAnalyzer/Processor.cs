using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AsmSpy;
using ExtensionGallery.Code;

namespace ConsoleApplication1
{
    class Processor
    {
        public void ProcessAll(string extensionsDir, string extractDir)
        {
            PackageHelper packageHelper = new PackageHelper(extensionsDir, extractDir);

            Console.Out.WriteLine("About to parse packages");

            // Ensure that extensionsDir exists
            Console.Out.WriteLine("Looking for Extensions Dir: " + extensionsDir);
            if (!Directory.Exists(extensionsDir))
            {
                Console.Out.WriteLine("Directory does not exist: " + extensionsDir);
                throw new ArgumentException("Extension Dir does not exist.");
            }

            // Ensure that extractDir is wiped clean (if it exists)
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }

            // Process each file in the extensionsDir
            Console.Out.WriteLine("Looking for extensions in Extensions Dir: " + extensionsDir);
            IEnumerable<string> filesInDirectory = Directory.EnumerateFiles(extensionsDir);
            foreach (string fileInDir in filesInDirectory)
            {
                if (fileInDir.EndsWith(".vsix") || fileInDir.EndsWith(".zip"))
                {
                    // Found VSIX file. Attempt to process it.
                    string fileToProcess = Path.Combine(extensionsDir, fileInDir);
                    Package package = packageHelper.ProcessVsix(fileToProcess);
                    Console.Out.WriteLine("Processed VSIX: " + fileInDir + " Package: " + package.Name + " IsMsi: " + package.IsMsi + " AllUsers: " + package.AllUsers);
                    //ProcessAssemblyInfo()
                }
            }

            Console.Out.WriteLine("Finished parsing all files packages.");

            // Now inspect all the DLLs in each extracted directory
            //FetchAssemblyList(extractDir);

            PrintAnalysis(packageHelper);

            Console.Out.WriteLine("Finished program");
        }

        public void FetchAssemblyList(string directoryPath)
        {
            ConsoleLogger consoleLogger = new ConsoleLogger(true);

            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
            if (!Directory.Exists(directoryPath))
            {
                consoleLogger.LogMessage(string.Format("Directory: '{0}' does not exist.", directoryPath));
                throw new ArgumentException("Cannot process files in directory: " + directoryPath);
            }

            // For each directory inside the directoryPath, analyze its dependencies
            IEnumerable<string> extensionDirs = Directory.EnumerateDirectories(directoryPath);
            foreach (string extensionDir in extensionDirs)
            {
                IDependencyAnalyzer analyzer = new DependencyAnalyzer() {DirectoryInfo = new DirectoryInfo(extensionDir)};
                DependencyAnalyzerResult result = analyzer.Analyze(consoleLogger);

                // Output results to Console
                IDependencyVisualizer visualizer = new ConsoleVisualizer(result);
                visualizer.Visualize();

                // Export results to file
                IDependencyVisualizer export = new DgmlExport(result, Path.Combine(directoryPath, extensionDir + ".references.dgml"), consoleLogger);
                export.Visualize();
            }

            Console.Out.WriteLine("Finished fetching assemblies");

        }

        private void PrintAnalysis(PackageHelper packageHelper)
        {
            int countExtensions = 0;
            int countMSI = 0;
            int countAllUsers = 0;
            List<Package> packageCache = packageHelper.PackageCache;
            foreach (Package package in packageCache)
            {
                countExtensions++;
                if (package.IsMsi) countMSI++;
                if (package.AllUsers) countAllUsers++;
                Console.Out.WriteLine("Package: " + package.Name + " IsMsi: " + package.IsMsi + " AllUsers: " + package.AllUsers);
            }
            Console.Out.WriteLine("Total extensions: " + countExtensions);
            Console.Out.WriteLine("Total MSI: " + countMSI);
            Console.Out.WriteLine("AllUsers: " + countAllUsers);
        }
    }
}