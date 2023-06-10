using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Text.Json;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static readonly Argument<string[]> ExecArgsArgument = new("args", "the process arguments to execute");

	private static async Task HandleExecCommand(InvocationContext context)
	{
		var logger = GetLogger(context);

		if (!File.Exists(LighthouseConfigurationFilePath))
		{
			logger.Error(
				"lighthouse config file does not exist: {ConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
			logger.Error("There are no registered lighthouses! Run `lh2mgr register <mac addresses>` to add them");
			context.ExitCode = 2;
			return;
		}

		var json = await File.ReadAllTextAsync(LighthouseConfigurationFilePath);
		LighthouseConfiguration? lighthouseConfiguration = default;
		try
		{
			if (!string.IsNullOrWhiteSpace(json))
			{
				lighthouseConfiguration = JsonSerializer.Deserialize<LighthouseConfiguration>(json);
			}
		}
		catch (Exception exception)
		{
			logger.Warning(
				"lighthouse config file is not valid JSON: {ConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
			logger.Warning(
				exception,
				"Failed to deserialize existing configuration: {ConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
		}

		if (lighthouseConfiguration?.Lighthouses == default)
		{
			logger.Error(
				"lighthouse config file is missing the `Lighthouses` property or it is not a valid array: {ConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
			logger.Error("There are no registered lighthouses! Run `lh2mgr register <mac addresses>` to add them");
			context.ExitCode = 3;
			return;
		}

		if (lighthouseConfiguration.Lighthouses.Length < 1)
		{
			logger.Error("There are no registered lighthouses! Run `lh2mgr register <mac addresses>` to add them");
			context.ExitCode = 4;
			return;
		}

		if (await SetPowerState(logger, PowerState.On, lighthouseConfiguration.Lighthouses))
		{
			logger.Information(
				"Successfully turned on the lighthouses: {LighthouseMacAddresses}",
				string.Join(", ", lighthouseConfiguration.Lighthouses)
			);
		}
		else
		{
			logger.Error(
				"Failed to turn on the lighthouses: {LighthouseMacAddresses}",
				string.Join(", ", lighthouseConfiguration.Lighthouses)
			);
			Environment.Exit(5);
			return;
		}

		var execArgs = context.ParseResult.GetValueForArgument(ExecArgsArgument);
		var execString = string.Join(' ', execArgs);
		logger.Information("Executing '{ExecString}'", execString);
		var programName = execArgs.First();
		var programArgs = execArgs.Skip(1).ToArray();
		var process = Process.Start(programName, programArgs);
		await process.WaitForExitAsync();

		if (await SetPowerState(logger, PowerState.Off, lighthouseConfiguration.Lighthouses))
		{
			logger.Information(
				"Successfully turned off the lighthouses: {LighthouseMacAddresses}",
				string.Join(", ", lighthouseConfiguration.Lighthouses)
			);
		}
		else
		{
			logger.Error(
				"Failed to turn on the lighthouses: {LighthouseMacAddresses}",
				string.Join(", ", lighthouseConfiguration.Lighthouses)
			);
			Environment.Exit(6);
		}
	}
}
