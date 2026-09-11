using System;

namespace Microsoft.ActiveDirectory.WebServices;

public class DirectoryObjectIdentifier
{
	private Guid objectReferenceProperty = Guid.Empty;

	private string objectDn;

	private InstanceType instance;

	internal Guid ObjectReferenceProperty
	{
		get
		{
			return objectReferenceProperty;
		}
		set
		{
			objectReferenceProperty = value;
		}
	}

	internal string ObjectDn
	{
		get
		{
			return objectDn;
		}
		set
		{
			objectDn = value;
		}
	}

	internal InstanceType Instance
	{
		get
		{
			return instance;
		}
		set
		{
			instance = value;
		}
	}

	internal DirectoryObjectIdentifier()
		: this(Guid.Empty, null)
	{
	}

	internal DirectoryObjectIdentifier(Guid objectReferenceProperty, InstanceType instance)
	{
		this.objectReferenceProperty = objectReferenceProperty;
		this.instance = instance;
	}

	internal DirectoryObjectIdentifier(string objectDn, InstanceType instance)
	{
		this.objectDn = objectDn;
		this.instance = instance;
	}

	internal bool IsObjectIdentifiedByDn()
	{
		if (!objectReferenceProperty.Equals(Guid.Empty) || objectDn == null)
		{
			return false;
		}
		return true;
	}

	internal string GetObjectRefOrDnAsString()
	{
		if (!IsObjectIdentifiedByDn())
		{
			return objectReferenceProperty.ToString();
		}
		return objectDn;
	}

	public override string ToString()
	{
		if (!IsObjectIdentifiedByDn())
		{
			Guid guid = objectReferenceProperty;
			return "<ObjectReferenceProperty>" + guid.ToString() + "</ObjectReferenceProperty>\n" + Instance.ToString();
		}
		return "<ObjectDN>" + objectDn + "</ObjectDN>\n" + Instance.ToString();
	}
}
