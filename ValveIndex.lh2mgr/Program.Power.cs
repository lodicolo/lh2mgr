using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static readonly Argument<PowerState> PowerCommandPowerStateArgument = new(
		"power-state",
		"one of \"on\", \"off\""
	);

	private static readonly Argument<string[]> PowerCommandMacAddressesArgument = new(
		"mac-addresses",
		"one or more MAC addresses corresponding to the lighthouse(s) that should have their power state changed"
	);

	private static async Task HandlePowerCommand(InvocationContext context)
	{
		var logger = GetLogger(context);

		var powerState = context.ParseResult.GetValueForArgument(PowerCommandPowerStateArgument);

		var macAddresses = context.ParseResult.GetValueForArgument(PowerCommandMacAddressesArgument);
		if (macAddresses.Length < 1)
		{
			try
			{
				if (!File.Exists(LighthouseConfigurationFilePath))
				{
					logger.Warning(
						"lighthouse config file does not exist: {ConfigurationFilePath}",
						LighthouseConfigurationFilePath
					);
					logger.Error(
						"No MAC addresses were specified and there are no registered lighthouses! Either provide at least one MAC Address (`lh2mgr power {State} <MAC addresses>`) or run `lh2mgr register <mac addresses>` to add them before running this command",
						powerState.ToString().ToLowerInvariant()
					);
					context.ExitCode = 81;
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
					logger.Error(
						exception,
						"Failed to deserialize existing configuration: {ConfigurationFilePath}",
						LighthouseConfigurationFilePath
					);
					logger.Error(
						"No MAC addresses were specified and the configuration is corrupt, run `lh2mgr register <mac addresses>` to re-create the configuration"
					);
					context.ExitCode = 82;
					return;
				}

				if (lighthouseConfiguration?.Lighthouses == default)
				{
					logger.Error(
						"lighthouse config file is missing the `Lighthouses` property or it is not a valid array: {ConfigurationFilePath}",
						LighthouseConfigurationFilePath
					);
					logger.Error(
						"No MAC addresses were specified and there are no registered lighthouses! Run `lh2mgr register <mac addresses>` to add them"
					);
					context.ExitCode = 83;
					return;
				}

				if (lighthouseConfiguration.Lighthouses.Length < 1)
				{
					logger.Error(
						"Expected at least one MAC address, or that there is at least one registered lighthouse! Run `lh2mgr register <mac addresses>` to add lighthouses"
					);
					context.ExitCode = 84;
					return;
				}

				macAddresses = lighthouseConfiguration.Lighthouses;
			}
			catch (Exception exception)
			{
				logger.Error(exception, "Expected one or more MAC addresses");
				context.ExitCode = 80;
				return;
			}
		}

		try
		{
			logger.Information(
				"Setting the power state of {LighthouseMacAddresses} to {PowerState}",
				string.Join(", ", macAddresses),
				powerState
			);

			if (!await SetPowerState(logger, powerState, macAddresses))
			{
				logger.Error(
					"Failed to set power state for the following lighthouses: {LighthouseMacAddresses}",
					string.Join(", ", macAddresses)
				);
				context.ExitCode = 91;
			}
		}
		catch (Exception exception)
		{
			logger.Error(
				exception,
				"Failed to set power state for the following lighthouses: {LighthouseMacAddresses}",
				string.Join(", ", macAddresses)
			);
			context.ExitCode = 90;
		}
	}
}
