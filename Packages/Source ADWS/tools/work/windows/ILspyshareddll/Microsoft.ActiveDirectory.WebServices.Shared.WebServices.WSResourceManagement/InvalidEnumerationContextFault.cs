using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSResourceManagement;

[Serializable]
[XmlType(TypeName = "InvalidEnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IncludeInSchema = false)]
[XmlRoot(ElementName = "InvalidEnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = true)]
public class InvalidEnumerationContextFault
{
}
