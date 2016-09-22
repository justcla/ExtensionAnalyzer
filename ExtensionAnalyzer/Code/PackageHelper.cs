using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;

namespace ExtensionGallery.Code
{
	public class PackageHelper
	{
	    private string _extensionRoot;
	    private readonly string _extractDir;
	    public static List<Package> _cache;

		public PackageHelper(string extensionRoot, string extractDir)
		{
		    _extensionRoot = extensionRoot;
		    _extractDir = extractDir;
		}

		public List<Package> PackageCache
		{
			get
			{
				if (_cache == null)
					_cache = GetAllPackages();

				return _cache;
			}
		}

		private List<Package> GetAllPackages()
		{
			List<Package> packages = new List<Package>();

			if (!Directory.Exists(_extensionRoot))
			{
				return packages.ToList();
			}

			foreach (string extension in Directory.EnumerateDirectories(_extensionRoot))
			{
				string json = Path.Combine(extension, "extension.json");
				if (File.Exists(json))
				{
					string content = File.ReadAllText(json);
					Package package = JsonConvert.DeserializeObject(content, typeof(Package)) as Package;
					packages.Add(package);
				}
			}

			return packages.OrderByDescending(p => p.DatePublished).ToList();
		}

		public Package GetPackage(string id)
		{
			if (PackageCache.Any(p => p.ID == id))
			{
				return PackageCache.SingleOrDefault(p => p.ID == id);
			}

			string folder = Path.Combine(_extensionRoot, id);
			List<Package> packages = new List<Package>();

			return DeserializePackage(folder);
		}

	    private void AddToPackageCache(Package package)
	    {
	        // Remove any existing package from PackageCache
	        Package existing = PackageCache.FirstOrDefault(p => p.ID == package.ID);
	        if (PackageCache.Contains(existing))
	        {
	            PackageCache.Remove(existing);
	        }

	        // Add the package to the PackageCache
	        PackageCache.Add(package);

	    }

	    private static Package DeserializePackage(string version)
		{
			string content = File.ReadAllText(Path.Combine(version, "extension.json"));
			return JsonConvert.DeserializeObject(content, typeof(Package)) as Package;
		}

	    public Package ProcessVsix(string vsixToProcess)
	    {
	        // Define a temporary directory to unzip the VSIX into
	        int startPosn = vsixToProcess.LastIndexOf(@"\", StringComparison.Ordinal);
	        int endPosn = vsixToProcess.LastIndexOf(@".", StringComparison.Ordinal);
	        string vsixName = vsixToProcess.Substring(startPosn+1, endPosn-1-startPosn);
	        string tempFolder = Path.Combine(_extractDir, vsixName);

	        try
	        {
	            // Extract (Unzip) the VSIX to a temporary directory
	            ZipFile.ExtractToDirectory(vsixToProcess, tempFolder);

	            // Parse the manifest
	            VsixManifestParser parser = new VsixManifestParser();
	            Package package = parser.CreateFromManifest(tempFolder);

	            // Cache the result for later analysis
	            AddToPackageCache(package);

	            return package;
	        }
	        finally
	        {
	            // Delete the temporary directory
//	            Directory.Delete(tempFolder, true);
	        }
	    }
	}
}