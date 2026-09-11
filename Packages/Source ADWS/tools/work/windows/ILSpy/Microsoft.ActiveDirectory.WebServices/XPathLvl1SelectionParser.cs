using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

namespace Microsoft.ActiveDirectory.WebServices;

internal class XPathLvl1SelectionParser : DialectParser
{
	internal override DirectoryObject ParseCreate(InstanceType instance, AttributeTypeAndValueType[] attrTypeAndValueArray)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate entered");
		DirectoryObject directoryObject = new DirectoryObject();
		IList<DirectoryAttribute> attributes = directoryObject.Attributes;
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (attrTypeAndValueArray == null)
		{
			throw new ArgumentNullException("attrTypeAndValueArray");
		}
		if (attrTypeAndValueArray.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.EmptyCreate);
		}
		foreach (AttributeTypeAndValueType attributeTypeAndValueType in attrTypeAndValueArray)
		{
			if (attributeTypeAndValueType.AttributeType == null || attributeTypeAndValueType.AttributeType.XPathExpression == null || attributeTypeAndValueType.AttributeType.XPathExpression.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseCreate:missing expression");
				throw new InvalidExpressionException(AdwsStrings.MissingExpression, InvalidExpressionDetailType.InvalidExpressionSyntax, string.Empty);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate:processing fragmentType with AttributeType {0}", attributeTypeAndValueType.AttributeType.XPathExpression);
			if (attributeTypeAndValueType.AttributeValue == null || attributeTypeAndValueType.AttributeValue.Values.Count == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseCreate:missing value");
				throw new CreateException(AdwsStrings.CreateMissingValues, attributeTypeAndValueType);
			}
			string attrName;
			string attrNameSpace;
			DirectorySyntaxType attrSyntaxType;
			string predicateValue;
			try
			{
				ParseExpression(instance, attributeTypeAndValueType.AttributeType, out var _, out attrName, out attrNameSpace, out attrSyntaxType, out var _, out predicateValue);
			}
			catch (InvalidExpressionException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseCreate:ParseExpression failed.");
				throw;
			}
			if (predicateValue != string.Empty)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseCreate: invalid predicate specified");
				throw new InvalidExpressionException(AdwsStrings.InvalidPredicate, InvalidExpressionDetailType.InvalidExpressionSyntax, attributeTypeAndValueType.AttributeType.XPathExpression);
			}
			DirectoryAttribute directoryAttribute = new DirectoryAttribute();
			directoryAttribute.Name = attrName;
			directoryAttribute.Syntax = attrSyntaxType;
			directoryAttribute.Xmlns = attrNameSpace;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate: created dirAttr Name={0} Syntax={1} Xmlns={2}", attrName, attrSyntaxType, attrNameSpace);
			foreach (SingleValue value in attributeTypeAndValueType.AttributeValue.Values)
			{
				DirectoryValue directoryValue = new DirectoryValue();
				try
				{
					directoryValue.SetXmlValue(value.Value, value.NsPairs, attrSyntaxType);
				}
				catch (ArgumentException innerException)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseCreate: SetXmlValue failed.");
					throw new CreateException(AdwsStrings.BadPutOrCreateValue, attributeTypeAndValueType, innerException);
				}
				directoryAttribute.AttrValues.Add(directoryValue);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate: created dirValue.Value={0}", directoryValue.Value);
			}
			attributes.Add(directoryAttribute);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate: ====================");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseCreate: returning a directoryObject");
		return directoryObject;
	}

	internal override IList<DirectoryAttributeOperation> ParsePut(InstanceType instance, ChangeType[] changeArray)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut entered");
		IList<DirectoryAttributeOperation> list = new List<DirectoryAttributeOperation>();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (changeArray == null)
		{
			throw new ArgumentNullException("changeArray");
		}
		if (changeArray.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.EmptyPut);
		}
		foreach (ChangeType changeType in changeArray)
		{
			if (changeType.Operation == null || (changeType.Operation != AdwsConstants.OperationDelete && changeType.Operation != AdwsConstants.OperationAdd && changeType.Operation != AdwsConstants.OperationReplace))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut: invalid mode {0}", (changeType.Operation == null) ? "NULL" : changeType.Operation);
				throw new ModifyRequestOperationUnsupportedException(AdwsStrings.PutOperationUnsupported, (changeType.Operation == null) ? string.Empty : changeType.Operation);
			}
			ModeType modeType = ((changeType.Operation == AdwsConstants.OperationDelete) ? ModeType.Remove : ((changeType.Operation == AdwsConstants.OperationAdd) ? ModeType.Insert : ModeType.Modify));
			if (changeType.AttributeType == null || changeType.AttributeType.XPathExpression == null || changeType.AttributeType.XPathExpression.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut:missing expression");
				throw new InvalidExpressionException(AdwsStrings.MissingExpression, InvalidExpressionDetailType.InvalidExpressionSyntax, string.Empty);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut:processing putFragmentType with AttributeType {0}", changeType.AttributeType.XPathExpression);
			int num = ((changeType.AttributeValue != null) ? changeType.AttributeValue.Values.Count : 0);
			if (modeType == ModeType.Insert && num == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut:Insert missing Value node(s) expression {0}", changeType.AttributeType.XPathExpression);
				throw new InvalidModifyRequestSyntaxException(AdwsStrings.MissingValue, changeType.AttributeType.XPathExpression);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: Operation={0} AttributeType={1} ValueCount={2}", changeType.Operation, changeType.AttributeType.XPathExpression, num);
			string attrName;
			string attrNameSpace;
			DirectorySyntaxType attrSyntaxType;
			string predicateValue;
			try
			{
				ParseExpression(instance, changeType.AttributeType, out var _, out attrName, out attrNameSpace, out attrSyntaxType, out var _, out predicateValue);
			}
			catch (InvalidExpressionException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut:ParseExpression failed.");
				throw;
			}
			if (predicateValue != string.Empty && modeType != ModeType.Remove)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut:predicate specified on MODIFY or INSERT");
				throw new InvalidExpressionException(AdwsStrings.InvalidPredicate, InvalidExpressionDetailType.InvalidExpressionSyntax, changeType.AttributeType.XPathExpression);
			}
			DirectoryAttributeOperation directoryAttributeOperation = new DirectoryAttributeOperation();
			switch (modeType)
			{
			case ModeType.Modify:
				directoryAttributeOperation.Operation = AttributeOperationType.ReplaceAllValues;
				break;
			case ModeType.Insert:
				directoryAttributeOperation.Operation = AttributeOperationType.AddSpecifiedValues;
				break;
			case ModeType.Remove:
				if (predicateValue == string.Empty && num == 0)
				{
					directoryAttributeOperation.Operation = AttributeOperationType.RemoveAllValues;
				}
				else
				{
					directoryAttributeOperation.Operation = AttributeOperationType.RemoveSpecifiedValues;
				}
				break;
			}
			directoryAttributeOperation.Name = attrName;
			directoryAttributeOperation.Syntax = attrSyntaxType;
			directoryAttributeOperation.Xmlns = attrNameSpace;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: created dirAttrOp Name={0} Syntax={1} Xmlns={2} Operation={3}", directoryAttributeOperation.Name, directoryAttributeOperation.Syntax, directoryAttributeOperation.Xmlns, directoryAttributeOperation.Operation);
			if (num > 0)
			{
				foreach (SingleValue value in changeType.AttributeValue.Values)
				{
					DirectoryValue directoryValue = new DirectoryValue();
					try
					{
						directoryValue.SetXmlValue(value.Value, value.NsPairs, attrSyntaxType);
					}
					catch (ArgumentException innerException)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParsePut: SetXmlValue failed.");
						throw new PutException(AdwsStrings.BadPutOrCreateValue, changeType, innerException);
					}
					directoryAttributeOperation.AttrValues.Add(directoryValue);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: created dirValue.Value={0}", directoryValue.Value);
				}
			}
			if (modeType == ModeType.Remove && predicateValue != string.Empty)
			{
				DirectoryValue directoryValue2 = new DirectoryValue();
				directoryAttributeOperation.AttrValues.Add(directoryValue2);
				directoryValue2.Value = predicateValue;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: created dirValue.Value={0} from predicate", directoryValue2.Value);
			}
			list.Add(directoryAttributeOperation);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: ====================");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParsePut: returning an attrOpList of length {0}", list.Count);
		return list;
	}

	internal override IList<DirectoryAttribute> ParseGet(InstanceType instance, AttributeType[] attrTypeArray)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseGet entered");
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (attrTypeArray == null)
		{
			throw new ArgumentNullException("attrTypeArray");
		}
		IList<DirectoryAttribute> list = ParseXPathExpressions(instance, attrTypeArray, convertAllToStarAttribute: false);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseGet: returning an attrList of length {0}", list.Count);
		return list;
	}

	internal override IList<DirectoryAttribute> ParseSelectionOrSort(InstanceType instance, ExpressionBase[] selectionOrSortArray)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseSelectionOrSort entered");
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (selectionOrSortArray == null)
		{
			throw new ArgumentNullException("selectionOrSortArray");
		}
		IList<DirectoryAttribute> list = ParseXPathExpressions(instance, selectionOrSortArray, convertAllToStarAttribute: true);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseSelectionOrSort: returning an attrList of length {0}", list.Count);
		return list;
	}

	private IList<DirectoryAttribute> ParseXPathExpressions(InstanceType instance, ExpressionBase[] xpathArray, bool convertAllToStarAttribute)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions entered");
		IList<DirectoryAttribute> list = new List<DirectoryAttribute>();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (xpathArray == null)
		{
			throw new ArgumentNullException("xpathArray");
		}
		foreach (ExpressionBase expressionBase in xpathArray)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions:processing getExpression");
			string text = null;
			string text2 = null;
			string attrName;
			string attrNameSpace;
			DirectorySyntaxType attrSyntaxType;
			string predicateValue;
			try
			{
				ParseExpression(instance, expressionBase, out var _, out attrName, out attrNameSpace, out attrSyntaxType, out var _, out predicateValue);
			}
			catch (InvalidExpressionException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "PareseXPathExpressions:ParseExpression failed.");
				throw;
			}
			if (predicateValue != string.Empty)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "PareseXPathExpressions:predicate specified");
				throw new InvalidExpressionException(AdwsStrings.InvalidPredicate, InvalidExpressionDetailType.InvalidExpressionSyntax, expressionBase.XPathExpression);
			}
			DirectoryAttribute directoryAttribute = new DirectoryAttribute();
			if (expressionBase is AttributeType)
			{
				text = ((AttributeType)expressionBase).RangeLow;
				text2 = ((AttributeType)expressionBase).RangeHigh;
			}
			else if (expressionBase is SelectionProperty)
			{
				text = ((SelectionProperty)expressionBase).RangeLow;
				text2 = ((SelectionProperty)expressionBase).RangeHigh;
			}
			if (text != null)
			{
				if (!uint.TryParse(text, out var result))
				{
					throw new ArgumentException(AdwsStrings.BadValueForRangeLow);
				}
				if (text2 != null && !text2.Equals(AdwsConstants.star, StringComparison.InvariantCultureIgnoreCase))
				{
					if (!uint.TryParse(text2, out result))
					{
						throw new ArgumentException(AdwsStrings.BadValueForRangeHigh);
					}
				}
				else if (text2 == null)
				{
					text2 = AdwsConstants.star;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: rangeLow: {0}", text);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: rangeHigh: {0}", text2);
				attrName = attrName + ";range=" + text + "-" + text2;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: range attribute found: {0}", attrName);
			}
			if (text2 != null && text == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "PareseXPathExpressions: rangeLow attribute not specified for the expression");
				throw new ArgumentException(AdwsStrings.MissingLowerRange);
			}
			directoryAttribute.Name = attrName;
			directoryAttribute.Xmlns = attrNameSpace;
			directoryAttribute.Syntax = attrSyntaxType;
			if (attrName.Equals(AdwsConstants.all, StringComparison.InvariantCultureIgnoreCase) && convertAllToStarAttribute)
			{
				directoryAttribute.Name = AdwsConstants.star;
				directoryAttribute.Xmlns = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data";
			}
			else if (attrName.Equals(AdwsConstants.all, StringComparison.InvariantCultureIgnoreCase) && !convertAllToStarAttribute)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "PareseXPathExpressions: 'ad:all' attribute specified");
				throw new InvalidExpressionException(AdwsStrings.UnknownAttribute, InvalidExpressionDetailType.InvalidExpressionValue, attrName);
			}
			list.Add(directoryAttribute);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: found dirAttr Name={0} Syntax={1} Xmlns={2}", attrName, attrSyntaxType, attrNameSpace);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: ====================");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "PareseXPathExpressions: returning an attrList of length {0}", list.Count);
		return list;
	}

	private void ParseExpression(InstanceType instance, ExpressionBase expression, out string attrPrefix, out string attrName, out string attrNameSpace, out DirectorySyntaxType attrSyntaxType, out string objectName, out string predicateValue)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (expression == null)
		{
			throw new ArgumentNullException("expression");
		}
		string predicate = string.Empty;
		string empty = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		attrPrefix = string.Empty;
		attrName = string.Empty;
		attrNameSpace = string.Empty;
		objectName = string.Empty;
		predicateValue = string.Empty;
		attrSyntaxType = DirectorySyntaxType.Boolean;
		try
		{
			XPathLevel1.ParseXPathLevel1String(expression.XPathExpression, out objectName, out attrName, out predicate);
		}
		catch (InvalidExpressionException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:XPathLevel1.ParseXPathLevel1String failed.");
			throw;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseExpression: attrName={0} predicate={1} objectName={2}", attrName, predicate, objectName);
		string[] array = attrName.Split(new char[1] { ':' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 1)
		{
			attrPrefix = string.Empty;
			attrName = array[0];
		}
		else
		{
			if (array.Length != 2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:expression is bad");
				throw new InvalidExpressionException(AdwsStrings.MustSpecifyNamespace, InvalidExpressionDetailType.InvalidExpressionSyntax, expression.XPathExpression);
			}
			attrPrefix = array[0];
			attrName = array[1];
		}
		try
		{
			attrNameSpace = expression.NsPairs[attrPrefix];
		}
		catch (KeyNotFoundException)
		{
			attrNameSpace = null;
		}
		if (attrNameSpace == null || (!attrNameSpace.Equals(AdwsConstants.AdNs, StringComparison.InvariantCultureIgnoreCase) && !attrNameSpace.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase)))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:expression attribute namespace prefix is invalid");
			throw new InvalidExpressionException(AdwsStrings.ObjectInWrongNamespace, InvalidExpressionDetailType.InvalidExpressionValue, expression.XPathExpression);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseExpression: attrNameSpace={0}", attrNameSpace);
		if (predicate != string.Empty)
		{
			Match match = AdwsConstants.predicateCrackRegex.Match(predicate);
			if (!match.Success)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression: predicate is invalid");
				throw new InvalidExpressionException(AdwsStrings.InvalidExpression, InvalidExpressionDetailType.InvalidExpressionSyntax, expression.XPathExpression);
			}
			string text = match.Groups["left"].Value.Trim();
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression: prefFullName={0}", text);
			string[] array2 = text.Split(new char[1] { ':' }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length == 1)
			{
				empty = string.Empty;
				empty2 = array2[0];
			}
			else
			{
				if (array2.Length != 2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:expression is bad");
					throw new InvalidExpressionException(AdwsStrings.MustSpecifyNamespace, InvalidExpressionDetailType.InvalidExpressionSyntax, expression.XPathExpression);
				}
				empty = array2[0];
				empty2 = array2[1];
			}
			if (empty2 != "value")
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:expression predicate value is invalid");
				throw new InvalidExpressionException(AdwsStrings.InvalidPredicate, InvalidExpressionDetailType.InvalidExpressionValue, expression.XPathExpression);
			}
			try
			{
				empty3 = expression.NsPairs[empty];
			}
			catch (KeyNotFoundException)
			{
				empty3 = null;
			}
			if (empty3 == null || (!empty3.Equals(AdwsConstants.AdNs, StringComparison.InvariantCultureIgnoreCase) && !empty3.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase)))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression:expression predicate prefix is invalid");
				throw new InvalidExpressionException(AdwsStrings.ObjectInWrongNamespace, InvalidExpressionDetailType.InvalidExpressionValue, expression.XPathExpression);
			}
			predicateValue = match.Groups["right"].Value;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "XPathLvl1SelectionParser", "ParseExpression: predNameSpace={0} predName={1} predicateValue=[{2}]", empty3, empty2, predicateValue);
		}
		try
		{
			attrSyntaxType = ClassManager.DirectoryAction.GetAttributeType(instance, attrNameSpace, attrName);
		}
		catch (UnknownAttributeException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "XPathLvl1SelectionParser", "ParseExpression: Unable to determine SyntaxType of attribute {0}", attrName);
			throw new InvalidExpressionException(AdwsStrings.UnknownAttribute, InvalidExpressionDetailType.InvalidExpressionValue, expression.XPathExpression);
		}
	}
}
