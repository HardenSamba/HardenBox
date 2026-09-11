using System.Collections.Generic;
using System.Text;

namespace Microsoft.ActiveDirectory.WebServices;

public class DirectoryObjectFragment : IDirectoryObject
{
	private DirectoryObjectIdentifier objectIdentity;

	private DirectoryObjectIdentifier parentIdentity;

	private string distinguishedName;

	private string rDN;

	private string objectClass;

	private IList<DirectoryAttribute> attributes;

	public DirectoryObjectIdentifier ObjectIdentity
	{
		get
		{
			return objectIdentity;
		}
		set
		{
			objectIdentity = value;
			CheckIdentitiesEqual();
		}
	}

	public DirectoryObjectIdentifier ParentIdentity
	{
		get
		{
			return parentIdentity;
		}
		set
		{
			parentIdentity = value;
			CheckIdentitiesEqual();
		}
	}

	public string DistinguishedName
	{
		get
		{
			return distinguishedName;
		}
		set
		{
			distinguishedName = value;
		}
	}

	public string RDN
	{
		get
		{
			return rDN;
		}
		set
		{
			rDN = value;
		}
	}

	public string ObjectClass
	{
		get
		{
			return objectClass;
		}
		set
		{
			objectClass = value;
		}
	}

	public IList<DirectoryAttribute> Attributes
	{
		get
		{
			return attributes;
		}
		private set
		{
			attributes = value;
		}
	}

	public DirectoryObjectFragment()
	{
		attributes = new List<DirectoryAttribute>();
	}

	private void CheckIdentitiesEqual()
	{
		if (parentIdentity != null)
		{
			_ = objectIdentity;
		}
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<DirectoryObjectFragment>\n<ObjectIdentity>\n");
		if (ObjectIdentity != null)
		{
			stringBuilder.Append(ObjectIdentity.ToString());
		}
		stringBuilder.Append("</ObjectIdentity>\n<ParentIdentity>\n");
		if (ParentIdentity != null)
		{
			stringBuilder.Append(ParentIdentity.ToString());
		}
		stringBuilder.Append("</ParentIdentity>\n<DistinguishedName>");
		if (distinguishedName != null)
		{
			stringBuilder.Append(distinguishedName);
		}
		stringBuilder.Append("</DistinguishedName>\n<RDN>");
		if (rDN != null)
		{
			stringBuilder.Append(rDN);
		}
		stringBuilder.Append("</RDN>\n<ObjectClass>");
		if (objectClass != null)
		{
			stringBuilder.Append(objectClass);
		}
		stringBuilder.Append("</ObjectClass>\n");
		foreach (DirectoryAttribute attribute in attributes)
		{
			stringBuilder.Append(attribute.ToString());
		}
		stringBuilder.Append("</DirectoryObjectFragment>\n");
		return stringBuilder.ToString();
	}
}
