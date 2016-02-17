using System;
using System.ComponentModel.Composition;
using MessageAnalyzerToolkit.Executables;
using MessageAnalyzerToolkit.Parser;
using Pingo.CommandLine.Contracts.Command;
using Pingo.CommandLine.Contracts.Execute;
using Pingo.CommandLine.Execute;

namespace MessageAnalyzerToolkit.Commands
{
    [Export(typeof(ICommand))]
    [ExportMetadata("Command", "ClearManifest")]
    public class ClearManifestCommand : ICommand
    {
        public IExecuteResult ExecuteCommand(string[] args)
        {
            var executeResult = new ExecuteResult { Name = Resources.Common.ClearManifest_Name };
            IExecuteResult finalResult = executeResult;
            try
            {
                var parser = new ClearManifestArgumentParser();
                parser.Parse(args);

                var executable = new ClearManifestExecutable { ManifestFiles = parser.ManifestFiles, ManifestDirectory = parser.ManifestDirectory };
                finalResult = executable.Execute();
                executeResult.ResultCode = (int)ResultCodes.Success;
            }
            catch (Exception e)
            {
                var executeError = new ExecuteError { ErrorText = e.Message };
                executeResult.ErrorsStore.Add(executeError);
                executeResult.ResultCode = (int)ResultCodes.Fail;
            }
            return finalResult;
        }
    }
}