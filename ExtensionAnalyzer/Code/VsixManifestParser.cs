﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExtensionGallery.Code
{
	public class VsixManifestParser
	{
		public Package CreateFromManifest(string tempFolder)
		{
			string xml = File.ReadAllText(Path.Combine(tempFolder, "extension.vsixmanifest"));
			xml = Regex.Replace(xml, "( xmlns(:\\w+)?)=\"([^\"]+)\"", string.Empty);

			XmlDocument doc = new XmlDocument();
			doc.LoadXml(xml);

			Package package = new Package();

			if (doc.GetElementsByTagName("DisplayName").Count > 0)
			{
				Vs2012Format(doc, package);
			}
			else
			{
				Vs2010Format(doc, package);
			}

			string license = ParseNode(doc, "License", false);
			if (!string.IsNullOrEmpty(license))
			{
				string path = Path.Combine(tempFolder, license);
				if (File.Exists(path))
				{
					package.License = File.ReadAllText(path);
				}
			}

			return package;
		}

		private void Vs2012Format(XmlDocument doc, Package package)
		{
			package.ID = ParseNode(doc, "Identity", true, "Id");
			package.Name = ParseNode(doc, "DisplayName", true);
			package.Description = ParseNode(doc, "Description", true);
			package.Version = new Version(ParseNode(doc, "Identity", true, "Version")).ToString();
			package.Author = ParseNode(doc, "Identity", true, "Publisher");
		    package.Tags = ParseNode(doc, "Tags", false);
		    package.IsMsi = ParseBool(ParseNode(doc, "Installation", false, "InstalledByMsi"));
		    package.AllUsers = ParseBool(ParseNode(doc, "Installation", false, "AllUsers"));

		    ParseCommonFields(doc, package);
		}

	    private void Vs2010Format(XmlDocument doc, Package package)
		{
			package.ID = ParseNode(doc, "Identifier", true, "Id");
			package.Name = ParseNode(doc, "Name", true);
			package.Description = ParseNode(doc, "Description", true);
			package.Version = new Version(ParseNode(doc, "Version", true)).ToString();
			package.Author = ParseNode(doc, "Author", true);
		    package.AllUsers = ParseBool(ParseNode(doc, "AllUsers", true));

            ParseCommonFields(doc, package);
		}

	    private void ParseCommonFields(XmlDocument doc, Package package)
	    {
	        package.Icon = ParseNode(doc, "Icon", false);
	        package.Preview = ParseNode(doc, "PreviewImage", false);
	        package.DatePublished = DateTime.UtcNow;
	        package.SupportedVersions = GetSupportedVersions(doc);
	        package.ReleaseNotesUrl = ParseNode(doc, "ReleaseNotes", false);
	        package.GettingStartedUrl = ParseNode(doc, "GettingStartedGuide", false);
	        package.MoreInfoUrl = ParseNode(doc, "MoreInfo", false);
	    }

	    private bool ParseBool(string value)
	    {
	        return value != null && value.Equals(Boolean.TrueString, StringComparison.OrdinalIgnoreCase);
	    }

	    private static IEnumerable<string> GetSupportedVersions(XmlDocument doc)
		{
			XmlNodeList list = doc.GetElementsByTagName("InstallationTarget");

			if (list.Count == 0)
				list = doc.GetElementsByTagName("<VisualStudio");

			List<string> versions = new List<string>();

			foreach (XmlNode node in list)
			{
			    XmlAttribute versionAttribute = node.Attributes["Version"];
			    if (versionAttribute == null) continue;
			    string raw = versionAttribute.Value.Trim('[', '(', ']', ')');
				string[] entries = raw.Split(',');

				foreach (string entry in entries)
				{
					Version v;
					if (Version.TryParse(entry, out v) && !versions.Contains(v.ToString()))
					{
						versions.Add(v.ToString());
					}
				}
			}

			return versions;
		}

		private string ParseNode(XmlDocument doc, string name, bool required, string attribute = "")
		{
			XmlNodeList list = doc.GetElementsByTagName(name);

			if (list.Count > 0)
			{
				XmlNode node = list[0];

				if (string.IsNullOrEmpty(attribute))
					return node.InnerText;

				XmlAttribute attr = node.Attributes[attribute];

				if (attr != null)
					return attr.Value;
			}

			if (required)
			{
				string message = string.Format("Attribute '{0}' could not be found on the '{1}' element in the .vsixmanifest file.", attribute, name);
				//throw new Exception(message);
			}

			return null;
		}

	}
}