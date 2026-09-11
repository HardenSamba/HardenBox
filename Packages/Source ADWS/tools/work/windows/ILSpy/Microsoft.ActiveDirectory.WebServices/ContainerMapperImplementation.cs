using System;
using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ContainerMapperImplementation : IContainerMapper
{
	public void FillInContainerFields(DirectoryObject dirObject, bool useShowDeletedControl)
	{
		if (dirObject == null)
		{
			throw new ArgumentNullException("dirObject");
		}
		dirObject.ParentIdentity = ClassManager.DirectoryDataAccess.GetParentIdentifier(dirObject.ObjectIdentity, useShowDeletedControl);
	}

	public void FillInContainerFields(DirectoryObjectFragment dirObjectFragment, IList<DirectoryAttribute> attrsToRetrieve, bool useShowDeletedControl)
	{
		if (dirObjectFragment == null)
		{
			throw new ArgumentNullException("dirObjectFragment");
		}
		if (attrsToRetrieve == null)
		{
			throw new ArgumentNullException("attrsToRetrieve");
		}
		if (DirectoryUtilities.FindDirAttribute(attrsToRetrieve, AdwsConstants.containerHierarchyParent, AdwsConstants.AdNs, RemoveIfFound: false, OneValueEnforced: false) != null)
		{
			dirObjectFragment.ParentIdentity = ClassManager.DirectoryDataAccess.GetParentIdentifier(dirObjectFragment.ObjectIdentity, useShowDeletedControl);
		}
	}

	public void FillInContainerFields(DirectoryObjectFragment dirObjectFragment, Query filter, bool useShowDeletedControl)
	{
		if (dirObjectFragment == null)
		{
			throw new ArgumentNullException("dirObjectFragment");
		}
		if (filter == null)
		{
			throw new ArgumentNullException("filter");
		}
		if (filter.ReturnParentInformation())
		{
			dirObjectFragment.ParentIdentity = ClassManager.DirectoryDataAccess.GetParentIdentifier(dirObjectFragment.ObjectIdentity, useShowDeletedControl);
		}
	}

	public object GetParentObjectRefProperty<T>(IList<T> attrs, out T removedAttr) where T : DirectoryAttribute
	{
		T val = null;
		removedAttr = null;
		if (attrs == null)
		{
			throw new ArgumentNullException("attrs");
		}
		val = (removedAttr = DirectoryUtilities.FindDirAttribute(attrs, AdwsConstants.containerHierarchyParent, AdwsConstants.AdNs, RemoveIfFound: true, OneValueEnforced: true));
		if (val != null)
		{
			return GetGuidOrDnFromValue(val.AttrValues[0].Value);
		}
		return null;
	}

	public object GetParentObjectRefProperty<T>(IList<T> attrs) where T : DirectoryAttribute
	{
		T removedAttr = null;
		return GetParentObjectRefProperty(attrs, out removedAttr);
	}

	public string GetRDN<T>(IList<T> attrs, out T removedAttr) where T : DirectoryAttribute
	{
		T val = null;
		removedAttr = null;
		if (attrs == null)
		{
			throw new ArgumentNullException("attrs");
		}
		val = (removedAttr = DirectoryUtilities.FindDirAttribute(attrs, AdwsConstants.relativeDistinguishedName, AdwsConstants.AdNs, RemoveIfFound: true, OneValueEnforced: true));
		if (val != null)
		{
			if (val.AttrValues[0].Value is string)
			{
				return (string)val.AttrValues[0].Value;
			}
			throw new ArgumentException(AdwsStrings.AttributeValueNotaString);
		}
		return null;
	}

	public string GetRDN<T>(IList<T> attrs) where T : DirectoryAttribute
	{
		T removedAttr = null;
		return GetRDN(attrs, out removedAttr);
	}

	public string GetDN<T>(IList<T> attrs) where T : DirectoryAttribute
	{
		T val = null;
		if (attrs == null)
		{
			throw new ArgumentNullException("attrs");
		}
		val = DirectoryUtilities.FindDirAttribute(attrs, AdwsConstants.distinguishedName, AdwsConstants.AdNs, RemoveIfFound: true, OneValueEnforced: true);
		if (val != null)
		{
			if (val.AttrValues[0].Value is string)
			{
				return (string)val.AttrValues[0].Value;
			}
			throw new ArgumentException(AdwsStrings.AttributeValueNotaString);
		}
		return null;
	}

	public object GetObjectRefProperty<T>(IList<T> attrs) where T : DirectoryAttribute
	{
		T val = null;
		if (attrs == null)
		{
			throw new ArgumentNullException("attrs");
		}
		val = DirectoryUtilities.FindDirAttribute(attrs, AdwsConstants.objectReferenceProperty, AdwsConstants.AdNs, RemoveIfFound: true, OneValueEnforced: true);
		if (val != null)
		{
			return GetGuidOrDnFromValue(val.AttrValues[0].Value);
		}
		return null;
	}

	public bool IsRename(IList<DirectoryAttributeOperation> fragments)
	{
		if (fragments == null)
		{
			throw new ArgumentNullException("fragments");
		}
		if (DirectoryUtilities.FindDirAttribute(fragments, AdwsConstants.relativeDistinguishedName, AdwsConstants.AdNs, RemoveIfFound: false, OneValueEnforced: false) != null)
		{
			return true;
		}
		return false;
	}

	public bool IsMove(IList<DirectoryAttributeOperation> fragments)
	{
		if (fragments == null)
		{
			throw new ArgumentNullException("fragments");
		}
		if (DirectoryUtilities.FindDirAttribute(fragments, AdwsConstants.containerHierarchyParent, AdwsConstants.AdNs, RemoveIfFound: false, OneValueEnforced: false) != null)
		{
			return true;
		}
		return false;
	}

	public bool HasAttrUpdates(IList<DirectoryAttributeOperation> fragments)
	{
		if (fragments == null)
		{
			throw new ArgumentNullException("fragments");
		}
		int num = fragments.Count;
		if (IsMove(fragments))
		{
			num--;
		}
		if (IsRename(fragments))
		{
			num--;
		}
		if (num <= 0)
		{
			return false;
		}
		return true;
	}

	private object GetGuidOrDnFromValue(object v)
	{
		if (v is string)
		{
			string text = (string)v;
			if (text.Length == 36 && AdwsConstants.guidRegex.IsMatch(text))
			{
				try
				{
					return new Guid(text);
				}
				catch (ArgumentException)
				{
				}
			}
			if (text.Length > 0)
			{
				return text;
			}
		}
		throw new ArgumentException(AdwsStrings.AttributeValueNotaObjRef);
	}
}
