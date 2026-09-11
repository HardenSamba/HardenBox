using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DirectoryDataAccessImplementation : IDirectoryDataAccess, IDisposable
{
	private class LdapAttrSyntaxTable : IDisposable
	{
		private Dictionary<string, LdapAttrSyntaxTableEntry> attrToSyntaxMapping;

		private ReaderWriterLockSlim attrToSyntaxMappingRWLock;

		internal Dictionary<string, LdapAttrSyntaxTableEntry> AttrToSyntaxMapping => attrToSyntaxMapping;

		internal ReaderWriterLockSlim AttrToSyntaxMappingRWLock => attrToSyntaxMappingRWLock;

		internal LdapAttrSyntaxTable()
		{
			attrToSyntaxMapping = new Dictionary<string, LdapAttrSyntaxTableEntry>();
			attrToSyntaxMappingRWLock = new ReaderWriterLockSlim();
		}

		public void Dispose()
		{
			if (attrToSyntaxMappingRWLock != null)
			{
				attrToSyntaxMappingRWLock.Dispose();
				attrToSyntaxMappingRWLock = null;
			}
		}
	}

	private class LdapAttrSyntaxTableEntry
	{
		private string ldapAttrName;

		private DirectorySyntaxType syntax;

		private DateTime retrievalTime;

		internal string LdapAttrName => ldapAttrName;

		internal DirectorySyntaxType Syntax
		{
			get
			{
				return syntax;
			}
			set
			{
				syntax = value;
			}
		}

		internal DateTime RetrievalTime
		{
			get
			{
				return retrievalTime;
			}
			set
			{
				retrievalTime = value;
			}
		}

		internal LdapAttrSyntaxTableEntry(string ldapAttrName, DirectorySyntaxType syntax)
			: this(ldapAttrName, syntax, DateTime.UtcNow)
		{
		}

		internal LdapAttrSyntaxTableEntry(string ldapAttrName, DirectorySyntaxType syntax, DateTime retrievalTime)
		{
			this.ldapAttrName = ldapAttrName.ToLower();
			this.syntax = syntax;
			this.retrievalTime = retrievalTime;
		}
	}

	private class AdSyntax
	{
		private string attrSyntax;

		private string omSyntax;

		private byte[] omObjectClass;

		internal AdSyntax(string attrSyntax, string omSyntax, byte[] omObjectClass)
		{
			this.attrSyntax = attrSyntax;
			this.omSyntax = omSyntax;
			this.omObjectClass = omObjectClass;
		}

		public override bool Equals(object o)
		{
			if (!(o is AdSyntax adSyntax))
			{
				return false;
			}
			if (attrSyntax == adSyntax.attrSyntax && omSyntax == adSyntax.omSyntax)
			{
				if (omObjectClass == null && adSyntax.omObjectClass == null)
				{
					return true;
				}
				if (omObjectClass != null && adSyntax.omObjectClass != null && omObjectClass.Length == adSyntax.omObjectClass.Length)
				{
					for (int i = 0; i < omObjectClass.Length; i++)
					{
						if (omObjectClass[i] != adSyntax.omObjectClass[i])
						{
							return false;
						}
					}
					return true;
				}
				return false;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return attrSyntax.GetHashCode() ^ omSyntax.GetHashCode();
		}
	}

	private bool disposed;

	private Dictionary<InstanceType, LdapAttrSyntaxTable> instanceToSyntaxMappingTable;

	private ReaderWriterLockSlim instanceToSyntaxMappingTableRWLock;

	private Dictionary<AdSyntax, DirectorySyntaxType> adSyntaxToDirectorySyntaxType;

	internal DirectoryDataAccessImplementation()
	{
		instanceToSyntaxMappingTable = new Dictionary<InstanceType, LdapAttrSyntaxTable>();
		instanceToSyntaxMappingTableRWLock = new ReaderWriterLockSlim();
		adSyntaxToDirectorySyntaxType = new Dictionary<AdSyntax, DirectorySyntaxType>();
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.8", "1", null)] = DirectorySyntaxType.Boolean;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.9", "10", null)] = DirectorySyntaxType.Enumeration;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.9", "2", null)] = DirectorySyntaxType.Integer;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.16", "65", null)] = DirectorySyntaxType.LargeInteger;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.14", "127", new byte[9] { 43, 12, 2, 135, 115, 28, 0, 133, 62 })] = DirectorySyntaxType.AccessPoint;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.14", "127", new byte[10] { 42, 134, 72, 134, 247, 20, 1, 1, 1, 12 })] = DirectorySyntaxType.DNString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.7", "127", new byte[7] { 86, 6, 1, 2, 5, 11, 29 })] = DirectorySyntaxType.ORName;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.7", "127", new byte[10] { 42, 134, 72, 134, 247, 20, 1, 1, 1, 11 })] = DirectorySyntaxType.DNBinary;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.1", "127", new byte[9] { 43, 12, 2, 135, 115, 28, 0, 133, 74 })] = DirectorySyntaxType.DSDNString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.13", "127", new byte[9] { 43, 12, 2, 135, 115, 28, 0, 133, 92 })] = DirectorySyntaxType.PresentationAddress;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.10", "127", new byte[10] { 42, 134, 72, 134, 247, 20, 1, 1, 1, 6 })] = DirectorySyntaxType.ReplicaLink;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.3", "27", null)] = DirectorySyntaxType.CaseString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.5", "22", null)] = DirectorySyntaxType.IA5String;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.15", "66", null)] = DirectorySyntaxType.NTSecurityDescriptor;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.6", "18", null)] = DirectorySyntaxType.NumericString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.2", "6", null)] = DirectorySyntaxType.ObjectIdentifier;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.10", "4", null)] = DirectorySyntaxType.OctetString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.5", "19", null)] = DirectorySyntaxType.PrintableString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.17", "4", null)] = DirectorySyntaxType.SidString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.4", "20", null)] = DirectorySyntaxType.TeletexString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.12", "64", null)] = DirectorySyntaxType.UnicodeString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.11", "23", null)] = DirectorySyntaxType.UTCTimeString;
		adSyntaxToDirectorySyntaxType[new AdSyntax("2.5.5.11", "24", null)] = DirectorySyntaxType.GeneralizedTimeString;
	}

	public void InitializeInstance(InstanceType instance, uint initialConnectionCount, uint maxConnectionCount)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "InitializeInstance: entering, instance={0}, init={1}, max={2}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null", initialConnectionCount, maxConnectionCount);
		CheckDisposed();
		ClassManager.SessionPool.InitializeInstance(instance, initialConnectionCount, maxConnectionCount);
		instanceToSyntaxMappingTableRWLock.EnterWriteLock();
		try
		{
			LdapAttrSyntaxTable ldapAttrSyntaxTable = new LdapAttrSyntaxTable();
			PreloadSyntaxCache(ldapAttrSyntaxTable);
			instanceToSyntaxMappingTable[instance] = ldapAttrSyntaxTable;
		}
		finally
		{
			instanceToSyntaxMappingTableRWLock.ExitWriteLock();
		}
	}

	public bool IsInstanceKnown(InstanceType instance)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "IsInstanceKnown: entering, instance={0}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null");
		CheckDisposed();
		return ClassManager.SessionPool.IsInstanceKnown(instance);
	}

	public void RemoveInstance(InstanceType instance)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RemoveInstance: entering, instance={0}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		LdapAttrSyntaxTable ldapAttrSyntaxTable = null;
		instanceToSyntaxMappingTableRWLock.EnterWriteLock();
		try
		{
			if (instanceToSyntaxMappingTable.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RemoveInstance: found syntax table for instance, removing");
				ldapAttrSyntaxTable = instanceToSyntaxMappingTable[instance];
				instanceToSyntaxMappingTable.Remove(instance);
			}
		}
		finally
		{
			instanceToSyntaxMappingTableRWLock.ExitWriteLock();
		}
		try
		{
			ClassManager.SessionPool.RemoveInstance(instance);
		}
		catch (Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RemoveInstance: got an exception from session pool for instance {0}, {1}", instance.InstanceName, ex.ToString());
			if (ldapAttrSyntaxTable != null)
			{
				instanceToSyntaxMappingTableRWLock.EnterWriteLock();
				try
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RemoveInstance: putting back the syntax table for {0}", instance.InstanceName);
					if (!instanceToSyntaxMappingTable.ContainsKey(instance))
					{
						instanceToSyntaxMappingTable[instance] = ldapAttrSyntaxTable;
						ldapAttrSyntaxTable = null;
					}
					else
					{
						ldapAttrSyntaxTable.Dispose();
						ldapAttrSyntaxTable = null;
					}
				}
				finally
				{
					instanceToSyntaxMappingTableRWLock.ExitWriteLock();
				}
			}
			throw;
		}
		ldapAttrSyntaxTable?.Dispose();
	}

	public Guid CreateObject(DirectoryObject obj, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "CreateObject: entering");
		CheckDisposed();
		if (obj == null)
		{
			throw new ArgumentNullException("obj");
		}
		ldapControlsOut = null;
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		DirectoryObjectIdentifier parentIdentity = obj.ParentIdentity;
		string rDN = obj.RDN;
		string objectClass = obj.ObjectClass;
		IList<DirectoryAttribute> attributes = obj.Attributes;
		if (parentIdentity == null || parentIdentity.Instance == null || (parentIdentity.ObjectReferenceProperty.Equals(Guid.Empty) && parentIdentity.ObjectDn == null) || (parentIdentity.ObjectReferenceProperty.Equals(Guid.Empty) && parentIdentity.ObjectDn.Length == 0))
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyParentForCreation);
		}
		if (rDN == null || rDN.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyRdnForCreation);
		}
		if (objectClass == null || objectClass.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyObjectClassForCreation);
		}
		InstanceType instance = parentIdentity.Instance;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "CreateObject: trying to create under {0}, instance {1}", parentIdentity.GetObjectRefOrDnAsString(), instance.InstanceName);
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "CreateObject: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			flag = true;
			string text = RetrieveRegularDn(parentIdentity, connectionAsCurrentUser, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), useShowDeletedControl: false);
			if (text == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "CreateObject: couldn't get parent DN for {0}", parentIdentity.GetObjectRefOrDnAsString());
				throw new DirectoryOperationException(AdwsStrings.CouldntFindParentObjectForCreation);
			}
			string text2 = MakeDnFromRdnAndParent(rDN, text);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "CreateObject: creating object with DN {0} and class {1}", text2, objectClass);
			AddRequest addRequest = new AddRequest(text2, objectClass);
			foreach (DirectoryAttribute item in attributes)
			{
				if (item == null || item.Name == null || item.Name.Length == 0 || item.Xmlns == null || item.Xmlns.Length == 0)
				{
					throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForCreation);
				}
				System.DirectoryServices.Protocols.DirectoryAttribute directoryAttribute = new System.DirectoryServices.Protocols.DirectoryAttribute();
				directoryAttribute.Name = item.Name;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "CreateObject: adding {0} to object {1} ", item.Name, text2);
				AddInValues(item, directoryAttribute);
				addRequest.Attributes.Add(directoryAttribute);
			}
			AddInControlsToRequest(ldapControlsIn, addRequest);
			flag = true;
			AddResponse addResponse = (AddResponse)connectionAsCurrentUser.SendRequest(addRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(addResponse, "CreateObject", text2, instance);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "CreateObject: created {0}", text2);
			PullOutControlsFromResponse(addResponse, out ldapControlsOut);
			Guid empty = Guid.Empty;
			try
			{
				flag = true;
				empty = RetrieveObjectGuid(text2, connectionAsCurrentUser, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), useShowDeletedControl: false);
			}
			catch (DirectoryException innerException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "CreateObject: couldn't retrieve GUID for {0}", text2);
				throw new ObjectCreatedButIdentityUnknownException(innerException);
			}
			if (empty.Equals(Guid.Empty))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "CreateObject: GUID for {0} is empty", text2);
				throw new ObjectCreatedButIdentityUnknownException();
			}
			return empty;
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public void DeleteObject(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "DeleteObject: entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		ldapControlsOut = null;
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		string text = MakeDnFromObjectId(objectId);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "DeleteObject: trying to delete {0}, instance {1}", text, instance.InstanceName);
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "DeleteObject: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "DeleteObject: trying to delete {0} on {1}", text, instance.InstanceName);
			DeleteRequest deleteRequest = new DeleteRequest(text);
			AddInControlsToRequest(ldapControlsIn, deleteRequest);
			flag = true;
			DeleteResponse deleteResponse = (DeleteResponse)connectionAsCurrentUser.SendRequest(deleteRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(deleteResponse, "DeleteObject", text, instance);
			PullOutControlsFromResponse(deleteResponse, out ldapControlsOut);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "DeleteObject: deleted {0}", text);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	private void MoveObjectInternal(DirectoryObjectIdentifier objectId, Guid newParentObjectReferenceProperty, string newParentDn, string newRdn, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject: entering");
		ldapControlsOut = null;
		string text = (newParentObjectReferenceProperty.Equals(Guid.Empty) ? newParentDn : DirectoryUtilities.MakeGuidDn(newParentObjectReferenceProperty));
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject: instance {0}", instance.InstanceName.ToString());
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "MoveObject: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			string text2;
			string text3;
			if (newRdn == null)
			{
				flag = true;
				text2 = RetrieveRegularDn(objectId, connectionAsCurrentUser, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), DirectoryUtilities.ContainsShowDeletedControl(ldapControlsIn));
				if (text2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "MoveObject (no rename): couldn't get DN for {0}", objectId.GetObjectRefOrDnAsString());
					throw new DirectoryOperationException(AdwsStrings.CouldntFindObjectForMove);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject (no rename): trying to move {0} under {1}", text2, text);
				text3 = DirectoryUtilities.GetRdnFromDn(text2, out var _);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject (no rename): DN of object being moved is {0}, RDN is {1}", text2, text3);
			}
			else
			{
				text2 = MakeDnFromObjectId(objectId);
				text3 = newRdn;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject (with rename): trying to move {0} under {1} and rename to {2}", text2, text, text3);
			}
			ModifyDNRequest modifyDNRequest = new ModifyDNRequest();
			modifyDNRequest.DistinguishedName = text2;
			modifyDNRequest.NewParentDistinguishedName = text;
			modifyDNRequest.DeleteOldRdn = true;
			modifyDNRequest.NewName = text3;
			AddInControlsToRequest(ldapControlsIn, modifyDNRequest);
			flag = true;
			ModifyDNResponse modifyDNResponse = (ModifyDNResponse)connectionAsCurrentUser.SendRequest(modifyDNRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(modifyDNResponse, "MoveObject", text2, instance);
			PullOutControlsFromResponse(modifyDNResponse, out ldapControlsOut);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject: moved {0}", text2);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public void MoveObject(DirectoryObjectIdentifier objectId, Guid newParentObjectReferenceProperty, string newRdn, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject(Guid): entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (newParentObjectReferenceProperty.Equals(Guid.Empty))
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyContainerForMove);
		}
		ldapControlsOut = null;
		MoveObjectInternal(objectId, newParentObjectReferenceProperty, null, newRdn, ldapControlsIn, out ldapControlsOut);
	}

	public void MoveObject(DirectoryObjectIdentifier objectId, string newParentDn, string newRdn, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MoveObject(DN): entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (newParentDn == null)
		{
			throw new ArgumentNullException("newParentDn");
		}
		if (newParentDn.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyContainerForMove);
		}
		ldapControlsOut = null;
		MoveObjectInternal(objectId, Guid.Empty, newParentDn, newRdn, ldapControlsIn, out ldapControlsOut);
	}

	public void RenameObject(DirectoryObjectIdentifier objectId, string newRDN, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RenameObject: entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (newRDN == null)
		{
			throw new ArgumentNullException("newRDN");
		}
		if (newRDN.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyRdnForRename);
		}
		ldapControlsOut = null;
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		string text = MakeDnFromObjectId(objectId);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RenameObject: trying to rename {0} with RDN {1}", text, newRDN);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RenameObject: instance {0}", instance.InstanceName.ToString());
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RenameObject: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			ModifyDNRequest modifyDNRequest = new ModifyDNRequest();
			modifyDNRequest.DistinguishedName = text;
			modifyDNRequest.DeleteOldRdn = true;
			modifyDNRequest.NewName = newRDN;
			AddInControlsToRequest(ldapControlsIn, modifyDNRequest);
			flag = true;
			ModifyDNResponse modifyDNResponse = (ModifyDNResponse)connectionAsCurrentUser.SendRequest(modifyDNRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(modifyDNResponse, "RenameObject", modifyDNRequest.DistinguishedName, instance);
			PullOutControlsFromResponse(modifyDNResponse, out ldapControlsOut);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RenameObject: renamed {0} to {1}", text, newRDN);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public void UpdateAttributes(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> operations, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		UpdateAttributes(objectId, operations, ldapControlsIn, usePermissiveModify: false, out ldapControlsOut);
	}

	public void UpdateAttributes(DirectoryObjectIdentifier objectId, IList<DirectoryAttributeOperation> operations, IList<DirectoryControl> ldapControlsIn, bool usePermissiveModify, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "UpdateAttributes: entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		if (operations == null)
		{
			throw new ArgumentNullException("operations");
		}
		ldapControlsOut = null;
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		string text = null;
		text = ((!IsRootDseIdentifier(objectId)) ? MakeDnFromObjectId(objectId) : "");
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "UpdateAttributes: {0} modifications for {1}, instance {2}", operations.Count, text, instance.InstanceName);
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "UpdateAttributes: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			ModifyRequest modifyRequest = new ModifyRequest();
			modifyRequest.DistinguishedName = text;
			foreach (DirectoryAttributeOperation operation in operations)
			{
				if (operation == null || operation.Name == null || operation.Name.Length == 0 || operation.Xmlns == null || operation.Xmlns.Length == 0)
				{
					throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForUpdate);
				}
				DirectoryAttributeModification directoryAttributeModification = new DirectoryAttributeModification();
				directoryAttributeModification.Name = operation.Name;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "UpdateAttributes: modifying attribute {0} (mod type={1})", operation.Name, operation.Operation.ToString());
				switch (operation.Operation)
				{
				case AttributeOperationType.RemoveAllValues:
					directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Delete;
					break;
				case AttributeOperationType.RemoveSpecifiedValues:
					AddInValues(operation, directoryAttributeModification);
					directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Delete;
					break;
				case AttributeOperationType.AddSpecifiedValues:
					AddInValues(operation, directoryAttributeModification);
					directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Add;
					break;
				case AttributeOperationType.ReplaceAllValues:
					AddInValues(operation, directoryAttributeModification);
					directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Replace;
					break;
				}
				modifyRequest.Modifications.Add(directoryAttributeModification);
			}
			if (usePermissiveModify)
			{
				modifyRequest.Controls.Add(new PermissiveModifyControl());
			}
			AddInControlsToRequest(ldapControlsIn, modifyRequest);
			flag = true;
			ModifyResponse modifyResponse = (ModifyResponse)connectionAsCurrentUser.SendRequest(modifyRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(modifyResponse, "UpdateAttributes", text, instance);
			PullOutControlsFromResponse(modifyResponse, out ldapControlsOut);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "UpdateAttributes: updated {0}", text);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public string GetObjectType(DirectoryObjectIdentifier objectId, bool useShowDeletedControl)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectType: entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		if (IsRootDseIdentifier(objectId))
		{
			return "top";
		}
		string text = MakeDnFromObjectId(objectId);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectType: trying to find object type for {0}, instance {1}", text, instance.InstanceName);
		string[] attributeValuesAsCurrentUserOrSystem = GetAttributeValuesAsCurrentUserOrSystem<string>(instance, text, operationTimeout, "objectClass", useShowDeletedControl);
		if (attributeValuesAsCurrentUserOrSystem != null)
		{
			string text2 = attributeValuesAsCurrentUserOrSystem[attributeValuesAsCurrentUserOrSystem.Length - 1];
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectType: found object type {0} for object {1}", text2, text);
			return text2;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectType: no object type for object {0}", text);
		return null;
	}

	public DirectorySyntaxType GetAttributeType(InstanceType instance, string ldapAttributeName)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: entering");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (ldapAttributeName == null)
		{
			throw new ArgumentNullException("ldapAttributeName");
		}
		if (ldapAttributeName.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForTypeLookup);
		}
		ldapAttributeName = ldapAttributeName.ToLower();
		ldapAttributeName = ldapAttributeName.Split(';')[0];
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: trying to find syntax for {0}, instance {1}", ldapAttributeName, instance.InstanceName);
		LdapAttrSyntaxTable ldapAttrSyntaxTable = null;
		if (!instanceToSyntaxMappingTableRWLock.TryEnterReadLock(operationTimeout))
		{
			throw new LdapException(85, AdwsStrings.OperationTimeout);
		}
		try
		{
			if (!instanceToSyntaxMappingTable.ContainsKey(instance))
			{
				throw new ArgumentException(AdwsStrings.NoSuchPool);
			}
			ldapAttrSyntaxTable = instanceToSyntaxMappingTable[instance];
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: found syntax table for instance {0}", instance.InstanceName);
		}
		finally
		{
			instanceToSyntaxMappingTableRWLock.ExitReadLock();
		}
		bool flag = false;
		DirectorySyntaxType directorySyntaxType = DirectorySyntaxType.Boolean;
		if (!ldapAttrSyntaxTable.AttrToSyntaxMappingRWLock.TryEnterReadLock(DirectoryUtilities.GetTimeRemaining(utcNow, operationTimeout)))
		{
			throw new LdapException(85, AdwsStrings.OperationTimeout);
		}
		try
		{
			if (ldapAttrSyntaxTable.AttrToSyntaxMapping.ContainsKey(ldapAttributeName))
			{
				LdapAttrSyntaxTableEntry ldapAttrSyntaxTableEntry = ldapAttrSyntaxTable.AttrToSyntaxMapping[ldapAttributeName];
				if (ldapAttrSyntaxTableEntry.RetrievalTime != DateTime.MaxValue && utcNow - ldapAttrSyntaxTableEntry.RetrievalTime > ConfigurationSettings.SyntaxCacheEntryLifetime)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: found too old entry for attr {0},  instance {1}", ldapAttributeName, instance.InstanceName);
					flag = false;
				}
				else
				{
					directorySyntaxType = ldapAttrSyntaxTableEntry.Syntax;
					flag = true;
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: found good entry for attr {0},  instance {1}, syntax {2}", ldapAttributeName, instance.InstanceName, directorySyntaxType);
				}
			}
		}
		finally
		{
			ldapAttrSyntaxTable.AttrToSyntaxMappingRWLock.ExitReadLock();
		}
		if (flag)
		{
			return directorySyntaxType;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: couldn't find entry (or too old) for attr {0},  instance {1}, querying", ldapAttributeName, instance.InstanceName);
		DirectorySyntaxType directorySyntaxType2 = RetrieveAttributeSyntaxType(instance, ldapAttributeName, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: returned syntax from query was {0}", directorySyntaxType2);
		if (!ldapAttrSyntaxTable.AttrToSyntaxMappingRWLock.TryEnterWriteLock(DirectoryUtilities.GetTimeRemaining(utcNow, operationTimeout)))
		{
			throw new LdapException(85, AdwsStrings.OperationTimeout);
		}
		try
		{
			if (ldapAttrSyntaxTable.AttrToSyntaxMapping.ContainsKey(ldapAttributeName))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: Updating entry: instance {0}, attr {1} --> {2}", instance.InstanceName, ldapAttributeName, directorySyntaxType2);
				LdapAttrSyntaxTableEntry ldapAttrSyntaxTableEntry2 = ldapAttrSyntaxTable.AttrToSyntaxMapping[ldapAttributeName];
				ldapAttrSyntaxTableEntry2.Syntax = directorySyntaxType2;
				ldapAttrSyntaxTableEntry2.RetrievalTime = DateTime.UtcNow;
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeType: Adding entry: instance {0}, attr {1} --> {2}", instance.InstanceName, ldapAttributeName, directorySyntaxType2);
				LdapAttrSyntaxTableEntry value = new LdapAttrSyntaxTableEntry(ldapAttributeName, directorySyntaxType2);
				ldapAttrSyntaxTable.AttrToSyntaxMapping[ldapAttributeName] = value;
			}
		}
		finally
		{
			ldapAttrSyntaxTable.AttrToSyntaxMappingRWLock.ExitWriteLock();
		}
		return directorySyntaxType2;
	}

	public DirectoryObject ReadObject(DirectoryObjectIdentifier objectId, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObject(DirObj): entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		ldapControlsOut = null;
		return ReadObjectInternal<DirectoryObject>(objectId, null, ldapControlsIn, out ldapControlsOut);
	}

	public DirectoryObjectFragment ReadObject(DirectoryObjectIdentifier objectId, IList<DirectoryAttribute> attrsToRetrieve, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObject(DirObjFrag): entering");
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
		return ReadObjectInternal<DirectoryObjectFragment>(objectId, attrsToRetrieve, ldapControlsIn, out ldapControlsOut);
	}

	private T ReadObjectInternal<T>(DirectoryObjectIdentifier objectId, IList<DirectoryAttribute> attrsToRetrieveOpt, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut) where T : IDirectoryObject, new()
	{
		ldapControlsOut = null;
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		string text = MakeDnFromObjectId(objectId);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObjectInternal: trying to read {0}, instance {1}", text, instance.InstanceName);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObjectInternal: {0} attribute(s) specified", (attrsToRetrieveOpt == null) ? "ALL" : attrsToRetrieveOpt.Count.ToString());
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "ReadObjectInternal: no connection available on instance {0} to read {1}", instance.InstanceName, text);
				throw new NoConnectionAvailableException();
			}
			bool flag3 = false;
			if (IsRootDseIdentifier(objectId))
			{
				text = "";
				flag3 = true;
			}
			SearchRequest searchRequest = new SearchRequest();
			searchRequest.DistinguishedName = text;
			searchRequest.Filter = "(objectClass=*)";
			searchRequest.Scope = SearchScope.Base;
			AddInControlsToRequest(ldapControlsIn, searchRequest);
			if (attrsToRetrieveOpt != null)
			{
				bool flag4 = false;
				bool flag5 = false;
				foreach (DirectoryAttribute item in attrsToRetrieveOpt)
				{
					if (item == null || item.Name == null || item.Name.Length == 0 || item.Xmlns == null || item.Xmlns.Length == 0)
					{
						throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForRead);
					}
					string name = item.Name;
					searchRequest.Attributes.Add(name);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObjectInternal: retrieving {0}", name);
					if (name.Equals("objectClass", StringComparison.InvariantCultureIgnoreCase))
					{
						flag4 = true;
					}
					if (name.Equals("objectGuid", StringComparison.InvariantCultureIgnoreCase))
					{
						flag5 = true;
					}
				}
				if (!flag4)
				{
					searchRequest.Attributes.Add("objectClass");
				}
				if (!flag5)
				{
					searchRequest.Attributes.Add("objectGuid");
				}
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReadObjectInternal: retrieving all attributes");
			}
			flag = true;
			SearchResponse searchResponse = (SearchResponse)connectionAsCurrentUser.SendRequest(searchRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "ReadObjectInternal", text, instance);
			PullOutControlsFromResponse(searchResponse, out ldapControlsOut);
			SearchResultEntry srchEntry = searchResponse.Entries[0];
			T val;
			if (!flag3)
			{
				val = BuildDirObjectFromSearchEntry<T>(instance, srchEntry);
			}
			else
			{
				val = new T
				{
					DistinguishedName = "",
					RDN = ""
				};
				DirectoryObjectIdentifier objectIdentity = new DirectoryObjectIdentifier(AdwsConstants.rootDSEGuid, instance);
				val.ObjectIdentity = objectIdentity;
				val.ObjectClass = "top";
			}
			PopulateDirObjectAttributesFromSearchEntry(instance, srchEntry, attrsToRetrieveOpt, val);
			return val;
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public DirectoryObjectIdentifier GetParentIdentifier(DirectoryObjectIdentifier objectId, bool useShowDeletedControl)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetParentIdentifier: entering");
		CheckDisposed();
		if (objectId == null)
		{
			throw new ArgumentNullException("objectId");
		}
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		InstanceType instance = objectId.Instance;
		if (IsRootDseIdentifier(objectId))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetParentIdentifier: rootDSE case, returning rootDSEGuid");
			return new DirectoryObjectIdentifier(AdwsConstants.rootDSEGuid, instance);
		}
		if (!objectId.IsObjectIdentifiedByDn() && objectId.ObjectReferenceProperty.Equals(Guid.Empty))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetParentIdentifier: empty objectGUID case, returning null");
			return null;
		}
		string text = MakeDnFromObjectId(objectId);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetParentIdentifier: trying to find parent for {0}, instance {1}", text, instance.InstanceName);
		byte[][] attributeValuesAsCurrentUserOrSystem = GetAttributeValuesAsCurrentUserOrSystem<byte[]>(instance, text, operationTimeout, "parentGUID", useShowDeletedControl);
		if (attributeValuesAsCurrentUserOrSystem != null && attributeValuesAsCurrentUserOrSystem[0] != null)
		{
			Guid objectReferenceProperty = new Guid(attributeValuesAsCurrentUserOrSystem[0]);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetParentIdentifier: found parent {0} for object {1}", objectReferenceProperty.ToString(), text);
			return new DirectoryObjectIdentifier(objectReferenceProperty, instance);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetParentIdentifier: no parent for object {0}", text);
		return null;
	}

	public Guid GetObjectRefPropertyFromDn(InstanceType instance, string dn, bool useShowDeletedControl)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectRefPropertyFromDn: entering");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (dn == null)
		{
			throw new ArgumentNullException("dn");
		}
		if (dn.Length == 0)
		{
			throw new ArgumentException(AdwsStrings.MustSpecifyDnForIdentifierLookup);
		}
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectRefPropertyFromDn: trying to lookup {0}, instance {1}", dn, instance.InstanceName);
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetObjectRefPropertyFromDn: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			flag = true;
			Guid result = RetrieveObjectGuid(dn, connectionAsCurrentUser, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), useShowDeletedControl);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetObjectRefPropertyFromDn: got GUID {0}", result.ToString());
			return result;
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	public LdapSession GetLdapSession(TimeSpan timeToWaitToAcquire, InstanceType instance, DataAccessNotifyOfTimeout notifyIfExpires)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetLdapSession: entering");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapSessionNotificationWrapper ldapSessionNotificationWrapper = null;
		SessionPoolNotifyOfTimeout routineToNotify = null;
		if (notifyIfExpires != null)
		{
			ldapSessionNotificationWrapper = new LdapSessionNotificationWrapper(notifyIfExpires);
			routineToNotify = ldapSessionNotificationWrapper.SessionPoolTimedOutCallback;
		}
		ReservationToken reservationToken = sessionPool.ReserveConnectionAsCurrentUser(instance, routineToNotify, timeToWaitToAcquire);
		if (reservationToken == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetLdapSession: couldn't get token in time for {0}", instance.InstanceName);
			return null;
		}
		LdapSession ldapSession = new LdapSession(instance, reservationToken);
		if (ldapSessionNotificationWrapper != null)
		{
			ldapSessionNotificationWrapper.AssociatedLdapSession = ldapSession;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetLdapSession: got session for {0}", instance.InstanceName);
		return ldapSession;
	}

	public void ReleaseLdapSession(LdapSession session)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReleaseLdapSession: entering");
		CheckDisposed();
		if (session == null)
		{
			throw new ArgumentNullException("session");
		}
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		lock (session)
		{
			if (!session.IsValid())
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "ReleaseLdapSession: session for {0} already invalid", session.Instance.InstanceName);
				return;
			}
			try
			{
				sessionPool.ReleaseReservation(session.Token);
			}
			catch (InvalidReservationTokenException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReleaseLdapSession: session for {0} was valid but had invalid token", session.Instance.InstanceName);
			}
			session.InvalidateSession();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "ReleaseLdapSession: released session for {0}", session.Instance.InstanceName);
	}

	public void PreparePageSearch(LdapQuery query)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "PreparePageSearch: entering");
		CheckDisposed();
		if (query == null)
		{
			throw new ArgumentNullException("query");
		}
		if (query.Mode != LdapQueryMode.Unprepared)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "PreparePageSearch: trying to prepare an already-prepared LdapQuery (.Mode={0})", query.Mode.ToString());
			throw new InvalidOperationException(AdwsStrings.CantPrepareAlreadyPreparedQuery);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "PreparePageSearch: set mode for LdapQuery to Paged");
		query.Mode = LdapQueryMode.Paged;
	}

	public IList<DirectoryObjectFragment> GetNextPageSearchResults(LdapQuery query, uint pageSize, TimeSpan serverTimeLimit, IList<DirectoryControl> ldapControlsIn, out IList<DirectoryControl> ldapControlsOut)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: entering");
		CheckDisposed();
		ldapControlsOut = null;
		if (query == null)
		{
			throw new ArgumentNullException("query");
		}
		if (pageSize == 0)
		{
			throw new ArgumentException(AdwsStrings.PageSizeMustBeGreaterThanZero);
		}
		if (query.Mode != LdapQueryMode.Paged)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: trying to use an unprepared LdapQuery (.Mode={0})", query.Mode.ToString());
			throw new InvalidOperationException(AdwsStrings.QueryMustBePreparedBeforeUse);
		}
		if (serverTimeLimit < TimeSpan.Zero)
		{
			throw new ArgumentException(AdwsStrings.ServerTimeMustBeNonNegative);
		}
		if (query.QueryFilter == null || query.AllRequestedAttributesToRetrieve == null || query.Instance == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: LdapQuery is missing key properties");
			throw new ArgumentException(AdwsStrings.BadQuery);
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		LdapSession session = query.Session;
		if (!session.IsValid())
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: no connection available (LdapSession invalid)");
			throw new NoConnectionAvailableException(AdwsStrings.ReservedConnectionInvalidated);
		}
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection ldapConnection = null;
		try
		{
			ldapConnection = sessionPool.GetReservedConnection(session.Token);
		}
		catch (InvalidReservationTokenException innerException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: no connection available (GetReservedConnection threw InvalidReservationTokenException)");
			throw new NoConnectionAvailableException(AdwsStrings.ReservedConnectionInvalidated, innerException);
		}
		try
		{
			if (ldapConnection == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: no connection available (ldapConn is null --- pool shutting down?)");
				throw new NoConnectionAvailableException(AdwsStrings.ReservedConnectionInvalidated);
			}
			LdapAdaptor ldapAdaptor = new LdapAdaptor(query.QueryFilter);
			string ldapFilter = ldapAdaptor.GetLdapFilter();
			string dnForRootOfSearch = ldapAdaptor.GetDnForRootOfSearch();
			SearchScope scope = ldapAdaptor.GetScope();
			if (ldapFilter == null || ldapFilter.Length == 0)
			{
				throw new ArgumentException(AdwsStrings.InvalidSearchFilterForQuery);
			}
			if (dnForRootOfSearch == null)
			{
				throw new ArgumentException(AdwsStrings.MustSpecifyBaseDnForQuery);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: filter is '{0}', base DN is '{1}', scope is '{2}'", ldapFilter, dnForRootOfSearch, scope.ToString());
			SearchRequest searchRequest = new SearchRequest();
			searchRequest.DistinguishedName = dnForRootOfSearch;
			searchRequest.Filter = ldapFilter;
			searchRequest.Scope = scope;
			TimeSpan timeSpan = DirectoryUtilities.GetTimeRemaining(utcNow, operationTimeout);
			if (serverTimeLimit < timeSpan)
			{
				timeSpan = serverTimeLimit;
			}
			searchRequest.TimeLimit = timeSpan;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: client-requested server time limit={0}, using {1}", serverTimeLimit.ToString(), timeSpan.ToString());
			AddInControlsToRequest(ldapControlsIn, searchRequest);
			string[] serverAttributesToRetrieve = query.GetServerAttributesToRetrieve();
			bool flag4 = false;
			bool flag5 = false;
			if (serverAttributesToRetrieve.Length != 0)
			{
				string[] array = serverAttributesToRetrieve;
				foreach (string text in array)
				{
					if (text == null || text.Length == 0)
					{
						throw new ArgumentException(AdwsStrings.MustSpecifyAttributeNameForQuery);
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: including attribute {0}", text);
					if (text.Equals("*", StringComparison.InvariantCultureIgnoreCase))
					{
						flag4 = true;
						flag5 = true;
						flag3 = true;
					}
					if (text.Equals("objectClass", StringComparison.InvariantCultureIgnoreCase))
					{
						flag4 = true;
					}
					if (text.Equals("objectGuid", StringComparison.InvariantCultureIgnoreCase))
					{
						flag5 = true;
					}
					searchRequest.Attributes.Add(text);
				}
			}
			if (!flag4)
			{
				searchRequest.Attributes.Add("objectClass");
			}
			if (!flag5)
			{
				searchRequest.Attributes.Add("objectGuid");
			}
			PageResultRequestControl pageResultRequestControl = new PageResultRequestControl();
			pageResultRequestControl.PageSize = (int)pageSize;
			pageResultRequestControl.IsCritical = true;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: using page size of {0}", pageSize);
			PageResultResponseControl pagedResponse = query.PagedResponse;
			if (pagedResponse != null && pagedResponse.Cookie != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: using paged cookie from previous response");
				pageResultRequestControl.Cookie = pagedResponse.Cookie;
			}
			searchRequest.Controls.Add(pageResultRequestControl);
			SortRequestControl sortRequestControl = null;
			if (query.SortKey != null)
			{
				if (query.SortKey.Name == null || query.SortKey.Name.Length == 0 || query.SortKey.Xmlns == null || query.SortKey.Xmlns.Length == 0)
				{
					throw new ArgumentException(AdwsStrings.MustSpecifySortAttributeNameForQuery);
				}
				string name = query.SortKey.Name;
				sortRequestControl = new SortRequestControl(name, !query.AscendingSort);
				sortRequestControl.IsCritical = true;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: sorting on {0} (ascending={1})", name, query.AscendingSort);
				searchRequest.Controls.Add(sortRequestControl);
			}
			PageResultResponseControl pageResultResponseControl = null;
			SearchResponse searchResponse = null;
			try
			{
				flag = true;
				searchResponse = (SearchResponse)ldapConnection.SendRequest(searchRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
				DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "GetNextPageSearchResults", dnForRootOfSearch, query.Instance);
			}
			finally
			{
				if (searchResponse != null)
				{
					pageResultResponseControl = DirectoryUtilities.FindSpecifiedControl<PageResultResponseControl>(searchResponse.Controls, "1.2.840.113556.1.4.319");
					if (pageResultResponseControl != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: storing paged cookie for next search (more results={0})", (pageResultResponseControl.Cookie.Length != 0) ? "true" : "false");
						query.PagedResponse = pageResultResponseControl;
					}
				}
			}
			PullOutControlsFromResponse(searchResponse, out var ldapControls);
			if (ldapControls != null)
			{
				if (pageResultResponseControl != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: removing paged response control from output controls");
					DirectoryControl item = DirectoryUtilities.FindSpecifiedControl<DirectoryControl>(ldapControls, "1.2.840.113556.1.4.319");
					ldapControls.Remove(item);
				}
				if (sortRequestControl != null)
				{
					DirectoryControl directoryControl = DirectoryUtilities.FindSpecifiedControl<DirectoryControl>(ldapControls, "1.2.840.113556.1.4.474");
					if (directoryControl != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: removing sort response control from output controls");
						ldapControls.Remove(directoryControl);
					}
				}
			}
			ldapControlsOut = null;
			if (ldapControls != null && ldapControls.Count > 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: returning {0} output controls", ldapControls.Count);
				ldapControlsOut = ldapControls;
			}
			if (searchResponse == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: Unexpected State. srchResp == null");
				throw new InvalidOperationException();
			}
			List<DirectoryObjectFragment> list = new List<DirectoryObjectFragment>(searchResponse.Entries.Count);
			foreach (SearchResultEntry entry in searchResponse.Entries)
			{
				DirectoryObjectFragment directoryObjectFragment = BuildDirObjectFromSearchEntry<DirectoryObjectFragment>(query.Instance, entry);
				PopulateDirObjectAttributesFromSearchEntry(query.Instance, entry, flag3 ? null : serverAttributesToRetrieve, directoryObjectFragment);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetNextPageSearchResults: returning DirectoryObjectFragment for {0}", directoryObjectFragment.DistinguishedName);
				list.Add(directoryObjectFragment);
			}
			return list;
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyReservedConnectionDown(session.Token);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (ldapConnection != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyReservedConnectionUp(session.Token);
				}
				sessionPool.ReleaseReservedConnection(session.Token);
				ldapConnection = null;
			}
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (disposed)
			{
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "Dispose: disposing...");
			if (instanceToSyntaxMappingTable != null)
			{
				foreach (KeyValuePair<InstanceType, LdapAttrSyntaxTable> item in instanceToSyntaxMappingTable)
				{
					item.Value.Dispose();
				}
			}
			if (instanceToSyntaxMappingTableRWLock != null)
			{
				instanceToSyntaxMappingTableRWLock.Dispose();
				instanceToSyntaxMappingTableRWLock = null;
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	public LdapQueryFilter GetDefaultLdapQueryFilter(InstanceType instance)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: Enter");
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		bool flag = false;
		bool flag2 = false;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		string text = null;
		SearchScope scope = SearchScope.Subtree;
		string filter = "(objectClass=*)";
		LdapQueryFilter result = null;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, operationTimeout);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			string[] attributeList = new string[1] { "defaultNamingContext" };
			SearchRequest request = new SearchRequest("", "(objectclass=*)", SearchScope.Base, attributeList);
			flag = true;
			SearchResponse searchResponse = (SearchResponse)connectionAsCurrentUser.SendRequest(request, operationTimeout);
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "GetDefaultLdapQueryFilter", "", instance);
			if (searchResponse.Entries.Count <= 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: couldn't retrieve rootDSE");
				throw new InvalidOperationException(AdwsStrings.CouldntRetrieveRootDSEForFilter);
			}
			SearchResultEntry searchResultEntry = searchResponse.Entries[0];
			if (searchResultEntry == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: couldn't retrieve rootDSE");
				throw new InvalidOperationException(AdwsStrings.CouldntRetrieveRootDSEForFilter);
			}
			text = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "defaultNamingContext");
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: DefaultNamingContext {0}", (text == null) ? string.Empty : text);
			if (text == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: couldn't retrieve DefaultNamingContext");
				throw new InvalidOperationException(AdwsStrings.NoDefaultNamingContextFoundForFilter);
			}
			result = new LdapQueryFilter(filter, scope, text);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetDefaultLdapQueryFilter: Exit");
		return result;
	}

	private void PreloadSyntaxCache(LdapAttrSyntaxTable lst)
	{
		lst.AttrToSyntaxMapping["configurationnamingcontext"] = new LdapAttrSyntaxTableEntry("configurationnamingcontext", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["currenttime"] = new LdapAttrSyntaxTableEntry("currenttime", DirectorySyntaxType.GeneralizedTimeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["defaultnamingcontext"] = new LdapAttrSyntaxTableEntry("defaultnamingcontext", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dnshostname"] = new LdapAttrSyntaxTableEntry("dnshostname", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dsschemaattrcount"] = new LdapAttrSyntaxTableEntry("dsschemaattrcount", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dsschemaclasscount"] = new LdapAttrSyntaxTableEntry("dsschemaclasscount", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dsschemaprefixcount"] = new LdapAttrSyntaxTableEntry("dsschemaprefixcount", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dsservicename"] = new LdapAttrSyntaxTableEntry("dsservicename", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["highestcommittedusn"] = new LdapAttrSyntaxTableEntry("highestcommittedusn", DirectorySyntaxType.LargeInteger, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["isglobalcatalogready"] = new LdapAttrSyntaxTableEntry("isglobalcatalogready", DirectorySyntaxType.Boolean, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["issynchronized"] = new LdapAttrSyntaxTableEntry("issynchronized", DirectorySyntaxType.Boolean, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["ldapservicename"] = new LdapAttrSyntaxTableEntry("ldapservicename", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["namingcontexts"] = new LdapAttrSyntaxTableEntry("namingcontexts", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["pendingpropagations"] = new LdapAttrSyntaxTableEntry("pendingpropagations", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["rootdomainnamingcontext"] = new LdapAttrSyntaxTableEntry("rootdomainnamingcontext", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["schemanamingcontext"] = new LdapAttrSyntaxTableEntry("schemanamingcontext", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["servername"] = new LdapAttrSyntaxTableEntry("servername", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["subschemasubentry"] = new LdapAttrSyntaxTableEntry("subschemasubentry", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedcapabilities"] = new LdapAttrSyntaxTableEntry("supportedcapabilities", DirectorySyntaxType.ObjectIdentifier, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedcontrol"] = new LdapAttrSyntaxTableEntry("supportedcontrol", DirectorySyntaxType.ObjectIdentifier, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedldappolicies"] = new LdapAttrSyntaxTableEntry("supportedldappolicies", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedldapversion"] = new LdapAttrSyntaxTableEntry("supportedldapversion", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedsaslmechanisms"] = new LdapAttrSyntaxTableEntry("supportedsaslmechanisms", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["domaincontrollerfunctionality"] = new LdapAttrSyntaxTableEntry("domaincontrollerfunctionality", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["domainfunctionality"] = new LdapAttrSyntaxTableEntry("domainfunctionality", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["forestfunctionality"] = new LdapAttrSyntaxTableEntry("forestfunctionality", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-replallinboundneighbors"] = new LdapAttrSyntaxTableEntry("msds-replallinboundneighbors", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-replalloutboundneighbors"] = new LdapAttrSyntaxTableEntry("msds-replalloutboundneighbors", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-replconnectionfailures"] = new LdapAttrSyntaxTableEntry("msds-replconnectionfailures", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-repllinkfailures"] = new LdapAttrSyntaxTableEntry("msds-repllinkfailures", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-replpendingops"] = new LdapAttrSyntaxTableEntry("msds-replpendingops", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-replqueuestatistics"] = new LdapAttrSyntaxTableEntry("msds-replqueuestatistics", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-topquotausage"] = new LdapAttrSyntaxTableEntry("msds-topquotausage", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedconfigurablesettings"] = new LdapAttrSyntaxTableEntry("supportedconfigurablesettings", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["supportedextension"] = new LdapAttrSyntaxTableEntry("supportedextension", DirectorySyntaxType.ObjectIdentifier, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["validfsmos"] = new LdapAttrSyntaxTableEntry("validfsmos", DirectorySyntaxType.DSDNString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dsaversionstring"] = new LdapAttrSyntaxTableEntry("dsaversionstring", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-portldap"] = new LdapAttrSyntaxTableEntry("msds-portldap", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-portssl"] = new LdapAttrSyntaxTableEntry("msds-portssl", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["msds-principalname"] = new LdapAttrSyntaxTableEntry("msds-principalname", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["serviceaccountinfo"] = new LdapAttrSyntaxTableEntry("serviceaccountinfo", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["spnregistrationresult"] = new LdapAttrSyntaxTableEntry("spnregistrationresult", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["tokengroups"] = new LdapAttrSyntaxTableEntry("tokengroups", DirectorySyntaxType.SidString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["usnatrifm"] = new LdapAttrSyntaxTableEntry("usnatrifm", DirectorySyntaxType.LargeInteger, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["becomepdcwithcheckpoint"] = new LdapAttrSyntaxTableEntry("becomepdcwithcheckpoint", DirectorySyntaxType.SidString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["checkphantoms"] = new LdapAttrSyntaxTableEntry("checkphantoms", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dogarbagecollection"] = new LdapAttrSyntaxTableEntry("dogarbagecollection", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dumpdatabase"] = new LdapAttrSyntaxTableEntry("dumpdatabase", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["fixupinheritance"] = new LdapAttrSyntaxTableEntry("fixupinheritance", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["invalidateridpool"] = new LdapAttrSyntaxTableEntry("invalidateridpool", DirectorySyntaxType.SidString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["recalchierarchy"] = new LdapAttrSyntaxTableEntry("recalchierarchy", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["schemaupdatenow"] = new LdapAttrSyntaxTableEntry("schemaupdatenow", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["removelingeringobject"] = new LdapAttrSyntaxTableEntry("removelingeringobject", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dolinkcleanup"] = new LdapAttrSyntaxTableEntry("dolinkcleanup", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["doonlinedefrag"] = new LdapAttrSyntaxTableEntry("doonlinedefrag", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["replicatesingleobject"] = new LdapAttrSyntaxTableEntry("replicatesingleobject", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["updatecachedmemberships"] = new LdapAttrSyntaxTableEntry("updatecachedmemberships", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["dogarbagecollectionphantomsnow"] = new LdapAttrSyntaxTableEntry("dogarbagecollectionphantomsnow", DirectorySyntaxType.Integer, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["invalidategcconnection"] = new LdapAttrSyntaxTableEntry("invalidategcconnection", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["renewservercertificate"] = new LdapAttrSyntaxTableEntry("renewservercertificate", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["rodcpurgeaccount"] = new LdapAttrSyntaxTableEntry("rodcpurgeaccount", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["sqmrunonce"] = new LdapAttrSyntaxTableEntry("sqmrunonce", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
		lst.AttrToSyntaxMapping["runprotectadmingroupstask"] = new LdapAttrSyntaxTableEntry("runprotectadmingroupstask", DirectorySyntaxType.UnicodeString, DateTime.MaxValue);
	}

	private string MakeDnFromRdnAndParent(string rdn, string parentDn)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(rdn);
		stringBuilder.Append(",");
		stringBuilder.Append(parentDn);
		return stringBuilder.ToString();
	}

	private string GetDnAndGuidFromExtendedDn(string extendedDn, out Guid objectGuid)
	{
		Match match = AdwsConstants.extendedDnRegex.Match(extendedDn);
		Group obj = match.Groups["dn"];
		Group obj2 = match.Groups["guid"];
		if (!obj.Success || !obj2.Success)
		{
			throw new ArgumentException(AdwsStrings.InvalidExtendedDnString);
		}
		Guid guid;
		try
		{
			guid = new Guid(obj2.Value);
		}
		catch (FormatException innerException)
		{
			throw new ArgumentException(AdwsStrings.InvalidExtendedDnString, innerException);
		}
		objectGuid = guid;
		return obj.Value;
	}

	private string RetrieveRegularDn(DirectoryObjectIdentifier objectId, LdapConnection ldapConn, TimeSpan timeAllowed, bool useShowDeletedControl)
	{
		SearchRequest searchRequest = new SearchRequest(MakeDnFromObjectId(objectId), "(objectclass=*)", SearchScope.Base, "distinguishedName");
		if (useShowDeletedControl)
		{
			searchRequest.Controls.Add(new ShowDeletedControl());
		}
		SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(searchRequest, timeAllowed);
		if (searchResponse.Entries.Count > 0)
		{
			return DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse.Entries[0], "distinguishedName");
		}
		return null;
	}

	private Guid RetrieveObjectGuid(string dn, LdapConnection ldapConn, TimeSpan timeAllowed, bool useShowDeletedControl)
	{
		SearchRequest searchRequest = new SearchRequest(dn, "(objectclass=*)", SearchScope.Base, "objectGuid");
		if (useShowDeletedControl)
		{
			searchRequest.Controls.Add(new ShowDeletedControl());
		}
		SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(searchRequest, timeAllowed);
		if (searchResponse.Entries.Count > 0)
		{
			byte[] singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(searchResponse.Entries[0], "objectGuid");
			if (singleValuedAttrVal != null)
			{
				return new Guid(singleValuedAttrVal);
			}
		}
		return Guid.Empty;
	}

	private string RetrieveSchemaNcDc(LdapConnection ldapConn, TimeSpan timeAllowed)
	{
		SearchRequest request = new SearchRequest(null, "(objectclass=*)", SearchScope.Base, "schemaNamingContext");
		SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(request, timeAllowed);
		if (searchResponse.Entries.Count > 0)
		{
			return DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse.Entries[0], "schemaNamingContext");
		}
		return null;
	}

	private string MakeDnFromObjectId(DirectoryObjectIdentifier objectId)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MakeDnFromObjectId: entering");
		string text;
		if (objectId.IsObjectIdentifiedByDn())
		{
			text = objectId.ObjectDn;
			if (text.Length == 0)
			{
				throw new ArgumentException(AdwsStrings.InvalidObjectReferenceProperty);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MakeDnFromObjectId: using DN {0}", text);
		}
		else
		{
			Guid objectReferenceProperty = objectId.ObjectReferenceProperty;
			if (objectReferenceProperty.Equals(Guid.Empty))
			{
				throw new ArgumentException(AdwsStrings.InvalidObjectReferenceProperty);
			}
			text = DirectoryUtilities.MakeGuidDn(objectReferenceProperty);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "MakeDnFromObjectId: using GUID {0}", objectReferenceProperty.ToString());
		}
		return text;
	}

	private bool IsRootDseIdentifier(DirectoryObjectIdentifier objectId)
	{
		if (!objectId.IsObjectIdentifiedByDn() && objectId.ObjectReferenceProperty.Equals(AdwsConstants.rootDSEGuid))
		{
			return true;
		}
		return false;
	}

	private void AddInValues(DirectoryAttribute attr, System.DirectoryServices.Protocols.DirectoryAttribute sdspDirAttr)
	{
		DirectorySyntaxType syntax = attr.Syntax;
		foreach (DirectoryValue attrValue in attr.AttrValues)
		{
			if (attrValue == null || attrValue.Value == null)
			{
				throw new ArgumentException(AdwsStrings.MustSpecifyNonnullAttrValue);
			}
			switch (syntax)
			{
			case DirectorySyntaxType.ORName:
			case DirectorySyntaxType.DSDNString:
				if (AdwsConstants.guidRegex.IsMatch((string)attrValue.Value))
				{
					sdspDirAttr.Add(DirectoryUtilities.MakeGuidDn((string)attrValue.Value));
				}
				else
				{
					sdspDirAttr.Add((string)attrValue.Value);
				}
				break;
			case DirectorySyntaxType.AccessPoint:
			case DirectorySyntaxType.DNString:
			case DirectorySyntaxType.DNBinary:
			{
				Match match = (syntax switch
				{
					DirectorySyntaxType.DNBinary => AdwsConstants.dnWithBinaryRegex, 
					DirectorySyntaxType.DNString => AdwsConstants.dnWithStringRegex, 
					_ => AdwsConstants.dnAccessPointRegex, 
				}).Match((string)attrValue.Value);
				Group obj = match.Groups["prefix"];
				Group obj2 = match.Groups["dn"];
				if (!obj.Success || !obj2.Success)
				{
					throw new ArgumentException(AdwsStrings.InvalidDnWithStringBinaryAccessPointValue);
				}
				string value = string.Concat(str1: (!AdwsConstants.guidRegex.IsMatch(obj2.Value)) ? obj2.Value : DirectoryUtilities.MakeGuidDn(obj2.Value), str0: obj.Value);
				sdspDirAttr.Add(value);
				break;
			}
			default:
				if (attrValue.Value is string)
				{
					sdspDirAttr.Add((string)attrValue.Value);
				}
				else
				{
					sdspDirAttr.Add((byte[])attrValue.Value);
				}
				break;
			}
		}
	}

	private void AddInControlsToRequest(IList<DirectoryControl> ldapControls, DirectoryRequest dirReq)
	{
		if (ldapControls == null || ldapControls.Count <= 0)
		{
			return;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "AddInControlsToRequest: adding {0} controls to {1}", ldapControls.Count, dirReq.GetType().ToString());
		foreach (DirectoryControl ldapControl in ldapControls)
		{
			if (ldapControl.Type.Equals("1.2.840.113556.1.4.528") || ldapControl.Type.Equals("1.2.840.113556.1.4.1907"))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "AddInControlsToRequest: rejecting disallowed control");
				throw new ArgumentException(AdwsStrings.NotificationSearchControlNotAllowed);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "AddInControlsToRequest: adding type={0}, criticality={1}", ldapControl.Type, ldapControl.IsCritical);
			dirReq.Controls.Add(ldapControl);
		}
	}

	private void PullOutControlsFromResponse(DirectoryResponse dirResp, out IList<DirectoryControl> ldapControls)
	{
		ldapControls = null;
		if (dirResp.Controls.Length == 0)
		{
			return;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "PullOutControlsFromResponse: extracting {0} controls from {1}", dirResp.Controls.Length, dirResp.GetType().ToString());
		if (GlobalDebug.Info)
		{
			DirectoryControl[] controls = dirResp.Controls;
			foreach (DirectoryControl directoryControl in controls)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "PullOutControlsFromResponse: extracting type={0}, criticality={1}", directoryControl.Type, directoryControl.IsCritical);
			}
		}
		ldapControls = new List<DirectoryControl>(dirResp.Controls);
	}

	private T BuildDirObjectFromSearchEntry<T>(InstanceType instance, SearchResultEntry srchEntry) where T : IDirectoryObject, new()
	{
		string distinguishedName = srchEntry.DistinguishedName;
		T result = new T();
		string trimmedDn;
		string rdnFromDn = DirectoryUtilities.GetRdnFromDn(distinguishedName, out trimmedDn);
		string[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<string>(srchEntry, "objectClass");
		string text;
		if (multiValuedAttrVal == null || multiValuedAttrVal.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "BuildDirObjectFromSearchEntry: didn't get object class for {0} (instance {1}), using 'top' instead", distinguishedName, instance.InstanceName);
			text = "top";
		}
		else
		{
			text = multiValuedAttrVal[multiValuedAttrVal.Length - 1];
		}
		byte[] singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(srchEntry, "objectGuid");
		Guid objectReferenceProperty;
		if (singleValuedAttrVal == null || singleValuedAttrVal.Length != 16)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "BuildDirObjectFromSearchEntry: didn't get object GUID for {0} (instance {1}), using empty GUID instead", distinguishedName, instance.InstanceName);
			objectReferenceProperty = Guid.Empty;
		}
		else
		{
			objectReferenceProperty = new Guid(singleValuedAttrVal);
		}
		result.DistinguishedName = distinguishedName;
		result.RDN = rdnFromDn;
		DirectoryObjectIdentifier objectIdentity = new DirectoryObjectIdentifier(objectReferenceProperty, instance);
		result.ObjectIdentity = objectIdentity;
		result.ObjectClass = text;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "BuildDirObjectFromSearchEntry: Retrieved DN={0}, RDN={1}, ObjectGUID={2}, class={3}", distinguishedName, rdnFromDn, objectReferenceProperty.ToString(), text);
		return result;
	}

	private void PopulateDirObjectAttributesFromSearchEntry<T>(InstanceType instance, SearchResultEntry srchEntry, IList<DirectoryAttribute> attrsToRetrieveOpt, T dirObjToPopulate) where T : IDirectoryObject
	{
		string[] array = null;
		if (attrsToRetrieveOpt != null)
		{
			array = new string[attrsToRetrieveOpt.Count];
			for (int i = 0; i < attrsToRetrieveOpt.Count; i++)
			{
				array[i] = attrsToRetrieveOpt[i].Name;
			}
		}
		PopulateDirObjectAttributesFromSearchEntry(instance, srchEntry, array, dirObjToPopulate);
	}

	private void PopulateDirObjectAttributesFromSearchEntry<T>(InstanceType instance, SearchResultEntry srchEntry, string[] attrsToRetrieveArrayOpt, T dirObjToPopulate) where T : IDirectoryObject
	{
		foreach (System.DirectoryServices.Protocols.DirectoryAttribute value2 in srchEntry.Attributes.Values)
		{
			bool flag;
			if (attrsToRetrieveArrayOpt != null)
			{
				flag = false;
				foreach (string text in attrsToRetrieveArrayOpt)
				{
					string text2 = text.Split(';')[0];
					string value = value2.Name.Split(';')[0];
					if (text2.Equals(value, StringComparison.InvariantCultureIgnoreCase) || text.Equals(AdwsConstants.star, StringComparison.InvariantCultureIgnoreCase))
					{
						flag = true;
						break;
					}
				}
			}
			else
			{
				flag = true;
			}
			if (!flag)
			{
				continue;
			}
			DirectoryAttribute directoryAttribute2 = new DirectoryAttribute();
			directoryAttribute2.Name = value2.Name;
			directoryAttribute2.Xmlns = AdwsConstants.AddataNs;
			try
			{
				directoryAttribute2.Syntax = GetAttributeType(instance, directoryAttribute2.Name);
			}
			catch (UnknownAttributeException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "PopulateDirObjectAttributesFromSearchEntry: attribute {0} not found", value2.Name);
				throw;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "PopulateDirObjectAttributesFromSearchEntry: including attribute={0} syntax={1} in returned object", value2.Name, directoryAttribute2.Syntax);
			object[] array2;
			try
			{
				if (directoryAttribute2.Syntax.Equals(DirectorySyntaxType.OctetString) || directoryAttribute2.Syntax.Equals(DirectorySyntaxType.ReplicaLink) || directoryAttribute2.Syntax.Equals(DirectorySyntaxType.SidString) || directoryAttribute2.Syntax.Equals(DirectorySyntaxType.NTSecurityDescriptor))
				{
					object[] array = (byte[][])value2.GetValues(typeof(byte[]));
					array2 = array;
				}
				else
				{
					array2 = new object[value2.Count];
					for (int j = 0; j < value2.Count; j++)
					{
						array2[j] = value2[j];
					}
				}
			}
			catch (NotSupportedException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "PopulateDirObjectAttributesFromSearchEntry: Error while fetching values for attribute={0} syntax={1} in returned object", value2.Name, directoryAttribute2.Syntax);
				throw new UnknownAttributeException(value2.Name, AdwsStrings.ErrorWhileFetchingAttributeValues);
			}
			foreach (object obj in array2)
			{
				DirectoryValue item = ((!(obj is string)) ? new DirectoryValue((byte[])obj) : new DirectoryValue((string)obj));
				directoryAttribute2.AttrValues.Add(item);
			}
			dirObjToPopulate.Attributes.Add(directoryAttribute2);
		}
	}

	private T[] GetAttributeValuesAsCurrentUserOrSystem<T>(InstanceType instance, string dn, TimeSpan timeAllowed, string attrName, bool useShowDeletedControl) where T : class
	{
		DateTime utcNow = DateTime.UtcNow;
		T[] array = null;
		if (instance.DirectoryType == DirectoryType.NTDS || instance.DirectoryType == DirectoryType.GC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUserOrSystem: instance type is {0}, using system account", instance.DirectoryType.ToString());
			array = GetAttributeValuesAsSystem<T>(instance, dn, timeAllowed, attrName, useShowDeletedControl);
		}
		else
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUserOrSystem: instance type is {0}, trying user account", instance.DirectoryType.ToString());
			try
			{
				array = GetAttributeValuesAsCurrentUser<T>(instance, dn, timeAllowed, attrName, useShowDeletedControl);
			}
			catch (DirectoryOperationException ex)
			{
				if (ex.Response == null || ex.Response.ResultCode != ResultCode.NoSuchObject)
				{
					throw;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUserOrSystem: got no such object");
				array = null;
			}
			catch (NoConnectionAvailableException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUserOrSystem: got NoConnectionAvailableException");
				array = null;
			}
			if (array == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUserOrSystem: falling back to system account");
				array = GetAttributeValuesAsSystem<T>(instance, dn, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed), attrName, useShowDeletedControl);
			}
		}
		return array;
	}

	private T[] GetAttributeValuesAsCurrentUser<T>(InstanceType instance, string dn, TimeSpan timeAllowed, string attrName, bool useShowDeletedControl) where T : class
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUser: looking for {0} on {1}, instance {2}", attrName, dn, instance.InstanceName);
		bool flag = false;
		bool flag2 = false;
		DateTime utcNow = DateTime.UtcNow;
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, timeAllowed);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUser: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			SearchRequest searchRequest = new SearchRequest(dn, "(objectClass=*)", SearchScope.Base, attrName);
			if (useShowDeletedControl)
			{
				searchRequest.Controls.Add(new ShowDeletedControl());
			}
			flag = true;
			SearchResponse searchResponse = (SearchResponse)connectionAsCurrentUser.SendRequest(searchRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed));
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "GetAttributeValuesAsCurrentUser", dn, instance);
			if (searchResponse.Entries.Count > 0)
			{
				T[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<T>(searchResponse.Entries[0], attrName);
				if (multiValuedAttrVal != null)
				{
					return multiValuedAttrVal;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUser: no attribute {0} for object {1}", attrName, dn);
				return null;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsCurrentUser: no entry for object {0}", dn);
			return null;
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	private T[] GetAttributeValuesAsSystem<T>(InstanceType instance, string dn, TimeSpan timeAllowed, string attrName, bool useShowDeletedControl) where T : class
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "GetAttributeValuesAsSystem: looking for {0} on {1}, instance {2}", attrName, dn, instance.InstanceName);
		DateTime utcNow = DateTime.UtcNow;
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		UtilityConnection utilityConnection = sessionPool.GetUtilityConnection(instance);
		LdapConnection ldapConn = utilityConnection.LdapConn;
		try
		{
			SearchRequest searchRequest = new SearchRequest(dn, "(objectClass=*)", SearchScope.Base, attrName);
			if (useShowDeletedControl)
			{
				searchRequest.Controls.Add(new ShowDeletedControl());
			}
			SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(searchRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed));
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "GetAttributeValuesAsSystem", dn, instance);
			if (searchResponse.Entries.Count > 0)
			{
				T[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<T>(searchResponse.Entries[0], attrName);
				if (multiValuedAttrVal != null)
				{
					return multiValuedAttrVal;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsSystem: no attribute {0} for object {1}", attrName, dn);
				return null;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "GetAttributeValuesAsSystem: no entry for object {0}", dn);
			return null;
		}
		finally
		{
			sessionPool.ReleaseUtilityConnection(utilityConnection);
			utilityConnection = null;
			ldapConn = null;
		}
	}

	private DirectorySyntaxType RetrieveAttributeSyntaxType(InstanceType instance, string ldapAttrName, TimeSpan timeAllowed)
	{
		DateTime utcNow = DateTime.UtcNow;
		DirectorySyntaxType result = DirectorySyntaxType.Boolean;
		bool flag = false;
		if (instance.DirectoryType == DirectoryType.NTDS || instance.DirectoryType == DirectoryType.GC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: instance type is {0}, using system account", instance.DirectoryType.ToString());
			InstanceType instanceType = instance;
			if (instance.DirectoryType == DirectoryType.GC)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: substituting DC for GC instance for schema retrieval");
				instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(AdwsConstants.LdapInstancePrefix + AdwsConstants.AdLdapPort);
				if (instanceType == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: no DC instance so can't look up GC schema");
					throw new UnknownAttributeException(null, AdwsStrings.NoDCInstanceForGCSchemaLookup);
				}
			}
			result = RetrieveAttributeSyntaxTypeAsSystem(instanceType, ldapAttrName, timeAllowed);
			flag = true;
		}
		else
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: instance type is {0}, trying user account", instance.DirectoryType.ToString());
			try
			{
				result = RetrieveAttributeSyntaxTypeAsCurrentUser(instance, ldapAttrName, timeAllowed);
				flag = true;
			}
			catch (UnknownAttributeException)
			{
				flag = false;
			}
			catch (NoConnectionAvailableException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: got NoConnectionAvailableException");
				flag = false;
			}
			if (!flag)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxType: falling back to system account");
				result = RetrieveAttributeSyntaxTypeAsSystem(instance, ldapAttrName, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed));
				flag = true;
			}
		}
		return result;
	}

	private DirectorySyntaxType RetrieveAttributeSyntaxTypeAsCurrentUser(InstanceType instance, string ldapAttrName, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeAsCurrentUser: entering, attr is {0} on instance {1}", ldapAttrName, instance.InstanceName);
		bool flag = false;
		bool flag2 = false;
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		LdapConnection connectionAsCurrentUser = sessionPool.GetConnectionAsCurrentUser(instance, timeAllowed);
		try
		{
			if (connectionAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeAsCurrentUser: no connection available on instance {0}", instance.InstanceName);
				throw new NoConnectionAvailableException();
			}
			flag = true;
			return RetrieveAttributeSyntaxTypeOnConnection(connectionAsCurrentUser, instance, ldapAttrName, timeAllowed);
		}
		catch (LdapException ex)
		{
			if (ex.ErrorCode == 81)
			{
				sessionPool.NotifyConnectionDown(connectionAsCurrentUser);
				flag2 = true;
			}
			throw;
		}
		finally
		{
			if (connectionAsCurrentUser != null)
			{
				if (flag && !flag2)
				{
					sessionPool.NotifyConnectionUp(connectionAsCurrentUser);
				}
				sessionPool.ReleaseConnection(connectionAsCurrentUser);
				connectionAsCurrentUser = null;
			}
		}
	}

	private DirectorySyntaxType RetrieveAttributeSyntaxTypeAsSystem(InstanceType instance, string ldapAttrName, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeAsSystem: entering, attr is {0} on instance {1}", ldapAttrName, instance.InstanceName);
		ILdapSessionPool sessionPool = ClassManager.SessionPool;
		UtilityConnection utilityConnection = sessionPool.GetUtilityConnection(instance);
		LdapConnection ldapConn = utilityConnection.LdapConn;
		try
		{
			return RetrieveAttributeSyntaxTypeOnConnection(ldapConn, instance, ldapAttrName, timeAllowed);
		}
		finally
		{
			sessionPool.ReleaseUtilityConnection(utilityConnection);
			utilityConnection = null;
			ldapConn = null;
		}
	}

	private DirectorySyntaxType RetrieveAttributeSyntaxTypeOnConnection(LdapConnection ldapConn, InstanceType instance, string ldapAttrName, TimeSpan timeAllowed)
	{
		DateTime utcNow = DateTime.UtcNow;
		string text = RetrieveSchemaNcDc(ldapConn, timeAllowed);
		if (text != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeOnConnection: schema NC is {0}", text);
			SearchRequest request = new SearchRequest(text, "(&(objectClass=attributeSchema)(ldapDisplayName=" + ldapAttrName + "))", SearchScope.Subtree, "attributeSyntax", "oMSyntax", "oMObjectClass");
			SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(request, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed));
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "RetrieveAttributeSyntaxTypeOnConnection", ldapAttrName, instance);
			if (searchResponse.Entries.Count > 0)
			{
				SearchResultEntry srchEntry = searchResponse.Entries[0];
				string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(srchEntry, "attributeSyntax");
				string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(srchEntry, "oMSyntax");
				byte[] singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(srchEntry, "oMObjectClass");
				if (singleValuedAttrVal == null || singleValuedAttrVal2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeOnConnection: couldn't retrieve schema attrs for attr {0} on instance {1}", ldapAttrName, instance.InstanceName);
					throw new UnknownAttributeException(ldapAttrName);
				}
				AdSyntax key = new AdSyntax(singleValuedAttrVal, singleValuedAttrVal2, singleValuedAttrVal3);
				if (adSyntaxToDirectorySyntaxType.ContainsKey(key))
				{
					return adSyntaxToDirectorySyntaxType[key];
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeOnConnection: unknown syntax type, attr is {0} on instance {1}", ldapAttrName, instance.InstanceName);
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "DirectoryDataAccessImplementation", "RetrieveAttributeSyntaxTypeOnConnection: couldn't retrieve schema, attr is {0} on instance {1}", ldapAttrName, instance.InstanceName);
			}
		}
		throw new UnknownAttributeException(ldapAttrName);
	}
}
