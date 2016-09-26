using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using AsmSpy;
using ExtensionGallery.Code;

namespace ConsoleApplication1
{
    class Processor
    {
        static void Main(string[] args)
        {
            Console.Out.WriteLine("Analyzer started.");

            const string extensionsDir = @"C:\Extensions";
            string extensionsExtractDir = Path.Combine(extensionsDir, "extracted");
            const string baseInstallDir = @"C:\Extensions\BaseInstall";
            string baseInstallExtractDir = GetExtractDir(baseInstallDir);

//            CleanDirectory(GetExtractDir(baseInstallDir));
            ExtractVsixFiles(baseInstallDir);

            // Get list of all DLLs in the Base Install
            Console.Out.WriteLine("Fetching list of DLLs in Base Install");
            string[] allBaseDlls = FindAllDlls(baseInstallExtractDir);
            SortedSet<string> baseDllNames = StripToRawName(allBaseDlls);
            PrintArray(baseDllNames, "  ");
            Console.Out.WriteLine($"Found {baseDllNames.Count} DLLs");

//            ProcessAll(extensionsDir, extractDir);
            ProcessAssemblyComparisonForAllExtensions(extensionsExtractDir, baseDllNames);
        }

        private static SortedSet<string> StripToRawName(string[] allBaseDlls)
        {
            SortedSet<string> rawNames = new SortedSet<string>();
            foreach (string fullName in allBaseDlls)
            {
                rawNames.Add(GetVsixName(fullName));
            }
            return rawNames;
        }

        private static void PrintArray(IEnumerable<string> allBaseDlls, string padding)
        {
            foreach(string s in allBaseDlls)
            {
                Console.Out.WriteLine(padding + s);
            }
        }

        private static void CleanDirectory(string directory)
        {
            Console.Out.WriteLine($"Deleting directory: {directory}");
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
                Console.Out.WriteLine($"Directory deleted: {directory}");
            }
            else
            {
                Console.Out.WriteLine($"Directory does not exist: {directory}");
            }
        }

        public static void ExtractVsixFiles(string extensionsDir)
        {
            Console.Out.WriteLine("Looking for extensions in Extensions Dir: " + extensionsDir);

            int extractedCount = 0;
            int skippedCount = 0;
            // Process each VSIX (or Zip) in the extensionsDir
            foreach (string fileInDir in Directory.EnumerateFiles(extensionsDir))
            {
                // If not a VSIX (or Zip), skip it.
                if (!fileInDir.EndsWith(".vsix") && !fileInDir.EndsWith(".zip")) continue;

                // Define the directory to extract to
                var extractDir = GenerateVsixExtractDir(extensionsDir, fileInDir);

                // If directory exists, log message then move on
                if (Directory.Exists(extractDir))
                {
                    Console.Out.WriteLine("Extension already extracted: " + fileInDir);
                    skippedCount++;
                    continue;
                }

                // Extract VSIX to directory
                string vsixToProcess = Path.Combine(extensionsDir, fileInDir);
                ExtractFileToDirectory(vsixToProcess, extractDir);
                Console.Out.WriteLine($"Extracted file: {fileInDir} to directory: {extractDir}");
                extractedCount++;
            }

            // Finished. Report results
            Console.Out.WriteLine($"Extracted extensions: {extractedCount}");
            Console.Out.WriteLine($"Skipped extensions: {skippedCount}");

        }

        private static string GetExtractDir(string baseDirectory)
        {
            // Create the extract directory
            const string extracted = "extracted";
            return Path.Combine(baseDirectory, extracted);
        }

        private static string GenerateVsixExtractDir(string extensionsDir, string fileInDir)
        {
            // Create the extract directory
            return Path.Combine(GetExtractDir(extensionsDir), GetVsixName(fileInDir));
        }

