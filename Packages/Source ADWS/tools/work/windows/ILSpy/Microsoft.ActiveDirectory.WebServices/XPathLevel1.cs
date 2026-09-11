using System;
using System.Text.RegularExpressions;

namespace Microsoft.ActiveDirectory.WebServices;

public class XPathLevel1
{
	public static void ParseXPathLevel1String(string xPathLevel1Expression, out string objectName, out string attributeName, out string predicate)
	{
		objectName = string.Empty;
		attributeName = string.Empty;
		predicate = string.Empty;
		_ = string.Empty;
		if (xPathLevel1Expression == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseXPathLevel1String: null Expression");
			return;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseXPathLevel1String: entered xPathLevel1Expression={0}", xPathLevel1Expression);
		if (xPathLevel1Expression.StartsWith("///", StringComparison.Ordinal) || xPathLevel1Expression.ToLowerInvariant().Contains(";range="))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseXpathLevel1String", "Invalid expression {0}", xPathLevel1Expression);
			throw new InvalidExpressionException(AdwsStrings.InvalidExpression, InvalidExpressionDetailType.InvalidExpressionSyntax, xPathLevel1Expression);
		}
		xPathLevel1Expression = xPathLevel1Expression.Trim();
		if (xPathLevel1Expression.Contains("["))
		{
			predicate = xPathLevel1Expression.Substring(xPathLevel1Expression.IndexOf('['));
			xPathLevel1Expression = xPathLevel1Expression.Substring(0, xPathLevel1Expression.IndexOf('['));
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ParseXpathLevel1String", "Split off predicate={0}", predicate);
		}
		xPathLevel1Expression = xPathLevel1Expression.Trim();
		string[] array = xPathLevel1Expression.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		if (array == null || array.Length == 0 || array.Length > 2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseXpathLevel1String", "Invalid expression {0}", xPathLevel1Expression);
			throw new InvalidExpressionException(AdwsStrings.InvalidExpression, InvalidExpressionDetailType.InvalidExpressionSyntax, xPathLevel1Expression);
		}
		switch (array.Length)
		{
		case 1:
			attributeName = array[0];
			break;
		case 2:
			objectName = array[0];
			attributeName = array[1];
			break;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseXPathLevel1String: after split attributeName={0}", attributeName);
		attributeName = attributeName.Trim();
		if (predicate != string.Empty)
		{
			Match match = AdwsConstants.predicateRegex.Match(predicate);
			if (!match.Success)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ParseXpathLevel1String", "bad predicate {0}", attributeName);
				throw new InvalidExpressionException(AdwsStrings.InvalidPredicate, InvalidExpressionDetailType.InvalidExpressionSyntax, xPathLevel1Expression + predicate);
			}
			predicate = match.Groups["predicate"].Value;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseXPathLevel1String: valid predicate={0}", predicate);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseXPathLevel1String: exit with attributeName={0} predicate={1} objectName={2}", attributeName, predicate, objectName);
	}
}
