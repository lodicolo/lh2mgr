using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static readonly Argument<string[]> RegisterCommandMacAddressesArgument = new(
		"mac-addresses",
		"one or more MAC addresses corresponding to the lighthouse(s) that should have their power state changed automatically"
	);

	private static void HandleRegisterCommand(InvocationContext context)
	{
		var logger = GetLogger(context);

		LighthouseConfiguration? lighthouseConfiguration = default;
		string? json = default;
		try
		{
			if (File.Exists(LighthouseConfigurationFilePath))
			{
				json = File.ReadAllText(LighthouseConfigurationFilePath);
			}
		}
		catch (Exception exception)
		{
			logger.Warning(
				exception,
				"Failed to read lighthouse configuration: {LighthouseConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
		}

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
				exception,
				"Failed to deserialize lighthouse configuration: {LighthouseConfigurationJson}",
				json
			);
		}

		var macAddresses = context.ParseResult.GetValueForArgument(RegisterCommandMacAddressesArgument);
		var existingMacAddresses = lighthouseConfiguration?.Lighthouses ?? Array.Empty<string>();
		lighthouseConfiguration =
			new LighthouseConfiguration(existingMacAddresses.Concat(macAddresses).Distinct().ToArray());
		var updatedJson = JsonSerializer.Serialize(lighthouseConfiguration);

		try
		{
			if (!Directory.Exists(ValveIndexDirectoryPath))
			{
				Directory.CreateDirectory(ValveIndexDirectoryPath);
			}
		}
		catch (Exception exception)
		{
			logger.Error(
				exception,
				"Failed to create lighthouse configuration directory: {LighthouseConfigurationDirectoryPath}",
				ValveIndexDirectoryPath
			);
			context.ExitCode = 10;
			return;
		}

		try
		{
			File.WriteAllText(LighthouseConfigurationFilePath, updatedJson);
		}
		catch (Exception exception)
		{
			logger.Error(
				exception,
				"Failed to write lighthouse configuration: {LighthouseConfigurationFilePath}",
				LighthouseConfigurationFilePath
			);
			context.ExitCode = 11;
		}
	}
}
