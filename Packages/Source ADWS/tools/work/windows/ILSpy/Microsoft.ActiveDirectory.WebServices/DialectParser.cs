using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices;

public abstract class DialectParser
{
	internal static DialectParser Create(string dialect)
	{
		if (dialect == null)
		{
			throw new ArgumentNullException("dialect");
		}
		if (dialect.Equals(AdwsConstants.XPath10SubsetQuery, StringComparison.InvariantCultureIgnoreCase) || dialect.Equals(AdwsConstants.QNameSelection, StringComparison.InvariantCultureIgnoreCase))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DialectParser", "Create: unsupported dialect={0} returning null", dialect);
			return null;
		}
		if (dialect.Equals(AdwsConstants.LdapQuery, StringComparison.InvariantCultureIgnoreCase))
		{
			LdapQueryParser result = new LdapQueryParser();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "Create: dialect={0} returning LdapQueryParser", dialect);
			return result;
		}
		if (dialect.Equals(AdwsConstants.XPathLvl1Selection, StringComparison.InvariantCultureIgnoreCase))
		{
			XPathLvl1SelectionParser result2 = new XPathLvl1SelectionParser();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "Create: dialect={0} returning XPathLvl1SelectionParser", dialect);
			return result2;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "Create: unknown dialect={0}, returning null", dialect);
		return null;
	}

	internal static string[] GetSupportedDialects(OperationType operation)
	{
		string[] result = null;
		switch (operation)
		{
		case OperationType.Create:
		case OperationType.Put:
		case OperationType.Get:
		case OperationType.EnumerateSelection:
		case OperationType.EnumerateSort:
			result = new string[1] { AdwsConstants.XPathLvl1Selection };
			break;
		case OperationType.EnumerateFilter:
			result = new string[1] { AdwsConstants.LdapQuery };
			break;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "GetSupportedDialects: operation={0}", operation.ToString());
		return result;
	}

	internal static bool IsDialectSupported(OperationType operation, string dialect)
	{
		bool flag = false;
		string[] array = null;
		if (dialect == null || dialect.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "IsDialectSupported: null or zero-len dialect");
			return false;
		}
		array = GetSupportedDialects(operation);
		if (array != null)
		{
			string[] array2 = array;
			foreach (string text in array2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "IsDialectSupported: we support {0}", text);
				if (text.Equals(dialect, StringComparison.InvariantCultureIgnoreCase))
				{
					flag = true;
					break;
				}
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DialectParser", "IsDialectSupported: operation={0} dialect={1} returning {2}", operation.ToString(), dialect, flag);
		return flag;
	}

	internal virtual DirectoryObject ParseCreate(InstanceType instance, AttributeTypeAndValueType[] attrTypeAndValueArray)
	{
		throw new NotImplementedException(AdwsStrings.DialectClassBaseMethodNotImplemented);
	}

	internal virtual IList<DirectoryAttribute> ParseGet(InstanceType instance, AttributeType[] attrTypeArray)
	{
		throw new NotImplementedException(AdwsStrings.DialectClassBaseMethodNotImplemented);
	}

	internal virtual IList<DirectoryAttributeOperation> ParsePut(InstanceType instance, ChangeType[] changeArray)
	{
		throw new NotImplementedException(AdwsStrings.DialectClassBaseMethodNotImplemented);
	}

	internal virtual IList<DirectoryAttribute> ParseSelectionOrSort(InstanceType instance, ExpressionBase[] selectionOrSortArray)
	{
		throw new NotImplementedException(AdwsStrings.DialectClassBaseMethodNotImplemented);
	}

	internal virtual QueryFilter ParseEnumerate(XmlNode filter)
	{
		throw new NotImplementedException(AdwsStrings.DialectClassBaseMethodNotImplemented);
	}
}
