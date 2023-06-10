using System.CommandLine.Invocation;
using Serilog;
using Serilog.Events;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static ILogger GetLogger(InvocationContext invocationContext)
	{
		var verbose = invocationContext.ParseResult.GetValueForOption(VerboseOption);
		var loggerConfiguration = new LoggerConfiguration().MinimumLevel
			.Is(verbose ? LogEventLevel.Verbose : LogEventLevel.Information)
			.WriteTo.Console()
			.WriteTo.File(LoggerFilePath);
		return loggerConfiguration.CreateLogger();
	}
}
