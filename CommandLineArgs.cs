using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System.Diagnostics;
using System.Reflection;

namespace top.net;

public class InvalidCommandLineArgException : Exception
{
    public string argName { get; private set; }

    public InvalidCommandLineArgException(string argName)
    {
        this.argName = argName;

        Debug.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name}: The argument key {argName} is invalid...");
    }
}

internal static class CommandLineArgs
{
    public static IConfigurationBuilder AddCommandLineArgs(this IConfigurationBuilder configurationBuilder, IEnumerable<string> args, IDictionary<string, string> arguments = null)
    {
        configurationBuilder.Add(new CommandLineArgsConfigurationSource()
        {
            args = args,
            arguments = arguments
        });

        return configurationBuilder;
    }
}

internal class CommandLineArgsConfigurationSource : IConfigurationSource
{
    public IEnumerable<string> args { get; set; }
    public IDictionary<string, string> arguments { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new CommandLineArgsConfigurationProvider()
        {
            args = args,
            arguments = arguments
        };
    }
}

internal class CommandLineArgsConfigurationProvider : ConfigurationProvider
{
    public IEnumerable<string> args { get; set; }
    public IDictionary<string, string> arguments { get; set; }

    public override void Load()
    {
        EnvironmentVariablesConfigurationProvider environment = new EnvironmentVariablesConfigurationProvider();
        string assemblyName = $"{Assembly.GetEntryAssembly().GetName().Name.ToLower()}";

        environment.Load();

        Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if ((args != null) && (args.Count() > 0))
        {
            IEnumerator<string> enumerator = args.GetEnumerator();

            while (enumerator.MoveNext())
            {
                string current = enumerator.Current;

                while (InterpretKey(current, out string key))
                {
                    string value = null;

                    if (key.Contains("="))
                    {
                        value = key.Substring(key.IndexOf('=') + 1).ToLower();

                        key = key.Substring(0, key.Length - value.Length - 1);
                    }

                    data[key] = value;

                    if (enumerator.MoveNext())
                    {
                        current = enumerator.Current;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        foreach (KeyValuePair<string, string> argument in arguments)
        {
            if (!argument.Key.StartsWith('-') && !data.ContainsKey(argument.Key))
            {
                if (environment.TryGet($"{assemblyName}_{argument.Key}", out string szEnvironmnet))
                {
                    data[argument.Key] = szEnvironmnet.ToLower();
                }
            }
        }

        Data = data;
    }

    private bool InterpretKey(string inKey, out string outKey)
    {
        outKey = inKey;

        if (inKey.StartsWith("--"))
        {
            outKey = inKey.Substring(2);

            string[] split = outKey.Split('=');

            if (!arguments.ContainsKey(split[0]))
            {
                throw new InvalidCommandLineArgException(inKey);
            }
        }
        else if (inKey.StartsWith("-"))
        {
            string[] split = outKey.Split('=');

            if (arguments.ContainsKey(split[0]) && arguments.ContainsKey(arguments[split[0]]))
            {
                outKey = arguments[split[0]] + "=" + string.Join("=", split, 1, split.Length - 1);
            }
            else
            {
                throw new InvalidCommandLineArgException(inKey);
            }
        }
        else
        {
            throw new InvalidCommandLineArgException(inKey);
        }

        return (true);
    }
}