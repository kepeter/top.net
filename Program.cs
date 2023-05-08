using Microsoft.Extensions.Configuration;
using Mono.Unix.Native;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Principal;
using Terminal.Gui;

namespace top.net;

internal class Program
{
    internal static bool isWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#pragma warning disable CA1416
    internal static bool isWindowAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
    internal static bool isNixAdministrator => Syscall.geteuid() == 0;
    internal static bool isAdministrator => isWindows ? isWindowAdministrator : isNixAdministrator;

    internal static Dictionary<string, string> validArguments = new Dictionary<string, string>()
    {
        { "memory-units", "The units to use display memory usage.\rValues: KB, MB, KiB, MiB\rDefault: KiB"},
        { "version", "Displays the version in MAJOR.MINOR.BUILD format." },
        { "help", "Displays this help screen." },
        { "-m", "memory-units" },
        { "-v", "version" },
        { "-h", "help" },
    };

    internal enum MemoryUnits
    {
        KB = 1000,
        KiB = 1024,
        MB = 1000000,
        MiB = 1048576
    }

    internal class Settings
    {
        public bool help { get; set; }
        public bool version { get; set; }
        [DataMember(Name = "memory-units")]
        public MemoryUnits memoryUnits { get; set; } = MemoryUnits.KiB;
    }

    internal class ProcessInfo
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Executable { get; set; }
        public string Origin { get; set; }
        public TimeSpan ProcessorTime { get; set; }
    }

    internal class ProcessInfoList : IListDataSource
    {
        public ProcessInfoList() { }

        public List<ProcessInfo> Data { get; set; } = new List<ProcessInfo>();
        private Dictionary<int, bool> Mark = new Dictionary<int, bool>();

        public int Count => Data.Count;

        public int Length => Data.Count;

        public bool IsMarked(int item)
        {
            return (Mark.ContainsKey(item) ? Mark[item] : false);
        }

        public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
        {
            driver.AddStr(string.Format("{0,6:######} | {1} | {2}", Data[item].Id, Data[item].ProcessorTime, Data[item].Description));
        }

        public void SetMark(int item, bool value)
        {
            Mark[item] = value;
        }

        IList IListDataSource.ToList()
        {
            return (Data);
        }
    }

    internal static IConfiguration configurationRoot = null;
    internal static Settings settings = new Settings();

    internal static ProcessInfoList processInfoList = new ProcessInfoList();
    internal static List<ProcessInfo> oldProcessInfoList = new List<ProcessInfo>();

    internal static string getVersion => $"{Assembly.GetEntryAssembly().GetName().Version.Major}.{Assembly.GetEntryAssembly().GetName().Version.Minor}.{Assembly.GetEntryAssembly().GetName().Version.Build}";
    internal static string getName => $"{Assembly.GetEntryAssembly().GetName().Name.ToLower()}";

    static int Main(string[] args)
    {
        if (!isAdministrator)
        {
            Console.WriteLine($"{getName} v{getVersion}");
            Console.WriteLine("Run this application as administrator!");

            return (0);
        }

        try
        {
            configurationRoot = new ConfigurationBuilder()
                .AddCommandLineArgs(args, validArguments)
                .Build();
        }
        catch (InvalidCommandLineArgException ex)
        {
            Console.WriteLine($"The command-line parameter '{ex.argName}' is invalid...");

            return (-1);
        }

        ArgumentBinding.Bind(configurationRoot, settings);

        if (settings.help)
        {
            printHelp();
        }
        else if (settings.version)
        {
            Console.WriteLine(getVersion);
        }
        else
        {
            Application.Init();

            ListView listView = new ListView(processInfoList) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };

            loadProcessInfo();

            Application.Top.Add(listView);

            Task.Run(() =>
            {
                while (true)
                {
                    loadProcessInfo();

                    Application.MainLoop.Invoke(() => listView.SetNeedsDisplay());

                    Thread.Sleep(1000);
                }
            });

            Application.Run();
        }

        return (0);
    }

    static void printHelp()
    {
        Console.WriteLine($"{getName} v{getVersion}");
        Console.WriteLine("Usage:");

        foreach (KeyValuePair<string, string> argument in validArguments.ToImmutableSortedDictionary())
        {
            if (argument.Key.StartsWith("-"))
            {
                continue;
            }
            else
            {
                KeyValuePair<string, string>[] shorthandArgs = validArguments.Where(arg => (arg.Value != null && (arg.Value.ToLower() == argument.Key.ToLower()))).ToArray();

                Console.Write($"\t--{argument.Key}");

                foreach (KeyValuePair<string, string> shorthandArgument in shorthandArgs)
                {
                    Console.Write($", {shorthandArgument.Key}");
                }

                Console.WriteLine();

                string[] lines = argument.Value.Split("\r");

                foreach (string line in lines)
                {
                    Console.WriteLine($"\t\t\t{line}");
                }
            }
        }
    }

    static void loadProcessInfo()
    {
        processInfoList.Data.Clear();

        Process[] processes = Process.GetProcesses();

        foreach (Process process in processes)
        {
            if ((process.Id != 0) && !process.HasExited)
            {
                Exception exception = null;
                ProcessModule mainModule = null;

                try { mainModule = process.MainModule; } catch (Exception ex) { exception = ex; };

                ProcessInfo oldProcess = oldProcessInfoList.SingleOrDefault(p => p.Id == process.Id);

                if ((oldProcess == null) || ((oldProcess != null) && (process.TotalProcessorTime.CompareTo(oldProcess.ProcessorTime) != 0)))
                {
                    processInfoList.Data.Add(new ProcessInfo()
                    {
                        Id = process.Id,
                        Executable = mainModule == null ? process.ProcessName : Path.GetFileNameWithoutExtension(mainModule.FileName),
                        Origin = mainModule == null ? null : Path.GetFullPath(mainModule.FileName),
                        Description = mainModule == null ? $"({exception.Message})" : mainModule.FileVersionInfo.FileDescription,
                        ProcessorTime = process.TotalProcessorTime,
                    });
                }
            }
        }

        oldProcessInfoList.Clear();
        oldProcessInfoList.AddRange(processInfoList.Data);
    }
}