        private static string GetVsixDirName(string fullFileName)
        {
            int startPosn = fullFileName.LastIndexOf(@"\", StringComparison.Ordinal);
            return fullFileName.Substring(startPosn + 1);
        }

        private static string GetVsixName(string fullFileName)
        {
            // Extract the VSIX name from the file name
            int startPosn = fullFileName.LastIndexOf(@"\", StringComparison.Ordinal);
            int endPosn = fullFileName.LastIndexOf(@".", StringComparison.Ordinal);
            if (endPosn < 0)
            {
                return fullFileName.Substring(startPosn + 1);
            }
            return fullFileName.Substring(startPosn + 1, endPosn - 1 - startPosn);
        }

        private static void ExtractFileToDirectory(string fileToExtract, string extractDir)
        {
            // Extract (Unzip) the VSIX to a temporary directory
            ZipFile.ExtractToDirectory(fileToExtract, extractDir);
        }

        public static void ProcessAll(string extensionsDir, string extractDir)
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
            CleanDirectory(extractDir);

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

        public static string[] FindAllDlls(string parentSearchDir)
        {
            return Directory.GetFiles(parentSearchDir, "*.dll", SearchOption.AllDirectories);
        }

        public static void ProcessAssemblyComparisonForAllExtensions(string directoryPath, IEnumerable<string> baseInstallDlls)
        {
            ConsoleLogger consoleLogger = new ConsoleLogger(true);

            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
            if (!Directory.Exists(directoryPath))
            {
                consoleLogger.LogMessage($"Directory: '{directoryPath}' does not exist.");
                throw new ArgumentException("Cannot process files in directory: " + directoryPath);
            }

            List<string> compatibleExtensions = new List<string>();
            List<string> incompatibleExtensions = new List<string>();

            // For each directory inside the directoryPath, analyze its dependencies
            IEnumerable<string> extensionDirs = Directory.EnumerateDirectories(directoryPath);
            foreach (string extensionDir in extensionDirs)
            {
                string vsixName = GetVsixDirName(extensionDir);
                Console.Out.WriteLine("Processing assembly directory: {0}", extensionDir);
                bool isCompatible = ProcessExtensionAssemblies(extensionDir, baseInstallDlls);
                if (isCompatible)
                {
                    compatibleExtensions.Add(vsixName);
                }
                else
                {
                    incompatibleExtensions.Add(vsixName);
                }
            }

            Console.Out.WriteLine($"Compatible extensions: ({compatibleExtensions.Count})");
            PrintList(compatibleExtensions, "  ", "");

            Console.Out.WriteLine($"Incompatible extensions: ({incompatibleExtensions.Count})");
            PrintList(incompatibleExtensions, "  ", "");
        }

        private static bool ProcessExtensionAssemblies(string extensionDir, IEnumerable<string> baseInstallDlls)
        {
            // Fetch all assembly information for all DLLs in the extensionDir
            DependencyAnalyzerResult result = DependencyAnalyzer.Analyze(new DirectoryInfo(extensionDir));

            // Get references assembly list
            IEnumerable<string> referencedAssemblies = FetchAssemblyNames(result, true);
            PrintList(referencedAssemblies, "    ", "");

            // Compare referenced assembly list to DLLs in Base Install
            SortedSet<string> unavailableAssemblies = GetSetSubtraction(referencedAssemblies, baseInstallDlls);

            // Output results for this extensionDir
            OutputCompatibilityResults(GetVsixDirName(extensionDir), unavailableAssemblies);

            return unavailableAssemblies.Count == 0;
        }

        private static void OutputCompatibilityResults(string vsixName, SortedSet<string> unavailableAssemblies)
        {
            if (unavailableAssemblies.Count == 0)
            {
                Console.Out.WriteLine("Extension can run on Base Install: " + vsixName);
            }
            else
            {
                Console.Out.WriteLine("Extension not compatible with Base Install: " + vsixName);
                Console.Out.WriteLine("Missing DLLs:");
                PrintList(unavailableAssemblies, "  ", "");
            }
        }

        private static SortedSet<string> GetSetSubtraction(IEnumerable<string> assemblies, IEnumerable<string> baseInstallDlls)
        {
            SortedSet<string> setSubtraction = new SortedSet<string>();
            foreach (string assembly in assemblies)
            {
                if (!baseInstallDlls.Contains(assembly, StringComparer.OrdinalIgnoreCase))
                    setSubtraction.Add(assembly);
            }
            return setSubtraction;
        }

        private static IEnumerable<string> FetchAssemblyNames(DependencyAnalyzerResult result, bool skipSystem)
        {
            SortedSet<string> assemblyNames = new SortedSet<string>();
            foreach (string assemblyResultName in result.Assemblies.Keys.AsEnumerable())
            {
                // Skip the system assemblies if flagged to skip
                if (skipSystem && (assemblyResultName.StartsWith("System") || assemblyResultName.StartsWith("mscorlib"))) continue;

                string rawAssemblyName = GetAssemblyNameFromResult(assemblyResultName);

                assemblyNames.Add(rawAssemblyName);
            }
            return assemblyNames;
        }

        private static string GetAssemblyNameFromResult(string assemblyResultName)
        {
            return assemblyResultName.Substring(0, assemblyResultName.IndexOf(",", StringComparison.Ordinal));
        }

        private static void PrintList(IEnumerable<string> listToPrint, string prefix, string suffix)
        {
            foreach (string listItem in listToPrint)
            {
                Console.Out.WriteLine($"{prefix}{listItem}{suffix}");
            }
        }

        private static void PrintAnalysis(PackageHelper packageHelper)
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