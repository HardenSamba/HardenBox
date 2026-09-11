using System.Text;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DirectoryAttributeOperation : DirectoryAttribute
{
	private AttributeOperationType operation;

	internal AttributeOperationType Operation
	{
		get
		{
			return operation;
		}
		set
		{
			operation = value;
		}
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<AttributeOperation>\n");
		stringBuilder.Append(base.ToString());
		stringBuilder.Append("<Operation>" + Operation.ToString() + "</Operation>\n");
		stringBuilder.Append("</AttributeOperation>\n");
		return stringBuilder.ToString();
	}
}
