using System.Collections.Generic;
using System.Text;

namespace Microsoft.ActiveDirectory.WebServices;

public class DirectoryAttribute
{
	private string name;

	private DirectorySyntaxType syntax;

	private string xmlns;

	private IList<DirectoryValue> attrValues;

	public string Name
	{
		get
		{
			return name;
		}
		set
		{
			name = value;
		}
	}

	internal DirectorySyntaxType Syntax
	{
		get
		{
			return syntax;
		}
		set
		{
			syntax = value;
		}
	}

	public string Xmlns
	{
		get
		{
			return xmlns;
		}
		set
		{
			xmlns = value;
		}
	}

	internal IList<DirectoryValue> AttrValues
	{
		get
		{
			return attrValues;
		}
		private set
		{
			attrValues = value;
		}
	}

	internal DirectoryAttribute()
	{
		attrValues = new List<DirectoryValue>();
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<Attribute>\n");
		if (name != null)
		{
			stringBuilder.Append("<Name>" + name + "</Name>\n");
		}
		stringBuilder.Append("<Syntax>" + syntax.ToString() + "</Syntax>\n");
		if (xmlns != null)
		{
			stringBuilder.Append("<Xmlns>" + xmlns + "</Xmlns>\n");
		}
		foreach (DirectoryValue attrValue in attrValues)
		{
			stringBuilder.Append(attrValue.ToString());
		}
		stringBuilder.Append("</Attribute>\n");
		return stringBuilder.ToString();
	}
}
