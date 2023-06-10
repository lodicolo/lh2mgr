using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Serilog;

namespace ValveIndex.lh2mgr;

internal static partial class Program
{
	private static async Task<GattCharacteristic?> GetPowerCharacteristic(
		ILogger logger,
		IDevice1 device,
		string deviceMacAddress
	)
	{
		const string gattServiceUuid = "00001523-1212-efde-1523-785feabcd124";
		logger.Verbose(
			"Getting the GATT service ({GattServiceUuid}) of {DeviceMacAddress}...",
			gattServiceUuid,
			deviceMacAddress
		);
		var gattService = await device.GetServiceAsync(gattServiceUuid);
		if (gattService == default)
		{
			logger.Error(
				"No GATT service ({GattServiceUuid}) found for {DeviceMacAddress}!",
				gattServiceUuid,
				deviceMacAddress
			);
			var services = await device.GetServicesAsync();
			if (services?.Count is null or < 1)
			{
				logger.Error("No services found for {DeviceMacAddress}", deviceMacAddress);
			}
			else
			{
				logger.Information(
					"Found {ServicesCount} services for {DeviceMacAddress}",
					services.Count,
					deviceMacAddress
				);
				foreach (var service in services)
				{
					logger.Information("Found service: {ServiceObjectPath}", service.ObjectPath);
				}
			}

			return default;
		}

		const string powerCharacteristicUuid = "00001525-1212-efde-1523-785feabcd124";
		logger.Verbose(
			"Getting the power characteristic ({PowerCharacteristicUuid}) of {DeviceMacAddress}...",
			powerCharacteristicUuid,
			deviceMacAddress
		);
		var characteristic = await gattService.GetCharacteristicAsync(powerCharacteristicUuid);
		return characteristic;
	}

	private static async Task<IDevice1[]?> GetDevices(ILogger logger, params string[] macAddresses)
	{
		var adapters = await BlueZManager.GetAdaptersAsync();
		if (adapters == default || adapters.Count < 1)
		{
			logger.Error("No bluetooth adapters!");
			return Array.Empty<IDevice1>();
		}

		var adapter = adapters[0];

		var suffixes = macAddresses.Select(address => address.Replace(':', '_')).ToList();
		TaskCompletionSource<IDevice1[]> getDevicesTask = new();

		List<IDevice1> devices = new();

		adapter.DeviceFound += (_, deviceFoundEvent) =>
		{
			var device = deviceFoundEvent.Device;
			var objectPath = device.ObjectPath.ToString();
			var matchingSuffix = suffixes.FirstOrDefault(suffix => objectPath.EndsWith(suffix));

			if (string.IsNullOrWhiteSpace(matchingSuffix))
			{
				logger.Verbose(
					"Device {DeviceObjectPath} is not one of the specified lighthouses, skipping",
					objectPath
				);
				return Task.CompletedTask;
			}

			device.Connected += (sender, _) =>
			{
				logger.Verbose("Device {DeviceObjectPath} connected", sender.ObjectPath);
				return Task.CompletedTask;
			};

			device.Disconnected += (sender, _) =>
			{
				logger.Verbose("Device {DeviceObjectPath} disconnected", sender.ObjectPath);
				return Task.CompletedTask;
			};

			devices.Add(device);
			logger.Verbose("Found device {DeviceObjectPath}", device.ObjectPath);

			// ReSharper disable once InvertIf
			if (devices.Count == macAddresses.Length)
			{
				logger.Verbose("Finished finding all devices");
				getDevicesTask.TrySetResult(devices.ToArray());
			}

			return Task.CompletedTask;
		};

		logger.Verbose("Starting device discovery...");
		await adapter.StartDiscoveryAsync();

		try
		{
			logger.Verbose("Waiting to find all devices this will timeout after 30 seconds...");
			var discoveredDevices = await getDevicesTask.Task.WaitAsync(TimeSpan.FromSeconds(30));
			return discoveredDevices;
		}
		catch (TimeoutException exception)
		{
			logger.Verbose(exception, "Timed out waiting for connection to all devices");
			return default;
		}
		finally
		{
			logger.Verbose("Stopping device discovery...");
			await adapter.StopDiscoveryAsync();
		}
	}

	private static async Task<bool> SetPowerState(ILogger logger, PowerState powerState, params string[] macAddresses)
	{
		var devices = await GetDevices(logger, macAddresses);
		if (devices == default)
		{
			logger.Error("No devices found, unable to set power state!");
			return false;
		}

		logger.Verbose("Connecting to discovered devices...");
		foreach (var device in devices)
		{
			try
			{
				TaskCompletionSource serviceResolutionTaskSource = new();

				if (device is Device concreteDevice)
				{
					concreteDevice.ServicesResolved += (sender, _) =>
					{
						logger.Verbose("Services resolved for device {DeviceObjectPath}", sender.ObjectPath);
						serviceResolutionTaskSource.TrySetResult();
						return Task.CompletedTask;
					};
				}
				else
				{
					serviceResolutionTaskSource.TrySetResult();
				}

				logger.Verbose("Connecting to {DeviceObjectPath}...", device.ObjectPath);
				await device.ConnectAsync();

				logger.Verbose(
					"Waiting for services to be resolved for device ({DeviceObjectPath}), this will time out after 10 seconds...",
					device.ObjectPath
				);
				await serviceResolutionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(10));
			}
			catch (TimeoutException timeoutException)
			{
				logger.Error(
					timeoutException,
					"Failed to resolve services within 10 seconds for device ({DeviceObjectPath})",
					device.ObjectPath
				);
				try
				{
					await device.DisconnectAsync();
				}
				catch (Exception exception)
				{
					logger.Warning(exception, "Failed to disconnect from {DeviceObjectPath}", device.ObjectPath);
				}

				return false;
			}
			catch (Exception exception)
			{
				logger.Error(exception, "Failed to connect to device ({DeviceObjectPath})", device.ObjectPath);
				return false;
			}
		}

		logger.Verbose(
			"Changing power state to {PowerState} for the following lighthouses: {LighthouseMacAddresses}",
			powerState,
			string.Join(", ", macAddresses)
		);

		try
		{
			foreach (var device in devices)
			{
				var objectPath = device.ObjectPath;
				var deviceMacAddress = macAddresses.First(
					macAddress => objectPath.ToString().EndsWith(macAddress.Replace(':', '_'))
				);
				var characteristicValue = powerState.GetCharacteristicValue();
				Dictionary<string, object> options = new(0);

				logger.Verbose("Getting the power characteristic of {DeviceMacAddress}...", deviceMacAddress);
				var powerCharacteristic = await GetPowerCharacteristic(logger, device, deviceMacAddress);

				if (powerCharacteristic == default)
				{
					logger.Error(
						"No power state characteristic for {DeviceMacAddress} ({DeviceObjectPath})",
						deviceMacAddress,
						objectPath
					);
					return false;
				}

				logger.Verbose(
					"Writing to power characteristic of {DeviceMacAddress} to {PowerState}...",
					deviceMacAddress,
					powerState
				);
				await powerCharacteristic.WriteValueAsync(characteristicValue, options);
			}

			logger.Verbose("Finished setting the power states of all devices");
			return true;
		}
		finally
		{
			foreach (var device in devices)
			{
				try
				{
					await device.DisconnectAsync();
				}
				catch (Exception exception)
				{
					logger.Warning(exception, "Failed to disconnect from {DeviceObjectPath}", device.ObjectPath);
				}
			}
		}
	}
}
