using System;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Microsoft.ActiveDirectory.Shared.Utilities;

internal class CustomXPathContext : XsltContext
{
	private class CaseInsensitiveEqualsFunction : IXsltContextFunction
	{
		private XPathResultType[] m_argTypes = new XPathResultType[2]
		{
			XPathResultType.String,
			XPathResultType.String
		};

		public int Maxargs => 2;

		public int Minargs => 2;

		public XPathResultType[] ArgTypes => m_argTypes;

		public XPathResultType ReturnType => XPathResultType.Boolean;

		public object Invoke(XsltContext xsltContext, object[] args, XPathNavigator docContext)
		{
			_ = args.Length;
			_ = 2;
			string stringFromInvokeArgument = GetStringFromInvokeArgument(args[0]);
			string stringFromInvokeArgument2 = GetStringFromInvokeArgument(args[1]);
			return string.Equals(stringFromInvokeArgument, stringFromInvokeArgument2, StringComparison.OrdinalIgnoreCase);
		}

		private string GetStringFromInvokeArgument(object arg)
		{
			string result = string.Empty;
			if (arg is string)
			{
				result = (string)arg;
			}
			return result;
		}
	}

	public override bool Whitespace => true;

	public CustomXPathContext()
	{
	}

	public CustomXPathContext(NameTable table)
		: base(table)
	{
	}

	public override bool PreserveWhitespace(XPathNavigator node)
	{
		return Convert.ToBoolean(null);
	}

	public override int CompareDocument(string baseUri, string nextbaseUri)
	{
		return Convert.ToInt32(null);
	}

	public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] ArgTypes)
	{
		IXsltContextFunction result = null;
		if (string.Equals(LookupNamespace(prefix), "http://schemas.microsoft.com/2008/1/ActiveDirectory/XPathInternalExtensions", StringComparison.OrdinalIgnoreCase) && name == "nocaseequals" && ArgTypes.Length == 2 && ArgTypes[0] == XPathResultType.String && ArgTypes[1] == XPathResultType.String)
		{
			result = new CaseInsensitiveEqualsFunction();
		}
		return result;
	}

	public override IXsltContextVariable ResolveVariable(string prefix, string name)
	{
		return null;
	}
}
