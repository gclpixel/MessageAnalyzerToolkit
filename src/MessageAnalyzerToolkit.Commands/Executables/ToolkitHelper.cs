using System.IO;

namespace MessageAnalyzerToolkit.Executables
{
    public static class ToolkitHelper
    {
        public static void DeleteETWProviderCache(string analyzerPath, string providerName)
        {
            string analyzerProviderName = providerName.Replace('-', '_');

            //delete entries
            foreach (var opnFileName in Directory.EnumerateFiles(Path.Combine(analyzerPath, "SystemEtwManifestOPNCache"), analyzerProviderName + "*.opn", SearchOption.TopDirectoryOnly))
            {
                File.Delete(opnFileName);
            }

            foreach (var opnFileName in Directory.EnumerateFiles(Path.Combine(analyzerPath, @"OPNAndConfiguration\OPNs\Facton\"), analyzerProviderName + "*.opn", SearchOption.TopDirectoryOnly))
            {
                File.Delete(opnFileName);
            }

            foreach (var cacheFileName in Directory.EnumerateFiles(Path.Combine(analyzerPath, "CodecCache"), analyzerProviderName + "*.*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(cacheFileName);
            }

            foreach (var cacheFileName in Directory.EnumerateFiles(Path.Combine(analyzerPath, "CompilationCache"), analyzerProviderName + "*.*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(cacheFileName);
            }
        }

        public static void ReplaceETWProvider(string analyzerPath, string providerName, string opnFileContent)
        {
            string analyzerProviderName = providerName.Replace('-', '_');

            DeleteETWProviderCache(analyzerPath, providerName);

            string directoryPath = Path.Combine(analyzerPath, @"OPNAndConfiguration\OPNs\Facton\");
            Directory.CreateDirectory(directoryPath);

            File.WriteAllText(Path.Combine(directoryPath, analyzerProviderName + ".opn"), opnFileContent);
        }
    }
}