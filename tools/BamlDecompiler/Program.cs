using ICSharpCode.BamlDecompiler;
using ICSharpCode.Decompiler.Metadata;
using System.Resources;
using System.Text;

namespace Baml2Xaml;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  BamlDecompiler <app.exe> <Clipboard.Desktop.g.resources> <outDir>");
            Console.Error.WriteLine("  BamlDecompiler <app.exe> --baml <file.baml> <outDir>");
            return 2;
        }

        var appPath = Path.GetFullPath(args[0]);
        if (!File.Exists(appPath))
        {
            Console.Error.WriteLine($"Assembly not found: {appPath}");
            return 2;
        }

        using var pe = new PEFile(appPath, System.Reflection.PortableExecutable.PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(appPath, throwOnError: false, targetFramework: null);
        var decompiler = new XamlDecompiler(pe, resolver, new BamlDecompilerSettings());

        int ok = 0, failed = 0, copied = 0;

        if (args[1] == "--baml")
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("--baml requires <file.baml> <outDir>");
                return 2;
            }

            var src = Path.GetFullPath(args[2]);
            var outDir = Path.GetFullPath(args[3]);
            Directory.CreateDirectory(outDir);
            using var fs = File.OpenRead(src);
            if (Convert(decompiler, fs, Path.Combine(outDir, Path.ChangeExtension(Path.GetFileName(src), ".xaml")), Path.GetFileName(src))) ok++;
            else failed++;
        }
        else
        {
            var resourcesPath = Path.GetFullPath(args[1]);
            var outDir = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(outDir);

            using var rr = new ResourceReader(resourcesPath);
            foreach (System.Collections.DictionaryEntry entry in rr)
            {
                var key = (string)entry.Key;
                byte[] data;
                switch (entry.Value)
                {
                    case Stream valueStream:
                        using (var buffer = new MemoryStream())
                        {
                            valueStream.CopyTo(buffer);
                            data = buffer.ToArray();
                        }
                        break;
                    case byte[] valueBytes:
                        data = valueBytes;
                        break;
                    default:
                        Console.WriteLine($"SKIP {key}: unsupported resource value {entry.Value?.GetType().Name ?? "null"}");
                        continue;
                }

                var outPath = Path.Combine(outDir, key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                if (!key.EndsWith(".baml", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(outPath, data);
                    copied++;
                    continue;
                }

                using var ms = new MemoryStream(data);
                if (Convert(decompiler, ms, Path.ChangeExtension(outPath, ".xaml"), key)) ok++;
                else failed++;
            }
        }

        Console.WriteLine($"converted={ok} failed={failed} copied={copied}");
        return failed > 0 ? 1 : 0;
    }

    private static bool Convert(XamlDecompiler decompiler, Stream stream, string outPath, string label)
    {
        try
        {
            var result = decompiler.Decompile(stream);
            File.WriteAllText(outPath, result.Xaml.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"OK   {label}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL {label}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
