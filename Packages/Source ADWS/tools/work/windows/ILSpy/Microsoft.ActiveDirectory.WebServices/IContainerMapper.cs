using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface IContainerMapper
{
	void FillInContainerFields(DirectoryObject o, bool useShowDeletedControl);

	void FillInContainerFields(DirectoryObjectFragment o, IList<DirectoryAttribute> attrsToRetrieve, bool useShowDeletedControl);

	void FillInContainerFields(DirectoryObjectFragment o, Query filter, bool useShowDeletedControl);

	object GetParentObjectRefProperty<T>(IList<T> attrs, out T attrRemoved) where T : DirectoryAttribute;

	object GetParentObjectRefProperty<T>(IList<T> attrs) where T : DirectoryAttribute;

	string GetRDN<T>(IList<T> attrs, out T attrRemoved) where T : DirectoryAttribute;

	string GetRDN<T>(IList<T> attrs) where T : DirectoryAttribute;

	string GetDN<T>(IList<T> attrs) where T : DirectoryAttribute;

	object GetObjectRefProperty<T>(IList<T> attrs) where T : DirectoryAttribute;

	bool IsRename(IList<DirectoryAttributeOperation> fragments);

	bool IsMove(IList<DirectoryAttributeOperation> fragments);

	bool HasAttrUpdates(IList<DirectoryAttributeOperation> fragments);
}
