namespace ValveIndex.lh2mgr;

public enum PowerState
{
	Off,
	On
}

public static class PowerStateExtensions
{
	public static byte[] GetCharacteristicValue(this PowerState powerState)
	{
		return new[] { (byte)powerState };
	}
}
