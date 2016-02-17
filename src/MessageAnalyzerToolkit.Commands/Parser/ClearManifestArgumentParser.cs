using System;
using System.Collections.Generic;
using Fclp;
using MessageAnalyzerToolkit.Commands.Resources;

namespace MessageAnalyzerToolkit.Parser
{
    public class ClearManifestArgumentParser
    {
        public List<string> ManifestFiles { get; private set; }

        public string ManifestDirectory { get; private set; }

        public void Parse(string[] args)
        {
            var parser = new Fclp.FluentCommandLineParser();

            parser.Setup<List<string>>(CaseType.CaseInsensitive, Common.OptionSource_LongName,
                Common.OptionSource_ShortName)
                .Callback(items => ManifestFiles = items);

            parser.Setup<string>(CaseType.CaseInsensitive, Common.OptionDirectory_LongName,
                Common.OptionDirectory_ShortName)
                .Callback(dir => ManifestDirectory = dir);

            var result = parser.Parse(args);
            if (result.HasErrors)
            {
                throw new Exception(result.ErrorText);
            }
        }
    }
}