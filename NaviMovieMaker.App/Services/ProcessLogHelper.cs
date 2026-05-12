using System.Collections.ObjectModel;

namespace NaviMovieMaker.App.Services;

public static class ProcessLogHelper
{
    private static readonly HashSet<string> SensitiveOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--cookies",
        "--cookies-from-browser",
        "--add-header",
        "-H",
        "--username",
        "--password",
    };

    public static string FormatCommand(string executable, Collection<string> argumentList)
    {
        return FormatCommand(executable, argumentList.AsEnumerable());
    }

    public static string FormatCommand(string executable, IEnumerable<string> arguments)
    {
        return string.Join(" ", new[] { QuoteArgument(executable) }.Concat(FormatArguments(arguments)));
    }

    private static IEnumerable<string> FormatArguments(IEnumerable<string> arguments)
    {
        var maskNext = false;
        foreach (var argument in arguments)
        {
            if (maskNext)
            {
                yield return "***";
                maskNext = false;
                continue;
            }

            var equalsIndex = argument.IndexOf('=');
            if (equalsIndex > 0)
            {
                var option = argument[..equalsIndex];
                if (SensitiveOptions.Contains(option))
                {
                    yield return QuoteArgument(option);
                    yield return "***";
                    continue;
                }
            }

            if (SensitiveOptions.Contains(argument))
            {
                yield return QuoteArgument(argument);
                maskNext = true;
                continue;
            }

            yield return QuoteArgument(argument);
        }
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }
}
