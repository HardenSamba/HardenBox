using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

[Serializable]
[XmlType(TypeName = "FaultDetail", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
[XmlRoot(ElementName = "FaultDetail", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
public class Fault
{
	private ArgumentErrorDetail argErrEx;

	private string strEx;

	private string shortStrEx;

	private DirectoryErrorDetail dirErrEx;

	private string invalidAttributeType;

	private string invalidOperation;

	private ChangeType invalidChange;

	private AttributeTypeAndValueType invalidAttributeTypeOrValue;

	private string unknownAttribute;

	public ArgumentErrorDetail ArgumentError
	{
		get
		{
			return argErrEx;
		}
		set
		{
			argErrEx = value;
		}
	}

	public string Error
	{
		get
		{
			return strEx;
		}
		set
		{
			strEx = value;
		}
	}

	public DirectoryErrorDetail DirectoryError
	{
		get
		{
			return dirErrEx;
		}
		set
		{
			dirErrEx = value;
		}
	}

	public string InvalidAttributeType
	{
		get
		{
			return invalidAttributeType;
		}
		set
		{
			invalidAttributeType = value;
		}
	}

	public string InvalidOperation
	{
		get
		{
			return invalidOperation;
		}
		set
		{
			invalidOperation = value;
		}
	}

	public ChangeType InvalidChange
	{
		get
		{
			return invalidChange;
		}
		set
		{
			invalidChange = value;
		}
	}

	public AttributeTypeAndValueType InvalidAttributeTypeOrValue
	{
		get
		{
			return invalidAttributeTypeOrValue;
		}
		set
		{
			invalidAttributeTypeOrValue = value;
		}
	}

	public string ShortError
	{
		get
		{
			return shortStrEx;
		}
		set
		{
			shortStrEx = value;
		}
	}

	public string UnknownAttribute
	{
		get
		{
			return unknownAttribute;
		}
		set
		{
			unknownAttribute = value;
		}
	}

	public Fault()
	{
		argErrEx = null;
		strEx = null;
		shortStrEx = null;
		dirErrEx = null;
		invalidAttributeType = null;
		invalidOperation = null;
		invalidChange = null;
		invalidAttributeTypeOrValue = null;
		unknownAttribute = null;
	}

	public Fault(ArgumentErrorDetail Ae, string Se, DirectoryErrorDetail De)
	{
		argErrEx = Ae;
		strEx = Se;
		if (Se != null && WebServiceStrings.LongToShortMap.TryGetValue(Se, out var value))
		{
			shortStrEx = value;
		}
		else
		{
			shortStrEx = WebServiceConstants.ArgumentException;
		}
		dirErrEx = De;
	}

	public Fault(AttributeTypeAndValueType attributeTypeAndValueType)
		: this()
	{
		invalidAttributeTypeOrValue = attributeTypeAndValueType;
	}

	public Fault(string expression, ChangeType change, string mode)
		: this()
	{
		invalidAttributeType = expression;
		invalidOperation = mode;
		invalidChange = change;
	}
}
