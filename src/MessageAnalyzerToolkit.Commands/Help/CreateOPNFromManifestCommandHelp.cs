using System.Collections;
using System.ComponentModel.Composition;
using MessageAnalyzerToolkit.Commands.Resources;
using Pingo.CommandLine;
using Pingo.CommandLine.Contracts.Help;

namespace MessageAnalyzerToolkit.Help
{
    [Export(typeof(ICommandHelp))]
    [ExportMetadata("Command", "CreateOPN")]
    public class CreateOPNFromManifestCommandHelp : ICommandHelp
    {
        private SortedList _sortedOptionHelps;

        public string Name
        {
            get { return Common.CreateOPN_Name; }
        }

        public string Description
        {
            get { return Common.CreateOPN_Description; }
        }

        public string Usage
        {
            get { return Common.CreateOPN_Description; }
        }

        public string Detailed
        {
            get { return Common.CreateOPN_Description; }
        }

        public SortedList Options
        {
            get
            {
                if (_sortedOptionHelps == null)
                {
                    var optionNameSource = "-" + Common.OptionSource_LongName;
                    _sortedOptionHelps = new SortedList
                    {
                        {
                           optionNameSource, new OptionHelp()
                            {
                                Name = optionNameSource,
                                Description = Common.OptionSources_Description,
                                Usage =
                                    string.Format(Common.Format_OptionUsage,
                                        optionNameSource, Common.Argument_PathToSource)
                            }
                        },
                        {
                            Common.OptionHelp_Name, new OptionHelp()
                            {
                                Name = Common.OptionHelp_Name,
                                Description = Common.OptionHelp_Description,
                                Usage = Common.OptionHelp_Usage
                            }
                        }
                    };
                }
                return _sortedOptionHelps;
            }
        }
    }
}