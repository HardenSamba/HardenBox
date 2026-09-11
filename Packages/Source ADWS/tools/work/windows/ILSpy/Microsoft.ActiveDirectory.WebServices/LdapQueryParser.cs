using System;
using System.DirectoryServices.Protocols;
using System.Xml;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapQueryParser : DialectParser
{
	internal override QueryFilter ParseEnumerate(XmlNode filter)
	{
		if (filter == null)
		{
			throw new ArgumentNullException("filter");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQueryParser", "ParseEnumerate: entered");
		if (!filter.LocalName.Equals("LdapQuery", StringComparison.InvariantCultureIgnoreCase))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryParser", "ParseEnumerate node not an LdapQuery express node");
			throw new ArgumentException(AdwsStrings.UnknownXmlNode);
		}
		if (!filter.NamespaceURI.Equals(AdwsConstants.LdapQuery, StringComparison.InvariantCultureIgnoreCase))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryParser", "ParseNumerate: LdapQuery node not in adlq namespace");
			throw new ArgumentException(AdwsStrings.ObjectInWrongNamespace);
		}
		XmlNode xmlNode = null;
		XmlNode xmlNode2 = null;
		XmlNode xmlNode3 = null;
		foreach (XmlNode childNode in filter.ChildNodes)
		{
			if (childNode is XmlText || childNode is XmlComment)
			{
				continue;
			}
			if (!childNode.LocalName.Equals("Filter", StringComparison.InvariantCultureIgnoreCase) && !childNode.LocalName.Equals("BaseObject", StringComparison.InvariantCultureIgnoreCase) && !childNode.LocalName.Equals("Scope", StringComparison.InvariantCultureIgnoreCase))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryParser", "ParseEnumerate node [{0}] not a Filter, BaseObject or Scope expression node", childNode.LocalName);
				throw new ArgumentException(AdwsStrings.UnknownXmlNode);
			}
			if (!childNode.NamespaceURI.Equals(AdwsConstants.LdapQuery, StringComparison.InvariantCultureIgnoreCase))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryParser", "ParseEnumerate:child node not in adlq namespace");
				throw new ArgumentException(AdwsStrings.ObjectInWrongNamespace);
			}
			if (childNode.LocalName.Equals("Filter", StringComparison.InvariantCultureIgnoreCase))
			{
				if (xmlNode != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseEnumerate", "multiple filter nodes in filter");
					throw new ArgumentException(AdwsStrings.MissingOrMultipleFilterNodes);
				}
				xmlNode = childNode;
			}
			else if (childNode.LocalName.Equals("BaseObject", StringComparison.InvariantCultureIgnoreCase))
			{
				if (xmlNode2 != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseEnumerate", "multiple baseobject nodes in filter");
					throw new ArgumentException(AdwsStrings.MissingOrMultipleBaseObjectNodes);
				}
				xmlNode2 = childNode;
			}
			else if (childNode.LocalName.Equals("Scope", StringComparison.InvariantCultureIgnoreCase))
			{
				if (xmlNode3 != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseEnumerate", "multiple scope nodes in filter");
					throw new ArgumentException(AdwsStrings.MissingOrMultipleScopeNodes);
				}
				xmlNode3 = childNode;
			}
		}
		if (xmlNode3 == null || xmlNode2 == null || xmlNode == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseEnumerate", "missing scope, baseobject or filter node");
			throw new ArgumentException(AdwsStrings.MissingScopeOrBaseObjectOrFilterNode);
		}
		string text = xmlNode3.InnerText.Trim();
		string text2 = xmlNode.InnerText.Trim();
		string text3 = xmlNode2.InnerText.Trim();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ParseEnumerate", "filter={0}, scope={1}, baseObject={2}", text2, text, text3);
		SearchScope scope;
		if (text.Equals("onelevel", StringComparison.InvariantCultureIgnoreCase))
		{
			scope = SearchScope.OneLevel;
		}
		else if (text.Equals("subtree", StringComparison.InvariantCultureIgnoreCase))
		{
			scope = SearchScope.Subtree;
		}
		else
		{
			if (!text.Equals("base", StringComparison.InvariantCultureIgnoreCase))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseEnumerate", "scope node not 'onelevel' nor 'subtree' nor 'base'");
				throw new ArgumentException(AdwsStrings.ScopeNodeNotOnelevelNorSubtreeNorBase);
			}
			scope = SearchScope.Base;
		}
		Guid guid = Guid.Empty;
		if (text3.Length == 36 && AdwsConstants.guidRegex.IsMatch(text3))
		{
			try
			{
				guid = new Guid(text3);
			}
			catch (FormatException)
			{
			}
			catch (ArgumentException)
			{
			}
		}
		LdapQueryFilter ldapQueryFilter = null;
		ldapQueryFilter = ((!(guid == Guid.Empty)) ? new LdapQueryFilter(text2, scope, guid) : new LdapQueryFilter(text2, scope, text3));
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ParseEnumerate", "returning");
		return ldapQueryFilter;
	}
}
