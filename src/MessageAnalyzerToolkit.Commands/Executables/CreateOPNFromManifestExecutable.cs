using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using MessageAnalyzerToolkit.Commands.Resources;
using Pingo.CommandLine.Contracts.Execute;
using Pingo.CommandLine.Execute;

namespace MessageAnalyzerToolkit.Executables
{
	internal class CreateOPNFromManifestExecutable : IExecutable
	{
		private static readonly List<string> localizationOrder = new List<string>() { "en-US", "de-DE", "" };
		private static readonly Regex messageRegex = new Regex(@"%(?<param>\d+)", RegexOptions.Singleline | RegexOptions.Compiled);

		public List<string> ManifestFiles { get; set; }

		public string ManifestDirectory { get; set; }

		public IExecuteResult Execute()
		{
			var executeResult = new ExecuteResult { Name = Common.CreateOPN_Name };
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

					string opnContent = CreateOPNFile(document);

					ToolkitHelper.ReplaceETWProvider(analyzerPath, providerName, opnContent);
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

		private static XName GetXName(string name)
		{
			return XName.Get(name, "http://schemas.microsoft.com/win/2004/08/events");
		}

		private string CreateOPNFile(XDocument manifest)
		{
			StringBuilder sb = new StringBuilder();
			XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
			nsMgr.AddNamespace("etw", "http://schemas.microsoft.com/win/2004/08/events");

			XElement providerElement = manifest.XPathSelectElement("/etw:instrumentationManifest/etw:instrumentation/etw:events/etw:provider", nsMgr);
			XElement localizationElement = manifest.XPathSelectElement("/etw:instrumentationManifest/etw:localization", nsMgr);
			WriteProtocolHeader(sb, providerElement);
			WriteNamespaces(sb);
			WriteKeywords(sb, providerElement);
			WriteValueMaps(sb, providerElement, localizationElement);
			WriteEvents(sb, providerElement, localizationElement);
			return sb.ToString();
		}

		private void WriteValueMaps(StringBuilder sb, XElement provider, XElement localizationElement)
		{
			var maps = provider.Element(GetXName("maps"));
			if (maps != null)
			{
				foreach (var etwMap in maps.Elements(GetXName("valueMap")))
				{
					CreateMapPattern(sb, etwMap);
					CreateMapToText(sb, etwMap, localizationElement);
				}
			}
		}

		private void CreateMapToText(StringBuilder sb, XElement etwMap, XElement localizationElement)
		{
			sb.AppendFormat("string {0}_ToText(any e)", etwMap.Attribute("name").Value);
			sb.AppendLine();
			sb.AppendLine("{");
			sb.AppendFormat("\t{0} ev = e as {0};", etwMap.Attribute("name").Value);
			sb.AppendLine();
			sb.AppendLine("\tswitch (ev)");
			sb.AppendLine("\t{");
			foreach (var map in etwMap.Elements(GetXName("map")))
			{
				string mapValue = map.Attribute("value").Value;
				int value;
				if (mapValue.ToLower().Contains("x"))
				{
					value = Convert.ToInt32(mapValue, 16);
				}
				else
				{
					value = Convert.ToInt32(mapValue);
				}
				string message = map.Attribute("message")?.Value;
				message = message.Replace("$(string.", "").Replace(")", "");
				sb.AppendFormat("\t\tcase $ {0} => return \"{1}\";", value, FindLocalization(message, localizationElement));
				sb.AppendLine();
			}
			sb.AppendLine("\t\tdefault => return \"Unknown\";");
			sb.AppendLine("\t}");
			sb.AppendLine("};");
			sb.AppendLine();
		}

		private void CreateMapPattern(StringBuilder sb, XElement etwMap)
		{
			sb.AppendFormat("pattern {0} = enum uint", etwMap.Attribute("name").Value);
			sb.AppendLine();
			sb.AppendLine("\t\t{");
			foreach (var map in etwMap.Elements(GetXName("map")))
			{
				string mapValue = map.Attribute("value").Value;
				int value;
				if (mapValue.ToLower().Contains("x"))
				{
					value = Convert.ToInt32(mapValue, 16);
				}
				else
				{
					value = Convert.ToInt32(mapValue);
				}
				sb.AppendFormat("\t\t\tValue{0} = {0},", value);
				sb.AppendLine();
			}
			sb.AppendLine("\t\t\t...");
			sb.AppendLine("\t\t};");
			sb.AppendLine();
		}

		private void WriteProtocolHeader(StringBuilder sb, XElement provider)
		{
			sb.AppendFormat("protocol {0}", provider.Attribute("name").Value.Replace('-', '_'));
			sb.AppendLine();
			sb.AppendFormat("\twith ImportInfo{{ProviderId = {0},", provider.Attribute("guid").Value);
			sb.AppendLine();
			sb.AppendFormat("\t\t\t\t\tEventsCount = {0},", provider.Element(GetXName("events")).Elements(GetXName("event")).Count());
			sb.AppendLine();
			sb.AppendFormat("\t\t\t\t\tKeywordsCount = {0}}};", provider.Element(GetXName("keywords")).Elements(GetXName("keyword")).Count());
			sb.AppendLine();
		}

		private void WriteNamespaces(StringBuilder sb)
		{
			sb.AppendLine("using Etw;");
			sb.AppendLine("using EtwEvent;");
			sb.AppendLine("using WindowsReference;");
			sb.AppendLine("using Utility;");
			sb.AppendLine();
		}

		private void WriteKeywords(StringBuilder sb, XElement provider)
		{
			sb.AppendLine("type Keywords");
			sb.AppendLine("{");
			sb.AppendLine("\tWindowsEtwKeywords StandardKeywords;");
			sb.AppendLine();
			List<Tuple<string, string>> keywords = new List<Tuple<string, string>>();
			foreach (var keyword in provider.Element(GetXName("keywords")).Elements(GetXName("keyword")))
			{
				string keywordName = keyword.Attribute("name").Value;
				long mask;
				string maskValue = keyword.Attribute("mask").Value;
				if (maskValue.ToLower().Contains("x"))
				{
					mask = Convert.ToInt64(maskValue, 16);
				}
				else
				{
					mask = Convert.ToInt64(maskValue);
				}

				keywords.Add(new Tuple<string, string>(keywordName, mask.ToString("X16")));
			}

			foreach (var keyword in keywords.OrderBy(p => p.Item2))
			{
				sb.AppendFormat("\tbool {0};", keyword.Item1);
				sb.AppendLine();
				sb.AppendLine();
			}

			sb.AppendLine("\tpublic static Keywords Decode(ulong keyword)");
			sb.AppendLine("\t{");
			sb.AppendLine("\t\tKeywords result = new Keywords();");
			sb.AppendLine("\t\tresult.StandardKeywords = WindowsEtwKeywordsDecoder(keyword);");

			foreach (var keyword in keywords.OrderBy(p => p.Item2))
			{
				sb.AppendFormat("\t\tresult.{0} = EtwKeywordDecoder(keyword, 0x{1});", keyword.Item1, keyword.Item2);
				sb.AppendLine();
			}

			sb.AppendLine("\t\treturn result;");
			sb.AppendLine("\t}");

			sb.AppendLine("}");
			sb.AppendLine();
		}

		private void WriteEvents(StringBuilder sb, XElement provider, XElement localization)
		{
			Dictionary<Tuple<int, int, string>, XElement> events = new Dictionary<Tuple<int, int, string>, XElement>();
			Dictionary<string, List<Tuple<string, string, string>>> templateData = new Dictionary<string, List<Tuple<string, string, string>>>();
			foreach (var etwEvent in provider.Element(GetXName("events")).Elements(GetXName("event")))
			{
				events.Add(new Tuple<int, int, string>(int.Parse(etwEvent.Attribute("value").Value), int.Parse(etwEvent.Attribute("version").Value), etwEvent.Attribute("symbol").Value), etwEvent);
			}

			foreach (var template in provider.Element(GetXName("templates")).Elements(GetXName("template")))
			{
				List<Tuple<string, string, string>> datas = new List<Tuple<string, string, string>>();
				templateData.Add(template.Attribute("tid").Value, datas);
				foreach (var data in template.Elements(GetXName("data")))
				{
					datas.Add(new Tuple<string, string, string>(ConvertETWType(data.Attribute("inType").Value), ConvertToPascalCase(data.Attribute("name").Value), data.Attribute("map")?.Value));
				}
			}

			//WriteEvent Messages
			WriteEvents(sb, provider, localization, events, templateData);

			//Write Template Messages
			WriteTemplates(sb);

			//WriteEndpoint
			WriteEndPoint(sb, provider, events);

			//WriteETWActor
			WriteETWActor(sb, provider, events);
		}

		private void WriteETWActor(StringBuilder sb, XElement provider, Dictionary<Tuple<int, int, string>, XElement> events)
		{
			sb.AppendFormat("public autostart actor actor_{0}", provider.Attribute("guid").Value.Replace("-", "").TrimStart('{').TrimEnd('}'));
			sb.AppendLine();
			sb.AppendLine("\t(EtwEvent.Node node)");
			sb.AppendLine("{");
			sb.AppendLine("\tprocess node accepts m:EtwProviderMsg");
			sb.AppendFormat("\t\twhere (m.EventRecord.Header.ProviderId == {0} )", provider.Attribute("guid").Value);
			sb.AppendLine();
			sb.AppendLine("\t{");

			sb.AppendLine("\t\tswitch(m.EventRecord.Header.Descriptor.Version)");
			sb.AppendLine("\t\t{");
			foreach (var etwEventGroup in events.GroupBy(p => p.Key.Item2))
			{
				sb.AppendFormat("\t\t\t//EventVersion {0}", etwEventGroup.Key);
				sb.AppendLine();
				sb.AppendFormat("\t\t\tcase $ {0} =>", etwEventGroup.Key);
				sb.AppendLine();
				sb.AppendLine("\t\t\t\tswitch(m.EventRecord.Header.Descriptor.Id)");
				sb.AppendLine("\t\t\t\t{");
				foreach (var etwEvent in etwEventGroup)
				{
					sb.AppendFormat("\t\t\t\t\t//{0} =>", GetEventName(etwEvent.Key));
					sb.AppendLine();
					sb.AppendFormat("\t\t\t\t\tcase $ {0} =>", etwEvent.Key.Item1);
					sb.AppendLine();
					sb.AppendLine("\t\t\t\t\t\tswitch(m.Payload)");
					sb.AppendLine("\t\t\t\t\t\t{");
					sb.AppendFormat("\t\t\t\t\t\t\tcase decodedMsg: {0} from BinaryDecoder <{0}> =>", GetEventName(etwEvent.Key));
					sb.AppendLine();
					sb.AppendLine("\t\t\t\t\t\t\t{");
					sb.AppendLine("\t\t\t\t\t\t\t\tdecodedMsg.EtwKeywords = Keywords.Decode(m.EventRecord.Header.Descriptor.Keywords);");
					sb.AppendLine("\t\t\t\t\t\t\t\tdecodedMsg.EventId = m.EventRecord.Header.Descriptor.Id;");
					sb.AppendFormat("\t\t\t\t\t\t\t\tep_{0} ep = endpoint ep_{0};", provider.Attribute("name").Value.Replace('-', '_'));
					sb.AppendLine();
					sb.AppendLine("\t\t\t\t\t\t\t\tdispatch ep accepts decodedMsg;");
					sb.AppendLine("\t\t\t\t\t\t\t}");
					sb.AppendLine("\t\t\t\t\t\t\tdefault =>");
					sb.AppendFormat("\t\t\t\t\t\t\t\tThrowDecodingException(\"{0}\", \"{1}\");", provider.Attribute("name").Value, GetEventName(etwEvent.Key));
					sb.AppendLine();
					sb.AppendLine("\t\t\t\t\t\t}");
					sb.AppendLine();
				}
				sb.AppendLine("\t\t\t\t}");
				sb.AppendLine();
			}
			sb.AppendLine("\t\t}");
			sb.AppendLine("\t}");
			sb.AppendLine("}");
			sb.AppendLine();
		}

		private static string GetEventName(Tuple<int, int, string> etwEvent)
		{
			string eventName = etwEvent.Item3;
			if (etwEvent.Item2 > 1)
			{
				eventName = etwEvent.Item3 + "_V" + etwEvent.Item2;
			}
			return eventName;
		}

		private void WriteEvents(StringBuilder sb, XElement provider, XElement localization, Dictionary<Tuple<int, int, string>, XElement> events, Dictionary<string, List<Tuple<string, string, string>>> templateData)
		{
			foreach (var etwEvent in events)
			{
				string template = "EventTemplate";
				List<Tuple<string, string, string>> templateDatas = null;
				XAttribute templateAttr = etwEvent.Value.Attribute("template");
				if (templateAttr != null && !templateData.TryGetValue(templateAttr.Value, out templateDatas))
				{
					throw new Exception("ETW template not found: " + templateAttr.Value);
				}

				sb.AppendFormat("//Event {0}", etwEvent.Key);
				sb.AppendLine();
				sb.AppendFormat("message {0}: {1}", GetEventName(etwEvent.Key), template);
				sb.AppendLine();
				sb.AppendLine("{");

				if (templateDatas != null)
				{
					foreach (var data in templateDatas)
					{
						if (!String.IsNullOrWhiteSpace(data.Item3))
						{
							sb.AppendFormat("\t{0} {1} with DisplayInfo{{ToText = {0}_ToText}};", data.Item3, data.Item2);
						}
						else
						{
							sb.AppendFormat("\t{0} {1};", data.Item1, data.Item2);
						}
						sb.AppendLine();
						sb.AppendLine("");
					}
				}

				sb.AppendLine("\tstring GetSummary()");
				sb.AppendLine("\t{");

				string message = etwEvent.Value.Attribute("message")?.Value;
				if (message != null)
				{
					message = message.Replace("$(string.", "").Replace(")", "");
					string summary = FindLocalization(message, localization);
					sb.AppendLine("\t\t// " + summary + ";");

					MatchCollection matches = messageRegex.Matches(summary);
					if ((templateDatas == null || templateDatas.Count == 0) && matches.Count > 0)
					{
						throw new Exception("ETW template data items don't correspond to expected message parameters");
					}
					summary = "\"" + summary + "\"";

					foreach (Match param in matches)
					{
						int paramNr = int.Parse(param.Groups["param"].Value);
						var dataItem = templateDatas[paramNr - 1];
						if (!string.IsNullOrWhiteSpace(dataItem.Item3))
						{
							summary = summary.Replace(param.Value, string.Format("\" + \n\t\t\t\t{1}_ToText(this.{0}) + \"", dataItem.Item2, dataItem.Item3));
						}
						else
						{
							summary = summary.Replace(param.Value, string.Format("\" + \n\t\t\t\t(this.{0} as string) + \"", dataItem.Item2));
						}
					}
					summary = summary.Replace("\"\" +", "").Replace("+ \"\"", "").Trim();
					sb.AppendLine("\t\treturn " + summary + ";");
				}
				else
				{
					sb.AppendLine("\t\treturn \"\";");
				}
				sb.AppendLine("\t}");
				sb.AppendLine();
				sb.AppendLine("\tpublic override string ToString()");
				sb.AppendLine("\t{");
				sb.AppendLine("\t\treturn GetSummary();");
				sb.AppendLine("\t}");
				sb.AppendLine("}");
				sb.AppendLine();
			}
		}

		private static void WriteTemplates(StringBuilder sb)
		{
			sb.AppendFormat("message {0}", "EventTemplate");
			sb.AppendLine();
			sb.AppendLine("{");
			sb.AppendLine("\tKeywords EtwKeywords with Standard.Encoding{Ignore = true};");
			sb.AppendLine();
			sb.AppendLine("\tint EventId with Standard.Encoding{Ignore = true};");
			sb.AppendLine("}");
			sb.AppendLine();
		}

		private static void WriteEndPoint(StringBuilder sb, XElement provider, Dictionary<Tuple<int, int, string>, XElement> events)
		{
			sb.AppendFormat("endpoint ep_{0}", provider.Attribute("name").Value.Replace('-', '_'));
			sb.AppendLine();
			foreach (var etwEvent in events)
			{
				sb.AppendFormat("\t\t\taccepts {0}", GetEventName(etwEvent.Key));
				sb.AppendLine();
			}
			sb.AppendLine(";");
			sb.AppendLine();
		}

		private string FindLocalization(string locValue, XElement localization)
		{
			XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
			nsMgr.AddNamespace("etw", "http://schemas.microsoft.com/win/2004/08/events");

			foreach (var locOrder in localizationOrder)
			{
				XElement locElement = localization.XPathSelectElement(string.Format("./etw:resources[@culture='{0}']/etw:stringTable/etw:string[@id='{1}']", locOrder, locValue), nsMgr);
				if (locElement != null)
				{
					return locElement.Attribute("value").Value;
				}
			}

			return "\"\"";
		}

		private string ConvertETWType(string value)
		{
			switch (value)
			{
				case "win:GUID":
					return "guid";

				case "win:UnicodeString":
					return "string";

				case "win:Int32":
					return "int";

				case "win:Int64":
					return "long";

				case "win:UInt32":
					return "uint";

				case "win:UInt64":
					return "ulong";

				case "win:FILETIME":
					return "DateTime";

				default:
					throw new Exception("Manifest contains unknown etw type: " + value);
			}
		}

		private static string ConvertToPascalCase(string phrase)
		{
			string[] splittedPhrase = phrase.Split(' ', '-', '.');
			var sb = new StringBuilder();

			sb = new StringBuilder();

			foreach (String s in splittedPhrase)
			{
				char[] splittedPhraseChars = s.ToCharArray();
				if (splittedPhraseChars.Length > 0)
				{
					splittedPhraseChars[0] = ((new String(splittedPhraseChars[0], 1)).ToUpper().ToCharArray())[0];
				}
				sb.Append(new String(splittedPhraseChars));
			}
			return sb.ToString();
		}

		public IExecuteResult LastResult { get; private set; }
	}
}