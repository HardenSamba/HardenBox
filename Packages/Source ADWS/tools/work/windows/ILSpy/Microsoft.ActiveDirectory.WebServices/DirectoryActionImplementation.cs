using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DirectoryActionImplementation : IDirectoryAction, IDisposable
{
	private class SyntheticAttributeNameAndTypePair
	{
		internal string attrName;

		internal DirectorySyntaxType syntaxType;

		internal SyntheticAttributeNameAndTypePair(string attrName, DirectorySyntaxType syntaxType)
		{
			this.attrName = attrName;
			this.syntaxType = syntaxType;
		}
	}

	private static SyntheticAttributeNameAndTypePair[] SyntheticAttributes = new SyntheticAttributeNameAndTypePair[5]
	{
		new SyntheticAttributeNameAndTypePair(AdwsConstants.objectReferenceProperty, DirectorySyntaxType.UnicodeString),
		new SyntheticAttributeNameAndTypePair(AdwsConstants.containerHierarchyParent, DirectorySyntaxType.UnicodeString),
		new SyntheticAttributeNameAndTypePair(AdwsConstants.distinguishedName, DirectorySyntaxType.UnicodeString),
		new SyntheticAttributeNameAndTypePair(AdwsConstants.relativeDistinguishedName, DirectorySyntaxType.UnicodeString),
		new SyntheticAttributeNameAndTypePair(AdwsConstants.all, DirectorySyntaxType.UnicodeString)
	};

	private bool disposed;

	internal DirectoryActionImplementation()
	{
	}

	public DirectoryObjectIdentifier Create(InstanceType instance, DirectoryObject dirObj, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Create: entering, instance={0}, controls={1}", (instance != null) ? instance.InstanceName : "null", (ldapControlsIn != null) ? ldapControlsIn.Count.ToString() : "null");
		CheckDisposed();
		if (dirObj == null)
		{
			throw new ArgumentNullException("dirObj");
		}
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		ldapControlsOut = null;
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		if (dirObj.ObjectIdentity != null || dirObj.ParentIdentity != null || dirObj.RDN != null || dirObj.DistinguishedName != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryActionImplementation", "Create: supplied directory object is incorrect, exiting");
			throw new ArgumentException(AdwsStrings.BadDirectoryObjectForCreate);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Create: object's class is {0}", (dirObj.ObjectClass != null) ? dirObj.ObjectClass : "null");
		IList<DirectoryAttribute> attributes = dirObj.Attributes;
		List<DirectoryAttribute> attrListCopy = new List<DirectoryAttribute>(attributes);
		try
		{
			RejectObjectRefOrDnChanges(attributes, AdwsStrings.CantSetDistinguishedNameForCreate, AdwsStrings.CantSetObjectRefPropertyForCreate);
			string rDN;
			try
			{
				rDN = containerMapper.GetRDN(attributes);
			}
			catch (ArgumentException innerException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Create: bad RDN attribute");
				throw new ArgumentException(AdwsStrings.InvalidRdnForCreateAndUpdate, innerException);
			}
			if (rDN != null)
			{
				dirObj.RDN = rDN;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Create: object's RDN is {0}", rDN);
			}
			object parentObjectRefProperty;
			try
			{
				parentObjectRefProperty = containerMapper.GetParentObjectRefProperty(attributes);
			}
			catch (ArgumentException innerException2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Create: bad parent attribute");
				throw new ArgumentException(AdwsStrings.InvalidParentObjectRefForCreateAndUpdate, innerException2);
			}
			if (parentObjectRefProperty != null)
			{
				if (parentObjectRefProperty is string)
				{
					dirObj.ParentIdentity = new DirectoryObjectIdentifier((string)parentObjectRefProperty, instance);
				}
				else
				{
					dirObj.ParentIdentity = new DirectoryObjectIdentifier((Guid)parentObjectRefProperty, instance);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Create: object's parent is {0}", parentObjectRefProperty.ToString());
			}
			Guid objectReferenceProperty = directoryDataAccess.CreateObject(dirObj, ldapControlsIn, out ldapControlsOut);
			DirectoryObjectIdentifier result = (dirObj.ObjectIdentity = new DirectoryObjectIdentifier(objectReferenceProperty, instance));
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Create: newly-created object's identity is {0}", objectReferenceProperty.ToString());
			return result;
		}
		finally
		{
			RestoreOriginalList(attributes, attrListCopy);
		}
	}

	public void Update(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> attrsToModify, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		Update(objectId, attrsToModify, ldapControlsIn, usePermissiveModify: false, out ldapControlsOut);
	}

	public void Update(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> attrsToModify, IList<DirectoryControl> ldapControlsIn, bool usePermissiveModify, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: entering, permissive modify={0}, controls={1}", usePermissiveModify, (ldapControlsIn != null) ? ldapControlsIn.Count.ToString() : "null");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (attrsToModify == null)
		{
			throw new ArgumentNullException("attrsToModify");
		}
		ldapControlsOut = null;
		foreach (DirectoryAttributeOperation item in attrsToModify)
		{
			if (item == null || item.Name == null || item.Name.Length == 0 || item.Xmlns == null || item.Xmlns.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryActionImplementation", "Update: DirAttrOper is null or has a null name or namespace");
				throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForUpdate);
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: instance={0}, object={1}, attr count={2}", (objectId.Instance != null) ? objectId.Instance.InstanceName : "null", objectId.GetObjectRefOrDnAsString(), attrsToModify.Count);
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		List<DirectoryAttributeOperation> list = new List<DirectoryAttributeOperation>(attrsToModify);
		RejectObjectRefOrDnChanges(list, AdwsStrings.CantSetDistinguishedNameForUpdate, AdwsStrings.CantSetObjectRefPropertyForUpdate);
		DirectoryObjectIdentifier directoryObjectIdentifier = null;
		if (objectId.IsObjectIdentifiedByDn() && (containerMapper.IsMove(list) || containerMapper.IsRename(list)) && containerMapper.HasAttrUpdates(list))
		{
			directoryObjectIdentifier = new DirectoryObjectIdentifier();
			directoryObjectIdentifier.Instance = objectId.Instance;
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Update: need GUID as ((is DN), and (is a move or rename), and (has attr updates))");
			try
			{
				directoryObjectIdentifier.ObjectReferenceProperty = directoryDataAccess.GetObjectRefPropertyFromDn(objectId.Instance, objectId.ObjectDn, DirectoryUtilities.ContainsShowDeletedControl(ldapControlsIn));
			}
			catch (ArgumentException innerException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Update: caller specified empty DN");
				throw new ArgumentException(AdwsStrings.InvalidObjectReferenceProperty, innerException);
			}
			if (directoryObjectIdentifier.ObjectReferenceProperty.Equals(Guid.Empty))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Update: couldn't find GUID to substitute for {0}", objectId.ObjectDn);
				throw new LdapException(32);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: got Guid {0} for DN {1}", directoryObjectIdentifier.ObjectReferenceProperty.ToString(), objectId.ObjectDn);
		}
		if (containerMapper.IsMove(list))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: move requested");
			object parentObjectRefProperty;
			DirectoryAttributeOperation attrRemoved;
			try
			{
				parentObjectRefProperty = containerMapper.GetParentObjectRefProperty(list, out attrRemoved);
			}
			catch (ArgumentException innerException2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Update: move has bad parent attribute");
				throw new ArgumentException(AdwsStrings.InvalidParentObjectRefForCreateAndUpdate, innerException2);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: new parent for move is {0}", parentObjectRefProperty.ToString());
			if (attrRemoved.Operation != AttributeOperationType.ReplaceAllValues)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "Update: trying to move with oper type={0}, exiting", attrRemoved.Operation.ToString());
				throw new ArgumentException(AdwsStrings.CanOnlyReplaceParentObjectRefForUpdate);
			}
			string text = null;
			if (containerMapper.IsRename(list))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: rename in conjunction with a move requested");
				text = GetNewRdnForRename(list);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: new RDN for move-with-rename is {0}", text);
			}
			if (parentObjectRefProperty is string)
			{
				directoryDataAccess.MoveObject(objectId, (string)parentObjectRefProperty, text, ldapControlsIn, out ldapControlsOut);
			}
			else
			{
				directoryDataAccess.MoveObject(objectId, (Guid)parentObjectRefProperty, text, ldapControlsIn, out ldapControlsOut);
			}
		}
		else if (containerMapper.IsRename(list))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: rename w/o move requested");
			string newRdnForRename = GetNewRdnForRename(list);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: new RDN for rename is {0}", newRdnForRename);
			directoryDataAccess.RenameObject(objectId, newRdnForRename, ldapControlsIn, out ldapControlsOut);
		}
		if (list.Count > 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Update: performing attribute update for {0} attributes", list.Count);
			directoryDataAccess.UpdateAttributes((directoryObjectIdentifier == null) ? objectId : directoryObjectIdentifier, list, ldapControlsIn, usePermissiveModify, out ldapControlsOut);
		}
	}

	public DirectoryObject Read(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObj): entering, controls={0}", (ldapControlsIn != null) ? ldapControlsIn.Count.ToString() : "null");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		ldapControlsOut = null;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObj): instance={0}, object={1}", (objectId.Instance != null) ? objectId.Instance.InstanceName : "null", objectId.GetObjectRefOrDnAsString());
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		DirectoryObject directoryObject = directoryDataAccess.ReadObject(objectId, ldapControlsIn, out ldapControlsOut);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObj): read object {0} successfully.", objectId.GetObjectRefOrDnAsString());
		containerMapper.FillInContainerFields(directoryObject, DirectoryUtilities.ContainsShowDeletedControl(ldapControlsIn));
		return directoryObject;
	}

	public DirectoryObjectFragment Read(DirectoryObjectIdentifier objectId, IList<DirectoryAttribute> attrsToRetrieve, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): entering, controls={0}", (ldapControlsIn != null) ? ldapControlsIn.Count.ToString() : "null");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (attrsToRetrieve == null)
		{
			throw new ArgumentNullException("attrsToRetrieve");
		}
		ldapControlsOut = null;
		foreach (DirectoryAttribute item in attrsToRetrieve)
		{
			if (item == null || item.Name == null || item.Name.Length == 0 || item.Xmlns == null || item.Xmlns.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryActionImplementation", "Read(DirObjFragment): DirAttr is null or has a null name or namespace");
				throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForRead);
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): instance={0}, object={1}, attr count={2}", (objectId.Instance != null) ? objectId.Instance.InstanceName : "null", objectId.GetObjectRefOrDnAsString(), attrsToRetrieve.Count);
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		List<DirectoryAttribute> list = new List<DirectoryAttribute>(attrsToRetrieve);
		DirectoryAttribute specialDirectoryAttribute = GetSpecialDirectoryAttribute(list, AdwsConstants.relativeDistinguishedName, removeFromList: true);
		DirectoryAttribute specialDirectoryAttribute2 = GetSpecialDirectoryAttribute(list, AdwsConstants.distinguishedName, removeFromList: true);
		DirectoryAttribute specialDirectoryAttribute3 = GetSpecialDirectoryAttribute(list, AdwsConstants.objectReferenceProperty, removeFromList: true);
		DirectoryAttribute specialDirectoryAttribute4 = GetSpecialDirectoryAttribute(list, AdwsConstants.containerHierarchyParent, removeFromList: true);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): RDN was {0} requested", (specialDirectoryAttribute == null) ? "NOT" : "");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): DN was {0} requested", (specialDirectoryAttribute2 == null) ? "NOT" : "");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): object identity was {0} requested", (specialDirectoryAttribute3 == null) ? "NOT" : "");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): parent was {0} requested", (specialDirectoryAttribute4 == null) ? "NOT" : "");
		DirectoryObjectFragment directoryObjectFragment = directoryDataAccess.ReadObject(objectId, list, ldapControlsIn, out ldapControlsOut);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): read object successfully");
		containerMapper.FillInContainerFields(directoryObjectFragment, attrsToRetrieve, DirectoryUtilities.ContainsShowDeletedControl(ldapControlsIn));
		if (specialDirectoryAttribute == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): removing RDN field");
			directoryObjectFragment.RDN = null;
		}
		if (specialDirectoryAttribute2 == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): removing DN field");
			directoryObjectFragment.DistinguishedName = null;
		}
		if (specialDirectoryAttribute3 == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Read(DirObjFragment): removing object identity field");
			directoryObjectFragment.ObjectIdentity = null;
		}
		return directoryObjectFragment;
	}

	public void Delete(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Delete: entering, controls={0}", (ldapControlsIn != null) ? ldapControlsIn.Count.ToString() : "null");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		ldapControlsOut = null;
		ClassManager.DirectoryDataAccess.DeleteObject(objectId, ldapControlsIn, out ldapControlsOut);
	}

	public Query PrepareQuery(InstanceType instance, QueryFilter filter, IList<DirectoryAttribute> attributesToReturn, DirectoryAttribute attributeToSortOn, bool ascendingSort, DirectoryActionNotifyOfConnectionTimeout notifyIfTimeout)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery: entering");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (filter == null)
		{
			throw new ArgumentNullException("filter");
		}
		if (attributesToReturn == null)
		{
			throw new ArgumentNullException("attributesToReturn");
		}
		if (!(filter is LdapQueryFilter))
		{
			throw new ArgumentException(AdwsStrings.QueryFilterMustBeAnLdapQueryFilter);
		}
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		LdapQuery ldapQuery = new LdapQuery();
		ldapQuery.AllRequestedAttributesToRetrieve.AddRange(attributesToReturn);
		ldapQuery.SortKey = attributeToSortOn;
		ldapQuery.AscendingSort = ascendingSort;
		ldapQuery.Instance = instance;
		ldapQuery.QueryFilter = filter;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery: Query properties");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Attribute to return count: {0}", attributesToReturn.Count);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Attribute to sort on: {0}", (attributeToSortOn != null) ? attributeToSortOn.Name : "NULL");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Ascending sort: {0}", ascendingSort.ToString());
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Instance {0}", instance.InstanceName);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Filter {0}", filter.ToString());
		if (GlobalDebug.Info)
		{
			foreach (DirectoryAttribute item in attributesToReturn)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery:   Attribute to return: {0}", item.Name);
			}
		}
		ldapQuery.RoutineToNotify = notifyIfTimeout;
		ldapQuery.Session = directoryDataAccess.GetLdapSession(operationTimeout, instance, ldapQuery.DataAccessTimedOutCallback);
		if (ldapQuery.Session == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "PrepareQuery: no LdapSession available for the Query");
			throw new NoConnectionAvailableException();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "PrepareQuery: got LdapSession for the Query");
		return ldapQuery;
	}

	public IList<DirectoryObjectFragment> NextQueryPage(Query query, TimeSpan maxTime, uint maxObjects, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: entering");
		CheckDisposed();
		ldapControlsOut = null;
		if (query == null)
		{
			throw new ArgumentNullException("query");
		}
		if (maxObjects == 0)
		{
			throw new ArgumentException(AdwsStrings.PageSizeMustBeGreaterThanZero);
		}
		if (maxTime < TimeSpan.Zero)
		{
			throw new ArgumentException(AdwsStrings.ServerTimeMustBeNonNegative);
		}
		if (!(query is LdapQuery))
		{
			throw new ArgumentException(AdwsStrings.QueryMustBeAnLdapQuery);
		}
		IDirectoryDataAccess directoryDataAccess = ClassManager.DirectoryDataAccess;
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		LdapQuery ldapQuery = query as LdapQuery;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: asking for {0} objects maximum in max time of {1}", maxObjects, maxTime.ToString());
		if (ldapQuery.Mode == LdapQueryMode.Unprepared)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: preparing the query");
			directoryDataAccess.PreparePageSearch(ldapQuery);
		}
		if (ldapQuery.NoMoreResults())
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: no more results, skipping");
			return new List<DirectoryObjectFragment>();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: retrieving a page");
		IList<DirectoryObjectFragment> nextPageSearchResults = directoryDataAccess.GetNextPageSearchResults(ldapQuery, maxObjects, maxTime, ldapControlsIn, out ldapControlsOut);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: retrieved {0} objects on the page", nextPageSearchResults.Count);
		foreach (DirectoryObjectFragment item in nextPageSearchResults)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: got object {0}", item.DistinguishedName);
			containerMapper.FillInContainerFields(item, ldapQuery, DirectoryUtilities.ContainsShowDeletedControl(ldapControlsIn));
			if (ldapQuery.AllRequestedAttributesToRetrieve.Count > 0)
			{
				DirectoryAttribute specialDirectoryAttribute = GetSpecialDirectoryAttribute(ldapQuery.AllRequestedAttributesToRetrieve, AdwsConstants.relativeDistinguishedName, removeFromList: false);
				DirectoryAttribute specialDirectoryAttribute2 = GetSpecialDirectoryAttribute(ldapQuery.AllRequestedAttributesToRetrieve, AdwsConstants.distinguishedName, removeFromList: false);
				if (specialDirectoryAttribute == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: removing RDN field");
					item.RDN = null;
				}
				if (specialDirectoryAttribute2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "NextQueryPage: removing DN field");
					item.DistinguishedName = null;
				}
			}
		}
		return nextPageSearchResults;
	}

	public DirectorySyntaxType GetAttributeType(InstanceType instance, string attrNameSpace, string attrName)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "GetAttributeType: entering");
		CheckDisposed();
		if (instance == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetAttributeType: instance is null");
			throw new ArgumentNullException("instance");
		}
		if (attrNameSpace == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetAttributeType: attrNameSpace is null");
			throw new ArgumentNullException("attrNameSpace");
		}
		if (attrName == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetAttributeType: attrName is null");
			throw new ArgumentNullException("attrName");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "GetAttributeType: attrNameSpace={0} attrName={1}", attrNameSpace, attrName);
		if (!attrNameSpace.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase) && !attrNameSpace.Equals(AdwsConstants.AdNs, StringComparison.InvariantCultureIgnoreCase))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryActionImplementation", "GetAttributeType: Invalid namespace {0}", attrNameSpace);
			throw new ArgumentException(AdwsStrings.ObjectInWrongNamespace);
		}
		if (attrNameSpace.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase))
		{
			DirectorySyntaxType attributeType;
			try
			{
				attributeType = ClassManager.DirectoryDataAccess.GetAttributeType(instance, attrName);
			}
			catch (UnknownAttributeException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetAttributeType: Unable to determine SyntaxType of attribute {0}", attrName);
				throw;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "GetAttributeType: return 'real' attrSyntaxType={0}", attributeType);
			return attributeType;
		}
		SyntheticAttributeNameAndTypePair[] syntheticAttributes = SyntheticAttributes;
		foreach (SyntheticAttributeNameAndTypePair syntheticAttributeNameAndTypePair in syntheticAttributes)
		{
			if (attrName.Equals(syntheticAttributeNameAndTypePair.attrName, StringComparison.InvariantCultureIgnoreCase))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "GetAttributeType: return 'synthetic' attr={0}, syntax={1}", syntheticAttributeNameAndTypePair.attrName, syntheticAttributeNameAndTypePair.syntaxType.ToString());
				return syntheticAttributeNameAndTypePair.syntaxType;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetAttributeType: Unable to determine SyntaxType of 'synthetic' attribute {0}", attrName);
		throw new UnknownAttributeException(attrName);
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryActionImplementation", "Dispose: disposing...");
				disposed = true;
			}
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	private void RestoreOriginalList<T>(IList<T> attrListOriginal, IList<T> attrListCopy) where T : DirectoryAttribute
	{
		if (attrListCopy.Count == attrListOriginal.Count)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < attrListCopy.Count; i++)
		{
			if (i >= attrListOriginal.Count || attrListCopy[i] != attrListOriginal[num])
			{
				T item = attrListCopy[i];
				attrListOriginal.Insert(num, item);
			}
			num++;
		}
	}

	private void RejectObjectRefOrDnChanges<T>(IList<T> attrList, string messageForRejectingDn, string messageForRejectingObjRef) where T : DirectoryAttribute
	{
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		try
		{
			if (containerMapper.GetDN(attrList) != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "RejectObjectRefOrDnChanges: trying to set <ad:rdn> attribute, exiting");
				throw new InvalidOperationException(messageForRejectingDn);
			}
		}
		catch (ArgumentException innerException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "RejectObjectRefOrDnChanges: trying to set <ad:rdn> attribute (2), exiting");
			throw new InvalidOperationException(messageForRejectingDn, innerException);
		}
		try
		{
			if (containerMapper.GetObjectRefProperty(attrList) != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "RejectObjectRefOrDnChanges: trying to set <ad:parent> attribute, exiting");
				throw new InvalidOperationException(messageForRejectingObjRef);
			}
		}
		catch (ArgumentException innerException2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "RejectObjectRefOrDnChanges: trying to set <ad:parent> attribute (2), exiting");
			throw new InvalidOperationException(messageForRejectingObjRef, innerException2);
		}
	}

	private DirectoryAttribute GetSpecialDirectoryAttribute(IList<DirectoryAttribute> attrList, string attrName, bool removeFromList)
	{
		return DirectoryUtilities.FindDirAttribute(attrList, attrName, AdwsConstants.AdNs, removeFromList, OneValueEnforced: false);
	}

	public QueryFilter GetDefaultQueryFilter(InstanceType instance)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		return ClassManager.DirectoryDataAccess.GetDefaultLdapQueryFilter(instance);
	}

	public string GetNewRdnForRename(List<DirectoryAttributeOperation> attrList)
	{
		IContainerMapper containerMapper = ClassManager.ContainerMapper;
		string text = null;
		if (attrList == null)
		{
			throw new ArgumentNullException("attrList");
		}
		DirectoryAttributeOperation attrRemoved;
		try
		{
			text = containerMapper.GetRDN(attrList, out attrRemoved);
		}
		catch (ArgumentException innerException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetNewRdnForRename: rename has bad RDN attribute");
			throw new ArgumentException(AdwsStrings.InvalidRdnForCreateAndUpdate, innerException);
		}
		if (attrRemoved.Operation != AttributeOperationType.ReplaceAllValues)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryActionImplementation", "GetNewRdnForRename: trying to rename with oper type={0}, exiting", attrRemoved.Operation.ToString());
			throw new ArgumentException(AdwsStrings.CanOnlyReplaceRdnForUpdate);
		}
		return text;
	}
}
