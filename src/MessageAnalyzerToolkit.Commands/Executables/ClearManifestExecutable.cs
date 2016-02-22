using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using MessageAnalyzerToolkit.Commands.Resources;
using Pingo.CommandLine.Contracts.Execute;
using Pingo.CommandLine.Execute;

namespace MessageAnalyzerToolkit.Executables
{
    internal class ClearManifestExecutable : IExecutable
    {
        public List<string> ManifestFiles { get; set; }

        public string ManifestDirectory { get; set; }

        public IExecuteResult Execute()
        {
            var executeResult = new ExecuteResult { Name = Common.ClearManifest_Name };
            try
            {
                //detect Message analyzer path

                string analyzerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\MessageAnalyzer\");

                //if not found throw exception
                if (!Directory.Exists(analyzerPath))
                {
                    throw new Exception(string.Format(Common.Format_MessageAnalyzerNotInstalled, analyzerPath));
                }

                IEnumerable<string> list = null;
                if (this.ManifestFiles != null && this.ManifestFiles.Count > 0)
                {
                    list = this.ManifestFiles;
                }

                if (list == null)
                {
                    if (this.ManifestDirectory != null)
                    {
                        list = Directory.EnumerateFiles(this.ManifestDirectory, "*.etwManifest.man");
                    }
                    else
                    {
                        list = new string[] { };
                    }
                }
                else
                {
                    if (this.ManifestDirectory != null)
                    {
                        list = list.Union(Directory.EnumerateFiles(this.ManifestDirectory, "*.etwManifest.man"));
                    }
                }

                foreach (var file in list)
                {
                    var fi = new FileInfo(file);
                    if (!fi.Exists)
                    {
                        throw new Exception(string.Format(Common.Format_FileNotFound, fi.FullName));
                    }

                    //find etw provider id
                    XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
                    nsMgr.AddNamespace("etw", "http://schemas.microsoft.com/win/2004/08/events");
                    XDocument document = XDocument.Load(fi.FullName);

                    //find cache entries in analyzer path
                    Guid providerID;
                    string providerName;

                    XElement providerElement = document.XPathSelectElement("/etw:instrumentationManifest/etw:instrumentation/etw:events/etw:provider", nsMgr);
                    if (providerElement != null)
                    {
                        providerName = providerElement.Attribute("name").Value;
                        providerID = Guid.Parse(providerElement.Attribute("guid").Value);
                    }
                    else
                    {
                        throw new Exception(string.Format(Common.Format_ProviderNotFound));
                    }

                    ToolkitHelper.DeleteETWProviderCache(analyzerPath, providerName);
                }
            }
            catch (Exception e)
            {
                var executeError = new ExecuteError { ErrorText = e.Message };
                executeResult.ErrorsStore.Add(executeError);
            }
            LastResult = executeResult;
            return LastResult;
        }

        public IExecuteResult LastResult { get; private set; }
    }
}