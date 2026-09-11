namespace Microsoft.ActiveDirectory.WebServices;

internal class InstanceType
{
	private string instanceName;

	private int port;

	private int sslPort;

	private DirectoryType dirType;

	internal string InstanceName => instanceName;

	internal int Port => port;

	internal int SslPort => sslPort;

	internal DirectoryType DirectoryType => dirType;

	internal InstanceType(string InstanceName, int Port, int Sslport)
	{
		instanceName = InstanceName;
		port = Port;
		sslPort = Sslport;
	}

	internal InstanceType(string InstanceName, int Port, int Sslport, DirectoryType DirType)
		: this(InstanceName, Port, Sslport)
	{
		dirType = DirType;
	}

	public override bool Equals(object o)
	{
		if (!(o is InstanceType instanceType))
		{
			return false;
		}
		if (instanceName == instanceType.instanceName && port == instanceType.port && sslPort == instanceType.sslPort)
		{
			return true;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return instanceName.GetHashCode() ^ port ^ sslPort;
	}

	public override string ToString()
	{
		return "<InstanceName>" + instanceName + "</InstanceName>\n<Port>" + port + "</Port>\n<SslPort>" + sslPort + "</SslPort>\n<DirectoryType>" + dirType.ToString() + "</DirectoryType>\n";
	}
}
