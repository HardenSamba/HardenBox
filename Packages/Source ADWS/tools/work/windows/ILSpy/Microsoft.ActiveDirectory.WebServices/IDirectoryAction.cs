using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface IDirectoryAction : IDisposable
{
	DirectoryObjectIdentifier Create(InstanceType instance, DirectoryObject o, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void Update(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> fragments, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void Update(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> fragments, IList<DirectoryControl> ldapControlsIn, bool usePermissiveModify, out IList<DirectoryControl> ldapControlsOut);

	DirectoryObject Read(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	DirectoryObjectFragment Read(DirectoryObjectIdentifier objectId, IList<DirectoryAttribute> attrsToRetrieve, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void Delete(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	Query PrepareQuery(InstanceType instance, QueryFilter filter, IList<DirectoryAttribute> attributesToReturn, DirectoryAttribute attributeToSortOn, bool ascendingSort, DirectoryActionNotifyOfConnectionTimeout notifyIfTimeout);

	IList<DirectoryObjectFragment> NextQueryPage(Query query, TimeSpan maxTime, uint maxObjects, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	DirectorySyntaxType GetAttributeType(InstanceType instance, string attrNameSpace, string attrName);

	QueryFilter GetDefaultQueryFilter(InstanceType instance);
}
