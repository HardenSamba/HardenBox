using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlInclude(typeof(ChangeType))]
[XmlType(TypeName = "ChangeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
[XmlRoot(ElementName = "ChangeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class ChangeType : AttributeTypeAndValueType
{
	private string operationField;

	[XmlAttribute(AttributeName = "Operation")]
	public string Operation
	{
		get
		{
			return operationField;
		}
		set
		{
			operationField = value;
		}
	}

	public ChangeType()
	{
	}

	public ChangeType(string expression, string operation, IList<SingleValue> values)
		: base(expression, values)
	{
		operationField = operation;
	}
}
