using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface IDirectoryDataAccess : IDisposable
{
	void InitializeInstance(InstanceType instance, uint initialConnectionCount, uint maxConnectionCount);

	bool IsInstanceKnown(InstanceType instance);

	void RemoveInstance(InstanceType instance);

	Guid CreateObject(DirectoryObject obj, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void DeleteObject(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void MoveObject(DirectoryObjectIdentifier objectId, Guid newParentObjectReferenceProperty, string newRdn, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void MoveObject(DirectoryObjectIdentifier objectId, string newParentDn, string newRdn, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void RenameObject(DirectoryObjectIdentifier objectId, string newRDN, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void UpdateAttributes(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> operations, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	void UpdateAttributes(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> operations, IList<DirectoryControl> ldapControlsIn, bool usePermissiveModify, out IList<DirectoryControl> ldapControlsOut);

	string GetObjectType(DirectoryObjectIdentifier objectId, bool useShowDeletedControl);

	DirectorySyntaxType GetAttributeType(InstanceType instance, string ldapAttributeName);

	DirectoryObject ReadObject(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	DirectoryObjectFragment ReadObject(DirectoryObjectIdentifier objectId, IList<DirectoryAttribute> attrsToRetrieve, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	DirectoryObjectIdentifier GetParentIdentifier(DirectoryObjectIdentifier o, bool useShowDeletedControl);

	Guid GetObjectRefPropertyFromDn(InstanceType instance, string dn, bool useShowDeletedControl);

	LdapSession GetLdapSession(TimeSpan timeToWaitToAcquire, InstanceType instance, DataAccessNotifyOfTimeout notifyIfExpires);

	void ReleaseLdapSession(LdapSession session);

	void PreparePageSearch(LdapQuery query);

	IList<DirectoryObjectFragment> GetNextPageSearchResults(LdapQuery query, uint pageSize, TimeSpan maxTime, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut);

	LdapQueryFilter GetDefaultLdapQueryFilter(InstanceType instance);
}
