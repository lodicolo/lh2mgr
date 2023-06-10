using System.CommandLine;
using Serilog;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static readonly string ValveIndexDirectoryPath = Path.Join(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"net.pandacoder.ValveIndex"
	);

	private static readonly string LogsDirectoryPath = Path.Join(ValveIndexDirectoryPath, "logs");

	private static readonly string LoggerFilePath = Path.Join(
		LogsDirectoryPath,
		$"lh2mgr.{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log"
	);

	private static readonly string LighthouseConfigurationFilePath = Path.Join(ValveIndexDirectoryPath, "lh2mgr.json");

	private static readonly Option<bool> VerboseOption =
		new(new[] { "-v", "--verbose" }, "Sets the output log level to verbose")
		{
			IsRequired = false
		};

	public static async Task Main(string[] args)
	{
		var bootstrapLoggerConfiguration = new LoggerConfiguration().Enrich.FromLogContext()
			.MinimumLevel.Verbose()
			.WriteTo.Console()
			.WriteTo.File(LoggerFilePath);
		await using var bootstrapLogger = bootstrapLoggerConfiguration.CreateLogger();
		bootstrapLogger.Information("Received {Args}", string.Join(' ', args));

		var execCommand = new Command(
			"exec",
			"runs another process, turning on the registered lighthouse(s) before starting the other process, and then turning off the lighthouse(s) after the process stops"
		);
		execCommand.AddArgument(ExecArgsArgument);
		execCommand.SetHandler(HandleExecCommand);

		var powerCommand = new Command("power", "manually control the power state of the lighthouse(s)");
		powerCommand.AddArgument(PowerCommandPowerStateArgument);
		powerCommand.AddArgument(PowerCommandMacAddressesArgument);
		powerCommand.SetHandler(HandlePowerCommand);

		var registerCommand = new Command(
			"register",
			"register the MAC address(es) of the lighthouse(s) that should be automatically started/stopped"
		);
		registerCommand.AddArgument(RegisterCommandMacAddressesArgument);
		registerCommand.SetHandler(HandleRegisterCommand);

		var rootCommand = new RootCommand("Lighthouse control utility written in .NET");
		rootCommand.AddCommand(execCommand);
		rootCommand.AddCommand(powerCommand);
		rootCommand.AddCommand(registerCommand);
		rootCommand.AddGlobalOption(VerboseOption);
		Environment.ExitCode = await rootCommand.InvokeAsync(args);
		bootstrapLogger.Information("Exiting with code {ExitCode}...", Environment.ExitCode);
	}
}
