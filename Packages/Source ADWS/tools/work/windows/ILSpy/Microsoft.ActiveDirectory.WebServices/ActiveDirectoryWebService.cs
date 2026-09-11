using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerSession, ValidateMustUnderstand = true, IgnoreExtensionDataObject = true, Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
internal class ActiveDirectoryWebService : IResourceFactory, IResource, IEnumeration, IActiveDirectoryAccountManagement, IActiveDirectoryTopologyManagementV1, IActiveDirectoryTopologyManagement, IDisposable
{
	internal enum RequestType
	{
		Create,
		Put,
		Get,
		Delete,
		Enumerate
	}

	private int numberOfActiveOperations;

	private bool disposed;

	private string localDnsDomainName;

	private object localDnsDomainNameLock = new object();

	public ActiveDirectoryWebService()
	{
		PerfCounters.OpenWebServiceSessions.Increment();
		PerfCounters.OpenWebServiceSessionsRate.Increment();
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	[IdentityManagementOperation]
	[ObjectIdAndInstanceInformation]
	public Message Create(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.CreateOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: {0}", request.ToString());
			}
			Message message = null;
			Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddRequest addRequest = null;
			try
			{
				VerifyImpersonationLevel();
				Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddResponse addResponse = null;
				IList<DirectoryControl> ldapControlsIn = null;
				IList<DirectoryControl> ldapControlsOut = null;
				DirectoryObjectIdentifier directoryObjectIdentifier = BuildObjectIdentifierFromTheRequest(request, RequestType.Create);
				if (MessageContainsIdentityManagementHeader(request))
				{
					try
					{
						addRequest = request.GetBody<Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddRequest>(new XmlTypeObjectSerializer(typeof(Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddRequest)));
					}
					catch (SerializationException ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: SerializationException {0}", ex.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex);
					}
					catch (InvalidOperationException ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: InvalidOperationException {0}", ex2.ToString());
						if (ex2.InnerException != null && ex2.InnerException is InvalidExpressionException)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: Expression {0} Not Valid", ((InvalidExpressionException)ex2.InnerException).Expression);
							throw FaultGenerator.GenerateCannotProcessFilterFault((InvalidExpressionException)ex2.InnerException);
						}
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex2);
					}
					catch (XmlException ex3)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: XmlException {0}", ex3.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex3);
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: It's a IMDA Create Request with a body.");
					if (addRequest.Controls != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: IMDA Request has {0} no. of controls present in the body.", addRequest.Controls.Length);
						ldapControlsIn = BuildListOfSDSPDirectoryControls(addRequest.Controls);
					}
					if (addRequest.AttributeTypeAndValues != null)
					{
						if (addRequest.AttributeTypeAndValues.Length > 100)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: No. of AttributeTypeAndValues {0} exceeds the allowed limit of {1}.", addRequest.AttributeTypeAndValues.Length, 100);
							throw FaultGenerator.GenerateEncodingLimitFault(EncodingLimitExceededDetailType.AttributeTypeAndValues);
						}
						if (!string.IsNullOrEmpty(addRequest.Dialect))
						{
							if (!DialectParser.IsDialectSupported(OperationType.Create, addRequest.Dialect))
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: Dialect {0} Not Supported", addRequest.Dialect);
								throw FaultGenerator.GenerateFragmentDialectNotSupportedFault(OperationType.Create);
							}
							DirectoryObject directoryObject;
							try
							{
								directoryObject = DialectParser.Create(addRequest.Dialect).ParseCreate(directoryObjectIdentifier.Instance, addRequest.AttributeTypeAndValues);
								DirectoryAttribute directoryAttribute = DirectoryUtilities.FindDirAttribute(directoryObject.Attributes, AdwsConstants.objectClass, AdwsConstants.AddataNs, RemoveIfFound: true, OneValueEnforced: true);
								if (directoryAttribute == null)
								{
									throw new ArgumentException(AdwsStrings.MustSpecifyObjectClassForCreation);
								}
								directoryObject.ObjectClass = directoryAttribute.AttrValues[0].Value.ToString();
							}
							catch (InvalidExpressionException ex4)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: Expression {0} Not Valid", ex4.Expression);
								throw FaultGenerator.GenerateCannotProcessFilterFault(ex4);
							}
							catch (CreateException e)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: AttributeTypeAndValue with bad value");
								throw FaultGenerator.GenerateUnwillingToPerformFault(e);
							}
							try
							{
								DirectoryObjectIdentifier directoryObjectIdentifier2 = ClassManager.DirectoryAction.Create(directoryObjectIdentifier.Instance, directoryObject, ldapControlsIn, out ldapControlsOut);
								ReferenceParametersType refParamtype = new ReferenceParametersType(directoryObjectIdentifier2.ObjectReferenceProperty, AdwsConstants.LdapInstancePrefix + directoryObjectIdentifier2.Instance.Port);
								string text = Utils.GetComputerDnsName();
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: fully qualified DNS host name of the computer: {0}", (text == null) ? "-" : text);
								if (text == null)
								{
									GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Create: Error while retrieving the fully qualified DNS host name of the computer, hence adding 'localhost' to the URI");
									text = AdwsConstants.DefaultDNSHostName;
								}
								addResponse = new Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddResponse(new Uri(AdwsConstants.WcfNetEndpointPrefix + text + AdwsConstants.WcfNetEndpointSuffix)?.ToString() + "/" + AdwsConstants.WcfWindowsEndpointPrefix + AdwsConstants.WcfResourceEndpoint, refParamtype);
							}
							catch (ObjectCreatedButIdentityUnknownException ex5)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: ObjectCreatedButIdentityUnknownException {0}", ex5.ToString());
								throw FaultGenerator.GenerateUnwillingToPerformFaultForIdentityUnknownException(ex5);
							}
							catch (InvalidOperationException ex6)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: InvalidOperationException {0}", ex6.ToString());
								throw FaultGenerator.GenerateUnwillingToPerformFaultForCreateObject(ex6);
							}
							message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/transfer/CreateResponse", addResponse, new ControlSerializer(ldapControlsOut, new XmlTypeObjectSerializer(typeof(Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.AddResponse))));
							message.Headers.RelatesTo = request.Headers.MessageId;
							if (GlobalDebug.Info)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: Response {0}", message.ToString());
							}
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Create: Exit");
							return message;
						}
						throw new ArgumentException(AdwsStrings.MissingDialect);
					}
					throw new ArgumentException(AdwsStrings.NoAttrTypeAndValsPresentInTheBody);
				}
				throw FaultGenerator.GenerateActionNotSupportedFault(AdwsStrings.ActionNotSupportedCreateDetail);
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: LdapException {0}", ex8.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex8);
			}
			catch (DirectoryOperationException ex9)
			{
				if (ex9.Response != null && ex9.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: DirectoryOperatonException(noSuchObject) {0}", ex9.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex9);
				}
				if (ex9.Response != null && ex9.Response.ResultCode == ResultCode.ObjectClassViolation)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: DirectoryOperatonException(ObjectClassViolation) {0}", ex9.ToString());
					throw FaultGenerator.GenerateInvalidRepresentationFault(ex9);
				}
				if (ex9.Response != null && ex9.Response.ResultCode == ResultCode.EntryAlreadyExists)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: DirectoryOperatonException(EntryAlreadyExists) {0}", ex9.ToString());
					throw FaultGenerator.GenerateEntryAlreadyExistsFault(ex9);
				}
				if (ex9.Response != null && ex9.Response.ResultCode == ResultCode.InsufficientAccessRights)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: DirectoryOperatonException(InsufficientAccessRights) {0}", ex9.ToString());
					throw FaultGenerator.GenerateAccessDeniedFault(ex9);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: DirectoryOperationException {0}", ex9.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex9);
			}
			catch (ArgumentException ex10)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: ArgumentException {0}", ex10.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex10);
			}
			catch (NoConnectionAvailableException ex11)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Create: NoConnectionAvailableException {0}", ex11.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex11);
			}
			catch (Exception ex12)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Create: Unhandled Exception {0}", ex12.ToString());
				throw;
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	[IdentityManagementOperation]
	[ObjectIdAndInstanceInformation]
	public Message Get(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.GetOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Get:request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: {0}", request.ToString());
			}
			Message message = null;
			BaseObjectSearchRequest baseObjectSearchRequest = null;
			Controls controls = null;
			try
			{
				VerifyImpersonationLevel();
				IList<DirectoryControl> ldapControlsIn = null;
				DirectoryObjectIdentifier directoryObjectIdentifier = BuildObjectIdentifierFromTheRequest(request, RequestType.Get);
				IList<DirectoryControl> ldapControlsOut;
				if (MessageContainsIdentityManagementHeader(request))
				{
					try
					{
						baseObjectSearchRequest = request.GetBody<BaseObjectSearchRequest>(new XmlTypeObjectSerializer(typeof(BaseObjectSearchRequest)));
					}
					catch (SerializationException ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: SerializationException {0}", ex.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex);
					}
					catch (InvalidOperationException ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: InvalidOperationException {0}", ex2.ToString());
						if (ex2.InnerException != null && ex2.InnerException is InvalidExpressionException)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: Expression {0} Not Valid", ((InvalidExpressionException)ex2.InnerException).Expression);
							throw FaultGenerator.GenerateCannotProcessFilterFault((InvalidExpressionException)ex2.InnerException);
						}
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex2);
					}
					catch (XmlException ex3)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: XmlException {0}", ex3.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex3);
					}
					BaseObjectSearchResponse baseObjectSearchResponse = null;
					if (baseObjectSearchRequest.Controls != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: IMDA Request has {0} no. of controls present in the body.", baseObjectSearchRequest.Controls.Length);
						ldapControlsIn = BuildListOfSDSPDirectoryControls(baseObjectSearchRequest.Controls);
					}
					if (baseObjectSearchRequest.AttributeTypes != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: IMDA Request has {0} no. of AttributeTypes present in the body.", baseObjectSearchRequest.AttributeTypes.Length);
						if (baseObjectSearchRequest.AttributeTypes.Length > 100)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: No. of AttributeTypes {0} exceeds the allowed limit of {1}.", baseObjectSearchRequest.AttributeTypes.Length, 100);
							throw FaultGenerator.GenerateEncodingLimitFault(EncodingLimitExceededDetailType.AttributeTypes);
						}
						if (string.IsNullOrEmpty(baseObjectSearchRequest.Dialect))
						{
							throw new ArgumentException(AdwsStrings.MissingDialect);
						}
						if (!DialectParser.IsDialectSupported(OperationType.Get, baseObjectSearchRequest.Dialect))
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: Dialect {0} Not Supported", baseObjectSearchRequest.Dialect);
							throw FaultGenerator.GenerateFragmentDialectNotSupportedFault(OperationType.Get);
						}
						IList<DirectoryAttribute> list;
						try
						{
							list = DialectParser.Create(baseObjectSearchRequest.Dialect).ParseGet(directoryObjectIdentifier.Instance, baseObjectSearchRequest.AttributeTypes);
						}
						catch (InvalidExpressionException ex4)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: Expression {0} Not Valid", ex4.Expression);
							throw FaultGenerator.GenerateCannotProcessFilterFault(ex4);
						}
						DirectoryObjectFragment objFrag;
						try
						{
							objFrag = ClassManager.DirectoryAction.Read(directoryObjectIdentifier, list, ldapControlsIn, out ldapControlsOut);
						}
						catch (InvalidOperationException ex5)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: InvalidOperationException {0}", ex5.ToString());
							throw FaultGenerator.GenerateUnwillingToPerformFaultForReadObject(ex5);
						}
						catch (UnknownAttributeException ex6)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: UnknownAttributeException {0}", ex6.ToString());
							throw FaultGenerator.GenerateCannotProcessFilterFaultForReadObject(ex6);
						}
						baseObjectSearchResponse = new BaseObjectSearchResponse(objFrag, list, ldapControlsOut);
					}
					else
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: IMDA Get has no AttributeTypes in the body.");
						DirectoryObject dirObj;
						try
						{
							dirObj = ClassManager.DirectoryAction.Read(directoryObjectIdentifier, ldapControlsIn, out ldapControlsOut);
						}
						catch (InvalidOperationException ex7)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: InvalidOperationException {0}", ex7.ToString());
							throw FaultGenerator.GenerateUnwillingToPerformFaultForReadObject(ex7);
						}
						catch (UnknownAttributeException ex8)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: UnknownAttributeException {0}", ex8.ToString());
							throw FaultGenerator.GenerateCannotProcessFilterFaultForReadObject(ex8);
						}
						baseObjectSearchResponse = new BaseObjectSearchResponse(dirObj, ldapControlsOut);
					}
					message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/transfer/GetResponse", baseObjectSearchResponse, new XmlTypeObjectSerializer(typeof(BaseObjectSearchResponse)));
					message.Headers.RelatesTo = request.Headers.MessageId;
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: This is a WS-T request since it does not have any IMDA header.");
					if (!request.IsEmpty)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: WS-T Request has a body.");
						try
						{
							controls = request.GetBody<Controls>(new XmlTypeObjectSerializer(typeof(Controls)));
						}
						catch (SerializationException ex9)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: SerializationException {0}", ex9.ToString());
							throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex9);
						}
						catch (InvalidOperationException ex10)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: InvalidOperationException {0}", ex10.ToString());
							throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex10);
						}
						catch (XmlException ex11)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: XmlException {0}", ex11.ToString());
							throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex11);
						}
						if (controls != null && controls.Ctrls != null)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: WS-T Request has {0} no. of controls present in the body.", controls.Ctrls.Length);
							ldapControlsIn = BuildListOfSDSPDirectoryControls(controls.Ctrls);
						}
					}
					DirectoryObject directoryObject;
					try
					{
						directoryObject = ClassManager.DirectoryAction.Read(directoryObjectIdentifier, ldapControlsIn, out ldapControlsOut);
					}
					catch (InvalidOperationException ex12)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: InvalidOperationException {0}", ex12.ToString());
						throw FaultGenerator.GenerateUnwillingToPerformFaultForReadObject(ex12);
					}
					catch (UnknownAttributeException ex13)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: UnknownAttributeException {0}", ex13.ToString());
						throw FaultGenerator.GenerateCannotProcessFilterFaultForReadObject(ex13);
					}
					message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/transfer/GetResponse", directoryObject, new ControlSerializer(ldapControlsOut, new DirectoryObjectSerializer(directoryObject)));
					message.Headers.RelatesTo = request.Headers.MessageId;
				}
				if (GlobalDebug.Info)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: Response {0}", message.ToString());
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Get: Exit");
				return message;
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex15)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: LdapException {0}", ex15.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex15);
			}
			catch (DirectoryOperationException ex16)
			{
				if (ex16.Response != null && ex16.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: DirectoryOperatonException(noSuchObject) {0}", ex16.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex16);
				}
				if (ex16.Response != null && ex16.Response.ResultCode == ResultCode.InsufficientAccessRights)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: DirectoryOperatonException(InsufficientAccessRights) {0}", ex16.ToString());
					throw FaultGenerator.GenerateAccessDeniedFault(ex16);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: DirectoryOperationException {0}", ex16.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex16);
			}
			catch (ArgumentException ex17)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: ArgumentException {0}", ex17.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex17);
			}
			catch (NoConnectionAvailableException ex18)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Get: NoConnectionAvailableException {0}", ex18.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex18);
			}
			catch (Exception ex19)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Get: Unhandled Exception {0}", ex19.ToString());
				throw;
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	[IdentityManagementOperation]
	[ObjectIdAndInstanceInformation]
	public Message Put(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.PutOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Put: request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: {0}", request.ToString());
			}
			Message message = null;
			Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.ModifyRequest modifyRequest = null;
			try
			{
				VerifyImpersonationLevel();
				IList<DirectoryAttributeOperation> fragments = new List<DirectoryAttributeOperation>();
				IList<DirectoryControl> ldapControlsIn = null;
				IList<DirectoryControl> ldapControlsOut = null;
				DirectoryObjectIdentifier directoryObjectIdentifier = BuildObjectIdentifierFromTheRequest(request, RequestType.Put);
				if (MessageContainsIdentityManagementHeader(request))
				{
					try
					{
						modifyRequest = request.GetBody<Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.ModifyRequest>(new XmlTypeObjectSerializer(typeof(Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations.ModifyRequest)));
					}
					catch (SerializationException ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: SerializationException {0}", ex.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex);
					}
					catch (InvalidOperationException ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: InvalidOperationException {0}", ex2.ToString());
						if (ex2.GetBaseException() is InvalidExpressionException ex3)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: remapping to base fault InvalidExpressionException: {0}", ex3.ToString());
							throw FaultGenerator.GenerateCannotProcessFilterFault(ex3);
						}
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex2);
					}
					catch (XmlException ex4)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: XmlException {0}", ex4.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex4);
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: It's a IMDA Put Request with a body.");
					if (modifyRequest.Controls != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: IMDA Request has {0} no. of controls present in the body.", modifyRequest.Controls.Length);
						ldapControlsIn = BuildListOfSDSPDirectoryControls(modifyRequest.Controls);
					}
					if (!string.IsNullOrEmpty(modifyRequest.Dialect))
					{
						if (!DialectParser.IsDialectSupported(OperationType.Put, modifyRequest.Dialect))
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: Dialect {0} Not Supported", modifyRequest.Dialect);
							throw FaultGenerator.GenerateFragmentDialectNotSupportedFault(OperationType.Put);
						}
						if (modifyRequest.Changes != null)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: IMDA Request has {0} no. of Changes present in the body.", modifyRequest.Changes.Length);
							if (modifyRequest.Changes.Length > 100)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: No. of Changes {0} exceeds the allowed limit of {1}.", modifyRequest.Changes.Length, 100);
								throw FaultGenerator.GenerateEncodingLimitFault(EncodingLimitExceededDetailType.AttributeTypeAndValues);
							}
							try
							{
								fragments = DialectParser.Create(modifyRequest.Dialect).ParsePut(directoryObjectIdentifier.Instance, modifyRequest.Changes);
							}
							catch (InvalidExpressionException ex5)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: Expression {0} Not Valid", ex5.Expression);
								throw FaultGenerator.GenerateCannotProcessFilterFault(ex5);
							}
							catch (InvalidModifyRequestSyntaxException ex6)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: Expression {0} is syntactically invalid.", ex6.Expression);
								throw FaultGenerator.GenerateSchemaValidationErrorFault(ex6);
							}
							catch (ModifyRequestOperationUnsupportedException ex7)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: Operation {0} Not Valid", ex7.Operation);
								throw FaultGenerator.GenerateSchemaValidationErrorFault(ex7);
							}
							catch (PutException e)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: change with bad value");
								throw FaultGenerator.GenerateUnwillingToPerformFault(e);
							}
						}
						try
						{
							ClassManager.DirectoryAction.Update(directoryObjectIdentifier, fragments, ldapControlsIn, out ldapControlsOut);
						}
						catch (InvalidOperationException ex8)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: InvalidOperationException {0}", ex8.ToString());
							throw FaultGenerator.GenerateUnwillingToPerformForUpdateObject(ex8);
						}
						message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/transfer/PutResponse", null, new ControlSerializer(ldapControlsOut, null));
						message.Headers.RelatesTo = request.Headers.MessageId;
						if (GlobalDebug.Info)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: Response {0}", message.ToString());
						}
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Put: Exit");
						return message;
					}
					throw new ArgumentException(AdwsStrings.MissingDialect);
				}
				throw FaultGenerator.GenerateActionNotSupportedFault(AdwsStrings.ActionNotSupportedPutDetail);
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex10)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: LdapException {0}", ex10.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex10);
			}
			catch (DirectoryOperationException ex11)
			{
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperatonException(noSuchObject) {0}", ex11.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex11);
				}
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.ConstraintViolation)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperatonException(ConstraintViolation) {0}", ex11.ToString());
					throw FaultGenerator.GenerateInvalidRepresentationFaultConstraintViolation(ex11);
				}
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.ObjectClassViolation)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperatonException(ObjectClassViolation) {0}", ex11.ToString());
					throw FaultGenerator.GenerateInvalidRepresentationFault(ex11);
				}
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.AttributeOrValueExists)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperatonException(AttributeOrValueExists) {0}", ex11.ToString());
					throw FaultGenerator.GenerateInvalidRepresentationFaultAttrExists(ex11);
				}
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.InsufficientAccessRights)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperatonException(InsufficientAccessRights) {0}", ex11.ToString());
					throw FaultGenerator.GenerateAccessDeniedFault(ex11);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: DirectoryOperationException {0}", ex11.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex11);
			}
			catch (ArgumentException ex12)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: ArgumentException {0}", ex12.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex12);
			}
			catch (NoConnectionAvailableException ex13)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Put: NoConnectionAvailableException {0}", ex13.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex13);
			}
			catch (Exception ex14)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Put: Unhandled Exception {0}", ex14.ToString());
				throw;
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	[ObjectIdAndInstanceInformation]
	public Message Delete(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.DeleteOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Delete: request is null");
				throw new ArgumentNullException("request");
			}
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Delete: {0}", request.ToString());
			}
			Message message = null;
			Controls controls = null;
			try
			{
				VerifyImpersonationLevel();
				IList<DirectoryControl> ldapControlsIn = null;
				IList<DirectoryControl> ldapControlsOut = new List<DirectoryControl>();
				DirectoryObjectIdentifier objectId = BuildObjectIdentifierFromTheRequest(request, RequestType.Delete);
				if (!request.IsEmpty)
				{
					try
					{
						controls = request.GetBody<Controls>(new XmlTypeObjectSerializer(typeof(Controls)));
					}
					catch (SerializationException ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: SerializationException {0}", ex.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex);
					}
					catch (InvalidOperationException ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: InvalidOperationException {0}", ex2.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex2);
					}
					catch (XmlException ex3)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: XmlException {0}", ex3.ToString());
						throw FaultGenerator.GenerateSchemaValidationErrorForSerialization(ex3);
					}
					if (controls != null && controls.Ctrls != null)
					{
						ldapControlsIn = BuildListOfSDSPDirectoryControls(controls.Ctrls);
					}
				}
				ClassManager.DirectoryAction.Delete(objectId, ldapControlsIn, out ldapControlsOut);
				message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/transfer/DeleteResponse", null, new ControlSerializer(ldapControlsOut, null));
				message.Headers.RelatesTo = request.Headers.MessageId;
				if (GlobalDebug.Info)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Delete: Response {0}", message.ToString());
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Delete: Exit");
				return message;
			}
			catch (LdapException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: LdapException {0}", ex4.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex4);
			}
			catch (DirectoryOperationException ex5)
			{
				if (ex5.Response != null && ex5.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: DirectoryOperatonException(noSuchObject) {0}", ex5.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex5);
				}
				if (ex5.Response != null && ex5.Response.ResultCode == ResultCode.InsufficientAccessRights)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: DirectoryOperatonException(InsufficientAccessRights) {0}", ex5.ToString());
					throw FaultGenerator.GenerateAccessDeniedFault(ex5);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: DirectoryOperationException {0}", ex5.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex5);
			}
			catch (ArgumentException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: ArgumentException {0}", ex6.ToString());
				throw FaultGenerator.GenerateUnwillingToPerformFault(ex6);
			}
			catch (NoConnectionAvailableException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Delete: NoConnectionAvailableException {0}", ex7.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex7);
			}
			catch (Exception ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Delete: Unhandled Exception {0}", ex8.ToString());
				throw;
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	[ObjectIdAndInstanceInformation]
	public Message Enumerate(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.EnumOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: {0}", request.ToString());
			}
			Message message = null;
			Enumerate enumerate = null;
			Query query = null;
			EnumerationContextCacheEntry enumerationContextCacheEntry = null;
			bool flag = false;
			try
			{
				VerifyImpersonationLevel();
				IList<DirectoryAttribute> attributesToReturn = new List<DirectoryAttribute>();
				IList<DirectoryAttribute> list = null;
				EnumerateResponse enumerateResponse = null;
				QueryFilter queryFilter = null;
				bool ascendingSort = true;
				DirectoryObjectIdentifier directoryObjectIdentifier = BuildObjectIdentifierFromTheRequest(request, RequestType.Enumerate);
				enumerationContextCacheEntry = new EnumerationContextCacheEntry(directoryObjectIdentifier.Instance, this);
				Guid enumerationContext = enumerationContextCacheEntry.EnumerationContext;
				try
				{
					enumerate = request.GetBody<Enumerate>(new XmlTypeObjectSerializer(typeof(Enumerate)));
				}
				catch (SerializationException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: SerializationException {0}", ex.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex);
				}
				catch (InvalidOperationException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: InvalidOperationException {0}", ex2.ToString());
					if (ex2.InnerException != null && ex2.InnerException is InvalidExpressionException)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: Expression {0} Not Valid", ((InvalidExpressionException)ex2.InnerException).Expression);
						throw FaultGenerator.GenerateCannotProcessFilterFault((InvalidExpressionException)ex2.InnerException);
					}
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex2);
				}
				catch (XmlException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: XmlException {0}", ex3.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex3);
				}
				if (enumerate.Filter != null && enumerate.Filter.QueryField != null)
				{
					if (string.IsNullOrEmpty(enumerate.Filter.Dialect))
					{
						throw new ArgumentException(AdwsStrings.MissingFilterDialect);
					}
					if (!DialectParser.IsDialectSupported(OperationType.EnumerateFilter, enumerate.Filter.Dialect))
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: Filter Dialect {0} Not Supported", enumerate.Filter.Dialect);
						throw FaultGenerator.GenerateFilterDialectRequestedUnavailableFault();
					}
					DialectParser dialectParser = DialectParser.Create(enumerate.Filter.Dialect);
					try
					{
						queryFilter = dialectParser.ParseEnumerate(enumerate.Filter.QueryField);
					}
					catch (ArgumentException ex4)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: ArgumentException {0} thrown from parseEnumerate", ex4.ToString());
						throw FaultGenerator.GenerateCannotProcessFilterFault(ex4);
					}
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: Filter not found in the request, hence constructing default query filter");
					try
					{
						queryFilter = ClassManager.DirectoryAction.GetDefaultQueryFilter(directoryObjectIdentifier.Instance);
					}
					catch (InvalidOperationException e)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: InvalidOperationException {0} Could not find rootDSE or default naming context from the directory");
						throw FaultGenerator.GenerateCannotProcessFilterFault(e);
					}
				}
				try
				{
					if (enumerate.Selection != null && enumerate.Selection.SelectionProperties != null)
					{
						if (string.IsNullOrEmpty(enumerate.Selection.Dialect))
						{
							throw new ArgumentException(AdwsStrings.MissingSelectionDialect);
						}
						if (!DialectParser.IsDialectSupported(OperationType.EnumerateSelection, enumerate.Selection.Dialect))
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: Selection Dialect {0} Not Supported", enumerate.Selection.Dialect);
							throw FaultGenerator.GenerateUnsupportedDialectFaultForEnumSelectionOrSort(OperationType.EnumerateSelection);
						}
						DialectParser dialectParser2 = DialectParser.Create(enumerate.Selection.Dialect);
						InstanceType instance = directoryObjectIdentifier.Instance;
						ExpressionBase[] selectionProperties = enumerate.Selection.SelectionProperties;
						attributesToReturn = dialectParser2.ParseSelectionOrSort(instance, selectionProperties);
					}
					if (enumerate.Sorting != null && enumerate.Sorting.SortingProperties != null)
					{
						if (string.IsNullOrEmpty(enumerate.Sorting.Dialect))
						{
							throw new ArgumentException(AdwsStrings.MissingSortingDialect);
						}
						if (!DialectParser.IsDialectSupported(OperationType.EnumerateSort, enumerate.Sorting.Dialect))
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: Sorting Dialect {0} Not Supported", enumerate.Sorting.Dialect);
							throw FaultGenerator.GenerateUnsupportedDialectFaultForEnumSelectionOrSort(OperationType.EnumerateSort);
						}
						DialectParser dialectParser3 = DialectParser.Create(enumerate.Sorting.Dialect);
						InstanceType instance2 = directoryObjectIdentifier.Instance;
						ExpressionBase[] selectionProperties = enumerate.Sorting.SortingProperties;
						list = dialectParser3.ParseSelectionOrSort(instance2, selectionProperties);
					}
					if (list != null && list.Count > 0)
					{
						if (list.Count > 1)
						{
							throw FaultGenerator.GenerateInvalidSortKeyFault(AdwsStrings.TooManySortKeySpecified);
						}
						if (!list[0].Xmlns.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase) || (list[0].Name.Equals(AdwsConstants.star, StringComparison.InvariantCultureIgnoreCase) && list[0].Xmlns.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase)))
						{
							throw FaultGenerator.GenerateInvalidSortKeyFault(AdwsStrings.SortKeyIsSpecialAttribute);
						}
						ascendingSort = enumerate.Sorting.SortingProperties[0].Ascending;
					}
				}
				catch (InvalidExpressionException ex5)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: Property {0} Not Valid", ex5.Expression);
					throw FaultGenerator.GenerateInvalidPropertyFault(ex5);
				}
				try
				{
					query = (enumerationContextCacheEntry.Query = ClassManager.DirectoryAction.PrepareQuery(directoryObjectIdentifier.Instance, queryFilter, attributesToReturn, (list == null || list.Count == 0) ? null : list[0], ascendingSort, ClassManager.EnumerationContextCache.TimeoutHandler));
					if (!string.IsNullOrEmpty(enumerate.Expires))
					{
						try
						{
							if (enumerate.Expires.StartsWith("P", StringComparison.OrdinalIgnoreCase))
							{
								enumerationContextCacheEntry.Expires = DateTime.UtcNow.Add(XmlConvert.ToTimeSpan(enumerate.Expires));
							}
							else
							{
								enumerationContextCacheEntry.Expires = XmlConvert.ToDateTime(enumerate.Expires, XmlDateTimeSerializationMode.Utc);
							}
						}
						catch (FormatException ex6)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: FormatException {0}", ex6.ToString());
							throw new ArgumentException(AdwsStrings.UnrecognizedDateAndTime);
						}
					}
					else
					{
						enumerationContextCacheEntry.Expires = DateTime.UtcNow.Add(ConfigurationSettings.MaxEnumContextExpiration);
						enumerationContextCacheEntry.Expires.AddSeconds(-1.0);
					}
					EnumerationContextCache.ResultCode resultCode = ClassManager.EnumerationContextCache.AddEntry(enumerationContextCacheEntry);
					if (resultCode == EnumerationContextCache.ResultCode.ExpiresInThePast || resultCode == EnumerationContextCache.ResultCode.ExpirationExceedsLimit)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: InvalidExpirationTime {0}", resultCode.ToString());
						throw FaultGenerator.GenerateInvalidExpirationTimeFault();
					}
				}
				catch (EnumerationContextLimitExceededException ex7)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: EnumerationContextLimitExceededException {0}", ex7.ToString());
					throw FaultGenerator.GenerateEnumerationContextLimitExceededFault(ex7);
				}
				enumerateResponse = new EnumerateResponse(enumerationContext, XmlConvert.ToString(enumerationContextCacheEntry.Expires, XmlDateTimeSerializationMode.Utc));
				message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/EnumerateResponse", enumerateResponse, new XmlTypeObjectSerializer(typeof(EnumerateResponse)));
				message.Headers.RelatesTo = request.Headers.MessageId;
				if (GlobalDebug.Info)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: Response {0}", message.ToString());
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: Exit");
				return message;
			}
			catch (FaultException)
			{
				flag = true;
				throw;
			}
			catch (LdapException ex9)
			{
				flag = true;
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: LdapException {0}", ex9.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex9);
			}
			catch (DirectoryOperationException ex10)
			{
				flag = true;
				if (ex10.Response != null && ex10.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: DirectoryOperatonException(noSuchObject) {0}", ex10.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex10);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: DirectoryOperationException {0}", ex10.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex10);
			}
			catch (ArgumentException ex11)
			{
				flag = true;
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: ArgumentException {0}", ex11.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex11);
			}
			catch (NoConnectionAvailableException ex12)
			{
				flag = true;
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Enumerate: NoConnectionAvailableException {0}", ex12.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex12);
			}
			catch (Exception ex13)
			{
				flag = true;
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Enumerate: Unhandled Exception {0}", ex13.ToString());
				throw;
			}
			finally
			{
				if (flag)
				{
					if (query != null)
					{
						query.Dispose();
						query = null;
					}
					if (enumerationContextCacheEntry != null)
					{
						EnumerationContextCache.ResultCode resultCode2 = ClassManager.EnumerationContextCache.RemoveEntry(enumerationContextCacheEntry.EnumerationContext);
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: RemoveEntry resultCode {0}", resultCode2.ToString());
						enumerationContextCacheEntry = null;
					}
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Enumerate: Exit");
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public Message Pull(Message request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.PullOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: {0}", request.ToString());
			}
			Message message = null;
			Pull pull = null;
			EnumerationContextCacheEntry enumerationContextCacheEntry = null;
			bool flag = false;
			try
			{
				VerifyImpersonationLevel();
				PullResponse pullResponse = null;
				TimeSpan zero = TimeSpan.Zero;
				uint num = 0u;
				IList<DirectoryControl> ldapControlsIn = null;
				IList<DirectoryControl> ldapControlsOut = null;
				IList<DirectoryObjectFragment> list = null;
				try
				{
					pull = request.GetBody<Pull>(new XmlTypeObjectSerializer(typeof(Pull)));
				}
				catch (SerializationException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: SerializationException {0}", ex.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex);
				}
				catch (InvalidOperationException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: InvalidOperationException {0}", ex2.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex2);
				}
				catch (XmlException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: XmlException {0}", ex3.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex3);
				}
				if (pull.Controls != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: WS-Enum Pull Request has {0} no. of controls present in the body.", pull.Controls.Length);
					ldapControlsIn = BuildListOfSDSPDirectoryControls(pull.Controls);
				}
				if (pull.EnumerationContext == Guid.Empty)
				{
					throw new ArgumentException(AdwsStrings.EnumContextAbsentInTheRequest);
				}
				switch (ClassManager.EnumerationContextCache.IsValidEnumContextForSession(pull.EnumerationContext, this))
				{
				case EnumerationContextCache.ResultCode.SessionMismatch:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.SessionsMismatch);
				case EnumerationContextCache.ResultCode.NoSuchEntry:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.NoSuchEnumCtxGuidExists);
				default:
					enumerationContextCacheEntry = ClassManager.EnumerationContextCache.GetEntry(pull.EnumerationContext);
					if (enumerationContextCacheEntry == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: ArgumentException: Expired or unknown entry.");
						throw new ArgumentException(AdwsStrings.NoSuchEnumCtxGuidExists);
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: Found entry");
					if (enumerationContextCacheEntry.Query != null && enumerationContextCacheEntry.Query.NoMoreResults())
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: No more results left for the query.");
						flag = true;
						throw new ArgumentException(AdwsStrings.NoSuchEnumCtxGuidExists);
					}
					if (!string.IsNullOrEmpty(pull.MaxCharacters))
					{
						throw FaultGenerator.GenerateMaxCharsNotSupportedFault();
					}
					if (!string.IsNullOrEmpty(pull.MaxTime))
					{
						try
						{
							zero = XmlConvert.ToTimeSpan(pull.MaxTime);
						}
						catch (FormatException ex4)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: FormatException {0}", ex4.ToString());
							throw new ArgumentException(AdwsStrings.UnrecognizedMaxTime);
						}
						if (zero >= ConfigurationSettings.MaxPullTimeout)
						{
							throw FaultGenerator.GenerateMaxTimeExceedsLimitFault();
						}
					}
					else
					{
						zero = ConfigurationSettings.MaxPullTimeout;
					}
					if (!string.IsNullOrEmpty(pull.MaxElements))
					{
						try
						{
							if (XmlConvert.ToInt64(pull.MaxElements) < 0)
							{
								throw new ArgumentException(AdwsStrings.UnrecognizedMaxElements);
							}
							num = (uint)XmlConvert.ToInt64(pull.MaxElements);
						}
						catch (FormatException ex5)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: FormatException {0}", ex5.ToString());
							throw new ArgumentException(AdwsStrings.UnrecognizedMaxElements);
						}
						catch (OverflowException ex6)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: OverflowException {0}", ex6.ToString());
							throw new ArgumentException(AdwsStrings.UnrecognizedMaxElements);
						}
					}
					else
					{
						num = 1u;
					}
					try
					{
						list = ClassManager.DirectoryAction.NextQueryPage(enumerationContextCacheEntry.Query, zero, num, ldapControlsIn, out ldapControlsOut);
						if (enumerationContextCacheEntry.Query.NoMoreResults())
						{
							flag = true;
						}
					}
					catch (UnknownAttributeException ex7)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: UnknownAttributeException {0}", ex7.ToString());
						throw FaultGenerator.GenerateEndpointUnavailableFault(ex7);
					}
					catch (InvalidOperationException ex8)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: InvalidOperationException {0}", ex8.ToString());
						throw FaultGenerator.GenerateEndpointUnavailableFault(ex8);
					}
					pullResponse = new PullResponse(list, ldapControlsOut);
					if (flag)
					{
						XmlDocument xmlDocument = new XmlDocument();
						xmlDocument.LoadXml(string.Format(CultureInfo.InvariantCulture, "<{0}:{1} {2}:{0}='{3}'></{0}:{1}>", "wsen", "EndOfSequence", "xmlns", "http://schemas.xmlsoap.org/ws/2004/09/enumeration"));
						pullResponse.EndOfSequence = xmlDocument.DocumentElement;
					}
					else
					{
						XmlDocument xmlDocument2 = new XmlDocument();
						xmlDocument2.LoadXml(string.Format(CultureInfo.InvariantCulture, "<{0}:{1} {2}:{0}='{3}'></{0}:{1}>", "wsen", "EnumerationContext", "xmlns", "http://schemas.xmlsoap.org/ws/2004/09/enumeration"));
						xmlDocument2.DocumentElement.InnerXml = pull.EnumerationContext.ToString();
						pullResponse.EnumerationContext = xmlDocument2.DocumentElement;
					}
					message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/PullResponse", pullResponse, new XmlTypeObjectSerializer(typeof(PullResponse)));
					message.Headers.RelatesTo = request.Headers.MessageId;
					if (GlobalDebug.Info)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: Response {0}", message.ToString());
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: Exit");
					return message;
				}
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex10)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: LdapException {0}", ex10.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex10);
			}
			catch (DirectoryOperationException ex11)
			{
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.NoSuchObject)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: DirectoryOperatonException(noSuchObject) {0}", ex11.ToString());
					throw FaultGenerator.GenerateDestinationUnreachableFault(ex11);
				}
				if (ex11.Response != null && ex11.Response.ResultCode == ResultCode.TimeLimitExceeded)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: DirectoryOperatonException(timeLimitExceeded) {0}", ex11.ToString());
					throw FaultGenerator.GenerateTimedOutFault(ex11);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: DirectoryOperationException {0}", ex11.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex11);
			}
			catch (ArgumentException ex12)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: ArgumentException {0}", ex12.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex12);
			}
			catch (NoConnectionAvailableException ex13)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Pull: NoConnectionAvailableException {0}", ex13.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex13);
			}
			catch (Exception ex14)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Pull: Unhandled Exception {0}", ex14.ToString());
				throw;
			}
			finally
			{
				if (enumerationContextCacheEntry != null)
				{
					ClassManager.EnumerationContextCache.ReturnToCache(enumerationContextCacheEntry);
				}
				if (flag && enumerationContextCacheEntry != null)
				{
					EnumerationContextCache.ResultCode resultCode = ClassManager.EnumerationContextCache.RemoveTimedOutOrExhaustedEntry(enumerationContextCacheEntry.EnumerationContext);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: RemoveTimedOutOrExhaustedEntry resultCode {0}", resultCode.ToString());
					enumerationContextCacheEntry = null;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Pull: Exit");
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public Message Renew(Message request)
	{
		CheckDisposed();
		EnterMethod();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Renew: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Renew: {0}", request.ToString());
			}
			Message message = null;
			Renew renew = null;
			try
			{
				VerifyImpersonationLevel();
				RenewResponse renewResponse = null;
				try
				{
					renew = request.GetBody<Renew>(new XmlTypeObjectSerializer(typeof(Renew)));
				}
				catch (SerializationException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: SerializationException {0}", ex.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex);
				}
				catch (InvalidOperationException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: InvalidOperationException {0}", ex2.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex2);
				}
				catch (XmlException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: XmlException {0}", ex3.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex3);
				}
				if (renew.EnumerationContext == Guid.Empty)
				{
					throw new ArgumentException(AdwsStrings.EnumContextAbsentInTheRequest);
				}
				switch (ClassManager.EnumerationContextCache.IsValidEnumContextForSession(renew.EnumerationContext, this))
				{
				case EnumerationContextCache.ResultCode.SessionMismatch:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.SessionsMismatch);
				case EnumerationContextCache.ResultCode.NoSuchEntry:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.NoSuchEnumCtxGuidExists);
				default:
					if (!string.IsNullOrEmpty(renew.Expires))
					{
						DateTime dateTime;
						try
						{
							dateTime = ((!renew.Expires.StartsWith("P", StringComparison.OrdinalIgnoreCase)) ? XmlConvert.ToDateTime(renew.Expires, XmlDateTimeSerializationMode.Utc) : DateTime.UtcNow.Add(XmlConvert.ToTimeSpan(renew.Expires)));
						}
						catch (FormatException ex4)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: FormatException {0}", ex4.ToString());
							throw new ArgumentException(AdwsStrings.UnrecognizedDateAndTime);
						}
						EnumerationContextCache.ResultCode resultCode = ClassManager.EnumerationContextCache.UpdateEntryExpiration(renew.EnumerationContext, dateTime);
						switch (resultCode)
						{
						case EnumerationContextCache.ResultCode.ExpiresInThePast:
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: InvalidExpirationTime {0}", resultCode.ToString());
							throw FaultGenerator.GenerateUnableToRenewFault(AdwsStrings.UnableToRenewExpiresInPastDetail);
						case EnumerationContextCache.ResultCode.ExpirationExceedsLimit:
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: InvalidExpirationTime {0}", resultCode.ToString());
							throw FaultGenerator.GenerateUnableToRenewFault(AdwsStrings.UnableToRenewLimitExceededDetail);
						case EnumerationContextCache.ResultCode.NoSuchEntry:
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: Unknown or expired entry. {0}", resultCode.ToString());
							throw FaultGenerator.GenerateUnableToRenewFault(AdwsStrings.UnableToRenewNoSuchEntryDetail);
						default:
							renewResponse = new RenewResponse(renew.EnumerationContext, XmlConvert.ToString(dateTime, XmlDateTimeSerializationMode.Utc));
							message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/RenewResponse", renewResponse, new XmlTypeObjectSerializer(typeof(RenewResponse)));
							message.Headers.RelatesTo = request.Headers.MessageId;
							if (GlobalDebug.Info)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Renew: Response {0}", message.ToString());
							}
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Renew: Exit");
							return message;
						}
					}
					throw new ArgumentException(AdwsStrings.NewExpirationTimeNotSpecified);
				}
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: LdapException {0}", ex6.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex6);
			}
			catch (ArgumentException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Renew: ArgumentException {0}", ex7.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex7);
			}
			catch (Exception ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Renew: Unhandled Exception {0}", ex8.ToString());
				throw;
			}
			finally
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Renew: Exit");
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public Message GetStatus(Message request)
	{
		CheckDisposed();
		EnterMethod();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetStatus: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetStatus: {0}", request.ToString());
			}
			Message message = null;
			GetStatus getStatus = null;
			try
			{
				VerifyImpersonationLevel();
				GetStatusResponse getStatusResponse = null;
				try
				{
					getStatus = request.GetBody<GetStatus>(new XmlTypeObjectSerializer(typeof(GetStatus)));
				}
				catch (SerializationException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: SerializationException {0}", ex.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex);
				}
				catch (InvalidOperationException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: InvalidOperationException {0}", ex2.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex2);
				}
				catch (XmlException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: XmlException {0}", ex3.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex3);
				}
				if (getStatus.EnumerationContext == Guid.Empty)
				{
					throw new ArgumentException(AdwsStrings.EnumContextAbsentInTheRequest);
				}
				switch (ClassManager.EnumerationContextCache.IsValidEnumContextForSession(getStatus.EnumerationContext, this))
				{
				case EnumerationContextCache.ResultCode.SessionMismatch:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.SessionsMismatch);
				case EnumerationContextCache.ResultCode.NoSuchEntry:
					throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.NoSuchEnumCtxGuidExists);
				default:
				{
					DateTime expirationTime;
					EnumerationContextCache.ResultCode entryExpiration = ClassManager.EnumerationContextCache.GetEntryExpiration(getStatus.EnumerationContext, out expirationTime);
					if (entryExpiration == EnumerationContextCache.ResultCode.NoSuchEntry)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: Entry not found or expired {0}", entryExpiration.ToString());
						throw FaultGenerator.GenerateInvalidEnumerationContextFault(AdwsStrings.NoSuchEnumCtxGuidExists);
					}
					getStatusResponse = new GetStatusResponse(XmlConvert.ToString(expirationTime, XmlDateTimeSerializationMode.Utc));
					message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/GetStatusResponse", getStatusResponse, new XmlTypeObjectSerializer(typeof(GetStatusResponse)));
					message.Headers.RelatesTo = request.Headers.MessageId;
					if (GlobalDebug.Info)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetStatus: Response {0}", message.ToString());
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetStatus: Exit");
					return message;
				}
				}
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: LdapException {0}", ex5.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex5);
			}
			catch (ArgumentException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetStatus: ArgumentException {0}", ex6.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex6);
			}
			catch (Exception ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetStatus: Unhandled Exception {0}", ex7.ToString());
				throw;
			}
			finally
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetStatus: Exit");
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public Message Release(Message request)
	{
		CheckDisposed();
		EnterMethod();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "request is null");
				throw new ArgumentNullException("request");
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Release: Enter");
			if (GlobalDebug.Info)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Release: {0}", request.ToString());
			}
			Message message = null;
			Release release = null;
			try
			{
				VerifyImpersonationLevel();
				try
				{
					release = request.GetBody<Release>(new XmlTypeObjectSerializer(typeof(Release)));
				}
				catch (SerializationException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: SerializationException {0}", ex.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex);
				}
				catch (InvalidOperationException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: InvalidOperationException {0}", ex2.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex2);
				}
				catch (XmlException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: XmlException {0}", ex3.ToString());
					throw FaultGenerator.GenerateEndpointUnavailableForSerialization(ex3);
				}
				if (release.EnumerationContext != Guid.Empty)
				{
					EnumerationContextCache.ResultCode resultCode = ClassManager.EnumerationContextCache.IsValidEnumContextForSession(release.EnumerationContext, this);
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: IsValidEnumContextForSession resultCode {0}", resultCode.ToString());
					if (resultCode == EnumerationContextCache.ResultCode.Success)
					{
						EnumerationContextCache.ResultCode resultCode2 = ClassManager.EnumerationContextCache.RemoveEntry(release.EnumerationContext);
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: RemoveEntry resultCode {0}", resultCode2.ToString());
					}
					message = Message.CreateMessage(request.Version, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/ReleaseResponse");
					message.Headers.RelatesTo = request.Headers.MessageId;
					if (GlobalDebug.Info)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Release: Response {0}", message.ToString());
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Release: Exit");
					return message;
				}
				throw new ArgumentException(AdwsStrings.EnumContextAbsentInTheRequest);
			}
			catch (FaultException)
			{
				throw;
			}
			catch (LdapException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: LdapException {0}", ex5.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex5);
			}
			catch (ArgumentException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "Release: ArgumentException {0}", ex6.ToString());
				throw FaultGenerator.GenerateEndpointUnavailableFault(ex6);
			}
			catch (Exception ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "Release: Unhandled Exception {0}", ex7.ToString());
				throw;
			}
			finally
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Release: Exit");
			}
		}
		finally
		{
			ExitMethod();
		}
	}

	private static bool MessageContainsIdentityManagementHeader(Message request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "MessageContainsIdentityManagementHeader", ": Enter");
		if (request == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MessageContainsIdentityManagementHeader: Request is null");
			throw new ArgumentNullException("request");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "MessageContainsIdentityManagementHeader", ": Exit");
		return -1 != request.Headers.FindHeader("IdentityManagementOperation", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
	}

	private DirectoryObjectIdentifier BuildObjectIdentifierFromTheRequest(Message request, RequestType operation)
	{
		if (request == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Request is null");
			throw new ArgumentNullException("request");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Enter");
		DirectoryObjectIdentifier directoryObjectIdentifier = new DirectoryObjectIdentifier();
		string text = null;
		if (operation == RequestType.Get || operation == RequestType.Delete || operation == RequestType.Put)
		{
			try
			{
				text = request.Headers.GetHeader<string>("objectReferenceProperty", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
			}
			catch (MessageHeaderException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Exception {0} objRefProp not present in the header", ex.Message.ToString());
				throw new ArgumentException(AdwsStrings.MustSpecifyObjectRefPropInTheHeader);
			}
			if (text == null || text.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: objId is null or empty");
				throw new ArgumentException(AdwsStrings.MustSpecifyObjectRefPropInTheHeader);
			}
		}
		string text2 = null;
		try
		{
			text2 = request.Headers.GetHeader<string>("instance", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
		}
		catch (MessageHeaderException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Exception {0} instance not present in the header", ex2.Message.ToString());
			throw new ArgumentException(AdwsStrings.MustSpecifyInstanceInfoInTheHeader);
		}
		if (text2 == null || text2.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: instance is null or empty");
			throw new ArgumentException(AdwsStrings.MustSpecifyInstanceInfoInTheHeader);
		}
		if (text != null)
		{
			if (text.Length == 36 && AdwsConstants.guidRegex.IsMatch(text))
			{
				try
				{
					directoryObjectIdentifier.ObjectReferenceProperty = new Guid(text);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Object guid={0} supplied", directoryObjectIdentifier.ObjectReferenceProperty);
				}
				catch (FormatException)
				{
				}
				catch (OverflowException)
				{
				}
			}
			if (directoryObjectIdentifier.ObjectReferenceProperty == Guid.Empty)
			{
				directoryObjectIdentifier.ObjectDn = text;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Object DN={0} supplied", text);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Object Ref Property Id {0}", text.ToString());
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Instance Type {0}", text2.ToString());
		InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(text2);
		if (instanceType == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: instanceType not in GlobalInstanceMap.");
			throw new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader);
		}
		directoryObjectIdentifier.Instance = instanceType;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "BuildObjectIdentifierFromTheRequest: Exit");
		return directoryObjectIdentifier;
	}

	private IList<DirectoryControl> BuildListOfSDSPDirectoryControls(DirControl[] dirControlArray)
	{
		if (dirControlArray == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "BuildListOfSDSPDirectoryControls: dirControlArray is null");
			throw new ArgumentNullException("dirControlArray");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebservices", "BuildListOfSDSPDirectoryControls: Entered w/{0} DirControls", dirControlArray.Length);
		IList<DirectoryControl> list = new List<DirectoryControl>();
		foreach (DirControl dirControl in dirControlArray)
		{
			DirectoryControl item = new DirectoryControl(dirControl.OID, (dirControl.ControlValue != null) ? dirControl.ControlValue.Value : null, dirControl.Criticality, serverSide: true);
			list.Add(item);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebservices", "BuildListOfSDSPDirectoryControls: Exit w/{0} SDSPDirectoryControls", list.Count);
		return list;
	}

	private void EnterMethod()
	{
		if (Interlocked.Increment(ref numberOfActiveOperations) == 1)
		{
			PerfCounters.ActiveWebServiceSessions.Increment();
		}
	}

	private void ExitMethod()
	{
		if (Interlocked.Decrement(ref numberOfActiveOperations) == 0)
		{
			PerfCounters.ActiveWebServiceSessions.Decrement();
		}
	}

	private void VerifyImpersonationLevel()
	{
		WindowsIdentity windowsIdentity = ServiceSecurityContext.Current.WindowsIdentity;
		if (GlobalDebug.Info)
		{
			try
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyImpersonationLevel: Impersonation Level is set to  {0}", windowsIdentity.ImpersonationLevel);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyImpersonationLevel: Running as SID {0}", windowsIdentity.User);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyImpersonationLevel: Running as username {0}", windowsIdentity.Name);
			}
			catch (IdentityNotMappedException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyImpersonationLevel: caught IdentityNotMappedException trying to log info");
			}
		}
		if (windowsIdentity.ImpersonationLevel < TokenImpersonationLevel.Impersonation)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "VerifyImpersonationLevel: Impersonation level not set to Impersonation or higher by caller, instead, it is set to {0}", windowsIdentity.ImpersonationLevel);
			throw new LdapException(48, AdwsStrings.ImpersonationLevelNotSetToImpersonate);
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "Dispose: disposing...");
				try
				{
					ClassManager.EnumerationContextCache.RemoveAssociatedEntries(this);
				}
				finally
				{
					disposed = true;
				}
				PerfCounters.OpenWebServiceSessions.Decrement();
			}
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADGroupMemberResponse GetADGroupMember(GetADGroupMemberRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADGroupMember: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.GetGroupMemberOpsRate.Increment();
		try
		{
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan maxGroupOrMemberEntriesTimeout = AdwsConstants.MaxGroupOrMemberEntriesTimeout;
			uint maxGroupOrMemberEntries = ConfigurationSettings.MaxGroupOrMemberEntries;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADGroupMember: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			if (request.PartitionDN == null || request.PartitionDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: req.PartitionDN is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(new ArgumentException(AdwsStrings.MustSupplyPartitionDn, "PartitionDN"));
			}
			if (request.GroupDN == null || request.GroupDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: req.GroupDN is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(new ArgumentException(AdwsStrings.MustSupplyGroupDnForGetADGroupMember, "GroupDN"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: instance {0} isn't NTDS or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADGroupMember: got request for {0} ({1}), isADLDS={2} PartitionDN:{3} GroupDN:{4} {5}", instanceType.InstanceName, request.Server, flag, request.PartitionDN, request.GroupDN, request.Recursive ? "recursive" : "non-recursive");
			GetADGroupMemberResponse getADGroupMemberResponse = new GetADGroupMemberResponse();
			PrincipalContext principalContext = null;
			try
			{
				VerifyImpersonationLevel();
				ContextType ctxType = ((!flag) ? ContextType.Domain : ContextType.ApplicationDirectory);
				principalContext = BuildPrincipalContext(ctxType, instanceType.Port, request.PartitionDN, out var hostname);
				using GroupPrincipal groupPrincipal = GroupPrincipal.FindByIdentity(principalContext, IdentityType.DistinguishedName, request.GroupDN);
				if (groupPrincipal == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: couldn't find Group Principal {0} in {1}", request.GroupDN, request.PartitionDN);
					throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(AdwsStrings.NoSuchGroup);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADGroupMember: found sdsamGroupPrincipal {0} ({1})", (groupPrincipal.DistinguishedName != null) ? groupPrincipal.DistinguishedName : "NULL", (groupPrincipal.SamAccountName != null) ? groupPrincipal.SamAccountName : "NULL");
				List<ActiveDirectoryPrincipal> list = new List<ActiveDirectoryPrincipal>();
				using (PrincipalSearchResult<Principal> principalSearchResult = groupPrincipal.GetMembers(request.Recursive))
				{
					uint num = 0u;
					foreach (Principal item in principalSearchResult)
					{
						using (item)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADGroupMember: member of {0} ({1})", (item.DistinguishedName != null) ? item.DistinguishedName : "NULL", (item.SamAccountName != null) ? item.SamAccountName : "NULL");
							VerifyLimitsExceededAndThrowWin32Exception(utcNow, maxGroupOrMemberEntriesTimeout, ++num, maxGroupOrMemberEntries);
							ActiveDirectoryPrincipal activeDirectoryPrincipal = new ActiveDirectoryPrincipal();
							PopulateActiveDirectoryPrincipalProperties(activeDirectoryPrincipal, item, hostname, instanceType);
							list.Add(activeDirectoryPrincipal);
						}
					}
				}
				getADGroupMemberResponse.Members = list.ToArray();
			}
			catch (MultipleMatchesException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got MultipleMatchesException: {0}", ex.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex);
			}
			catch (NoMatchingPrincipalException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got NoMatchingPrincipalException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex2);
			}
			catch (PrincipalOperationException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got PrincipalOperationException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex3);
			}
			catch (PrincipalException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got PrincipalException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex4);
			}
			catch (DirectoryServicesCOMException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got DirectoryServicesCOMException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex5);
			}
			catch (LdapException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: got LdapException: {0}", ex6.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex6);
			}
			catch (ArgumentException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: generating fault, got ArgumentException: {0}", ex7.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex7);
			}
			catch (InvalidOperationException ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: generating fault, got InvalidOperationException: {0}", ex8.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex8);
			}
			catch (Win32Exception ex9)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADGroupMember: generating fault, got Win32Exception: {0}", ex9.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADGroupMemberFault>(ex9.Message, ex9);
			}
			finally
			{
				if (principalContext != null)
				{
					principalContext.Dispose();
					principalContext = null;
				}
			}
			return getADGroupMemberResponse;
		}
		catch (Exception ex10)
		{
			if (!(ex10 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADGroupMember: fatal exception {0}", ex10.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADPrincipalGroupMembershipResponse GetADPrincipalGroupMembership(GetADPrincipalGroupMembershipRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.GetPrincGroupMembershipOpsRate.Increment();
		try
		{
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan maxGroupOrMemberEntriesTimeout = AdwsConstants.MaxGroupOrMemberEntriesTimeout;
			uint maxGroupOrMemberEntries = ConfigurationSettings.MaxGroupOrMemberEntries;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			if (request.PartitionDN == null || request.PartitionDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.PartitionDN is null or empty", "PartitionDN");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.MustSupplyPartitionDn));
			}
			if (request.PrincipalDN == null || request.PrincipalDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.PrincipalDN is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.MustSupplyPrincipalDn, "PrincipalDN"));
			}
			if (request.ResourceContextServer != null && request.ResourceContextServer.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.ResourceContextServer is empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.ResourceContextServerCannotBeEmptyForGetADPrincipalGroupMembership, "ResourceContextServer"));
			}
			if (request.ResourceContextPartition != null && request.ResourceContextPartition.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.ResourceContextPartition is empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.ResourceContextPartitionCannotBeEmptyForGetADPrincipalGroupMembership, "ResourceContextPartition"));
			}
			if ((request.ResourceContextServer != null && request.ResourceContextPartition == null) || (request.ResourceContextServer == null && request.ResourceContextPartition != null))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: req.ResourceContextPartition is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.MustFullySpecifyResourceContextForGetADPrincipalGroupMembership, "ResourceContextServer/ResourceContextPartition"));
			}
			bool flag = request.ResourceContextServer != null;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: PrincipalDN = '{0}', PartitionDN = '{1}'", request.PrincipalDN, request.PartitionDN);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: ResourceContextServer = '{0}', ResourceContextPartition = '{1}', useResourceContext = {2}", (request.ResourceContextServer != null) ? request.ResourceContextServer : "NULL", (request.ResourceContextPartition != null) ? request.ResourceContextPartition : "NULL", flag.ToString());
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: instance {0} isn't NTDS or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag2 = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got request for {0} ({1}), principalIsADLDS={2}", instanceType.InstanceName, request.Server, flag2);
			GetADPrincipalGroupMembershipResponse getADPrincipalGroupMembershipResponse = new GetADPrincipalGroupMembershipResponse();
			PrincipalContext principalContext = null;
			PrincipalContext principalContext2 = null;
			ContextType ctxType = ContextType.Domain;
			if (flag)
			{
				try
				{
					ctxType = ((DirectoryUtilities.DetermineTypeOfServer(request.ResourceContextServer, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, maxGroupOrMemberEntriesTimeout)) != DirectoryType.ADLDS) ? ContextType.Domain : ContextType.ApplicationDirectory);
				}
				catch (ArgumentException innerException)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: ResourceContextServer string '{0}' has bad format", request.ResourceContextServer);
					throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(new ArgumentException(AdwsStrings.ResourceContextServerHasBadFormatForGetADPrincipalGroupMembership, innerException));
				}
				catch (DirectoryException ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got DirectoryException '{0}' trying to check type of ResourceContextServer '{1}'", ex.ToString(), request.ResourceContextServer);
					throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(AdwsStrings.ResourceContextServerTypeCouldNotBeDeterminedForGetADPrincipalGroupMembership);
				}
				catch (InvalidOperationException)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: could not determine type of ResourceContextServer {0}", request.ResourceContextServer);
					throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(AdwsStrings.ResourceContextServerTypeCouldNotBeDeterminedForGetADPrincipalGroupMembership);
				}
			}
			try
			{
				VerifyImpersonationLevel();
				ContextType ctxType2 = ((!flag2) ? ContextType.Domain : ContextType.ApplicationDirectory);
				principalContext = BuildPrincipalContext(ctxType2, instanceType.Port, request.PartitionDN, out var hostname);
				using Principal principal = Principal.FindByIdentity(principalContext, IdentityType.DistinguishedName, request.PrincipalDN);
				if (principal == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: couldn't find principal {0} in {1}", request.PrincipalDN, request.PartitionDN);
					throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(AdwsStrings.NoSuchSecurityPrincipal);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: found principal {0} ({1})", (principal.DistinguishedName != null) ? principal.DistinguishedName : "NULL", (principal.SamAccountName != null) ? principal.SamAccountName : "NULL");
				PrincipalSearchResult<Principal> principalSearchResult = null;
				if (request.ResourceContextServer != null && request.ResourceContextPartition != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: using resource server {0}, dn {1}", request.ResourceContextServer, request.ResourceContextPartition);
					principalContext2 = DirectoryUtilities.CreatePrincipalContext(ctxType, request.ResourceContextServer, request.ResourceContextPartition);
					principalSearchResult = principal.GetGroups(principalContext2);
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: using default resource context");
					principalSearchResult = principal.GetGroups();
				}
				List<ActiveDirectoryGroup> list = new List<ActiveDirectoryGroup>();
				using (principalSearchResult)
				{
					uint num = 0u;
					foreach (GroupPrincipal item in principalSearchResult)
					{
						using (item)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: member of {0} ({1})", (item.DistinguishedName != null) ? item.DistinguishedName : "NULL", (item.SamAccountName != null) ? item.SamAccountName : "NULL");
							VerifyLimitsExceededAndThrowWin32Exception(utcNow, maxGroupOrMemberEntriesTimeout, ++num, maxGroupOrMemberEntries);
							ActiveDirectoryGroup activeDirectoryGroup = new ActiveDirectoryGroup();
							PopulateActiveDirectoryGroupProperties(activeDirectoryGroup, item, hostname, instanceType);
							list.Add(activeDirectoryGroup);
						}
					}
				}
				getADPrincipalGroupMembershipResponse.MemberOf = list.ToArray();
			}
			catch (MultipleMatchesException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got MultipleMatchesException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex3);
			}
			catch (NoMatchingPrincipalException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got NoMatchingPrincipalException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex4);
			}
			catch (PrincipalOperationException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got PrincipalOperationException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex5);
			}
			catch (PrincipalException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got PrincipalException: {0}", ex6.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex6);
			}
			catch (DirectoryServicesCOMException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got DirectoryServicesCOMException: {0}", ex7.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex7);
			}
			catch (LdapException ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got LdapException: {0}", ex8.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex8);
			}
			catch (ActiveDirectoryObjectNotFoundException ex9)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got ActiveDirectoryObjectNotFoundException: {0}", ex9.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex9);
			}
			catch (ActiveDirectoryServerDownException ex10)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: got ActiveDirectoryServerDownException: {0}", ex10.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex10);
			}
			catch (ArgumentException ex11)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: generating fault, got ArgumentException: {0}", ex11.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex11);
			}
			catch (InvalidOperationException ex12)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: generating fault, got InvalidOperationException: {0}", ex12.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex12);
			}
			catch (Win32Exception ex13)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: generating fault, got Win32Exception: {0}", ex13.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalGroupMembershipFault>(ex13.Message, ex13);
			}
			finally
			{
				if (principalContext2 != null)
				{
					principalContext2.Dispose();
					principalContext2 = null;
				}
				if (principalContext != null)
				{
					principalContext.Dispose();
					principalContext = null;
				}
			}
			return getADPrincipalGroupMembershipResponse;
		}
		catch (Exception ex14)
		{
			if (!(ex14 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADPrincipalGroupMembership: fatal exception {0}", ex14.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	private PrincipalContext BuildPrincipalContext(ContextType ctxType, int port, string partitionDN, out string hostname)
	{
		hostname = "localhost";
		if (ctxType == ContextType.ApplicationDirectory)
		{
			hostname = hostname + ":" + port;
		}
		return DirectoryUtilities.CreatePrincipalContext(ctxType, hostname, partitionDN);
	}

	private ActiveDirectoryGroupScope TranslateGroupScope(GroupScope scope)
	{
		switch (scope)
		{
		case GroupScope.Local:
			return ActiveDirectoryGroupScope.DomainLocal;
		case GroupScope.Global:
			return ActiveDirectoryGroupScope.Global;
		case GroupScope.Universal:
			return ActiveDirectoryGroupScope.Universal;
		default:
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateGroupScope: unknown scope {0}", scope.ToString());
			throw new InvalidOperationException(AdwsStrings.UnknownScopeForGetADPrincipalGroupMembership);
		}
	}

	private void PopulateObjectClasses(Principal principal, out string[] objectClasses)
	{
		objectClasses = null;
		DirectoryEntry directoryEntry = null;
		try
		{
			directoryEntry = (DirectoryEntry)principal.GetUnderlyingObject();
		}
		catch (InvalidOperationException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "PopulateObjectClasses: got InvalidOperationException: {0}", ex.ToString());
			objectClasses = new string[0];
			return;
		}
		if (directoryEntry == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "PopulateObjectClasses: no DirectoryEntry for {0}", principal.Name);
			objectClasses = new string[0];
			return;
		}
		PropertyValueCollection propertyValueCollection = directoryEntry.Properties["objectClass"];
		if (propertyValueCollection == null || propertyValueCollection.Count <= 0)
		{
			return;
		}
		List<string> list = new List<string>(propertyValueCollection.Count);
		foreach (object item in propertyValueCollection)
		{
			if (item is string)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "PopulateObjectClasses: adding class {0}", (string)item);
				list.Add((string)item);
			}
		}
		objectClasses = list.ToArray();
	}

	private string GetLocalDnsDomainName()
	{
		if (localDnsDomainName == null)
		{
			lock (localDnsDomainNameLock)
			{
				if (localDnsDomainName == null)
				{
					try
					{
						using DomainController domainController = DomainController.GetDomainController(DirectoryUtilities.CreateDirectoryContext("localhost"));
						using Domain domain = domainController.Domain;
						localDnsDomainName = domain.Name;
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: Retrieved domain name '{0}'", (localDnsDomainName != null) ? localDnsDomainName : "NULL");
					}
					catch (ActiveDirectoryObjectNotFoundException ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got ActiveDirectoryObjectNotFoundException: {0}", ex.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex);
					}
					catch (ActiveDirectoryOperationException ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got ActiveDirectoryOperationException: {0}", ex2.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex2);
					}
					catch (ActiveDirectoryServerDownException ex3)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got ActiveDirectoryServerDownException: {0}", ex3.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex3);
					}
					catch (OutOfMemoryException ex4)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got OutOfMemoryException: {0}", ex4.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex4);
					}
					catch (AuthenticationException ex5)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got AuthenticationException: {0}", ex5.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex5);
					}
					catch (UnauthorizedAccessException ex6)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got UnauthorizedAccessException: {0}", ex6.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex6);
					}
					catch (InvalidOperationException ex7)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetLocalDnsDomainName: got InvalidOperationException: {0}", ex7.ToString());
						throw new InvalidOperationException(AdwsStrings.CouldNotRetrieveLocalDomainName, ex7);
					}
				}
			}
		}
		return localDnsDomainName;
	}

	internal void PopulateActiveDirectoryGroupProperties(ActiveDirectoryGroup adGroup, GroupPrincipal sdsamGroup, string hostname, InstanceType instance)
	{
		adGroup.GroupType = ActiveDirectoryGroupType.Unknown;
		if (sdsamGroup.IsSecurityGroup.HasValue)
		{
			adGroup.GroupType = ((!sdsamGroup.IsSecurityGroup.Value) ? ActiveDirectoryGroupType.Distribution : ActiveDirectoryGroupType.Security);
		}
		adGroup.GroupScope = ActiveDirectoryGroupScope.Unknown;
		if (sdsamGroup.GroupScope.HasValue)
		{
			adGroup.GroupScope = TranslateGroupScope(sdsamGroup.GroupScope.Value);
		}
		PopulateActiveDirectoryPrincipalProperties(adGroup, sdsamGroup, hostname, instance);
	}

	internal void PopulateActiveDirectoryPrincipalProperties(ActiveDirectoryPrincipal adPrincipal, Principal sdsamPrincipal, string hostname, InstanceType instance)
	{
		adPrincipal.SamAccountName = sdsamPrincipal.SamAccountName;
		adPrincipal.SID = null;
		if (sdsamPrincipal.Sid != null)
		{
			byte[] array = new byte[sdsamPrincipal.Sid.BinaryLength];
			sdsamPrincipal.Sid.GetBinaryForm(array, 0);
			adPrincipal.SID = array;
		}
		PopulateActiveDirectoryObjectProperties(adPrincipal, sdsamPrincipal, hostname, instance);
	}

	internal void PopulateActiveDirectoryObjectProperties(ActiveDirectoryObject adObject, Principal sdsamPrincipal, string hostname, InstanceType instance)
	{
		adObject.ObjectGuid = Guid.Empty;
		if (sdsamPrincipal.Guid.HasValue)
		{
			adObject.ObjectGuid = sdsamPrincipal.Guid.Value;
		}
		adObject.Name = sdsamPrincipal.Name;
		adObject.DistinguishedName = sdsamPrincipal.DistinguishedName;
		adObject.ObjectClass = sdsamPrincipal.StructuralObjectClass;
		PopulateObjectClasses(sdsamPrincipal, out adObject.ObjectTypes);
		if (sdsamPrincipal.Context.ContextType == ContextType.Domain)
		{
			adObject.ReferenceServer = sdsamPrincipal.Context.Name;
			if (adObject.ReferenceServer != null && adObject.ReferenceServer.Equals(hostname, StringComparison.InvariantCultureIgnoreCase))
			{
				adObject.ReferenceServer = GetLocalDnsDomainName();
			}
		}
		else
		{
			adObject.ReferenceServer = sdsamPrincipal.Context.ConnectedServer;
			if (adObject.ReferenceServer.IndexOf(':') == -1)
			{
				adObject.ReferenceServer = adObject.ReferenceServer + ":" + instance.Port;
			}
		}
	}

	internal void VerifyLimitsExceededAndThrowWin32Exception(DateTime startTime, TimeSpan totalTimeAllowed, uint currNumEntries, uint maxEntries)
	{
		if (DirectoryUtilities.GetTimeRemaining(startTime, totalTimeAllowed).Ticks == 0L)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyLimitsExceededAndThrowFaultException: no time remaining, throwing exception");
			throw new Win32Exception(8226, AdwsStrings.OperationTimeout);
		}
		if (currNumEntries > maxEntries)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "VerifyLimitsExceededAndThrowFaultException: exceeded max entries, throwing exception");
			throw new Win32Exception(8227, AdwsStrings.MaxGroupOrMemberEntriesExceeded);
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public SetPasswordResponse SetPassword(SetPasswordRequest request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.SetPassOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "SetPassword: request is null");
				throw new ArgumentNullException("request");
			}
			SetOrChangePasswordCommon<SetPasswordFault>(isSetPassword: true, request.Server, request.PartitionDN, request.AccountDN, null, request.NewPassword);
			return new SetPasswordResponse();
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public ChangePasswordResponse ChangePassword(ChangePasswordRequest request)
	{
		CheckDisposed();
		EnterMethod();
		PerfCounters.ChangePassOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "ChangePassword: request is null");
				throw new ArgumentNullException("request");
			}
			SetOrChangePasswordCommon<ChangePasswordFault>(isSetPassword: false, request.Server, request.PartitionDN, request.AccountDN, request.OldPassword, request.NewPassword);
			return new ChangePasswordResponse();
		}
		finally
		{
			ExitMethod();
		}
	}

	private void SetOrChangePasswordCommon<T>(bool isSetPassword, string server, string partitionDN, string accountDN, string oldPassword, string newPassword) where T : CustomActionFault, new()
	{
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		if (server == null || server.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: server is null or empty");
			throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
		}
		if (partitionDN == null || partitionDN.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: partitionDN is null or empty");
			throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.MustSupplyPartitionDn, "PartitionDN"));
		}
		if (accountDN == null || accountDN.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: accountDN is null or empty");
			throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.MustSupplyAccountDnForSetPasswordorChangePassword, "AccountDN"));
		}
		if (!isSetPassword && oldPassword == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: oldPassword is null");
			throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.MustSupplyOldPasswordForChangePassword, "OldPassword"));
		}
		if (newPassword == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: newPassword is null");
			throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.MustSupplyNewPasswordForSetPasswordorChangePassword, "NewPassword"));
		}
		try
		{
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: unknown instance {0}", server);
				throw FaultGenerator.GenerateCustomActionFault<T>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: instance {0} isn't NTDS or AD LDS, is {1} instead", server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<T>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: got request for {0} ({1}), isADLDS={2} PartitionDN:{3} AccountDN:{4}", instanceType.InstanceName, server, flag, partitionDN, accountDN);
			ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> customActionConnCacheEntry = null;
			LdapConnection ldapConnection = null;
			try
			{
				VerifyImpersonationLevel();
				customActionConnCacheEntry = CustomActionCaches.GetLdapConnection(instanceType);
				ldapConnection = customActionConnCacheEntry.ConnectionObject;
				string filter = "(distinguishedName=" + accountDN + ")";
				SearchResultEntry searchResultEntry = SearchObject(ldapConnection, instanceType, partitionDN, filter, new string[1] { "objectClass" }, System.DirectoryServices.Protocols.SearchScope.Subtree, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
				if (searchResultEntry == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: couldn't find object {0} in {1}", accountDN, partitionDN);
					throw FaultGenerator.GenerateCustomActionFault<T>(AdwsStrings.NoSuchAuthenticablePrincipal);
				}
				if (!flag)
				{
					string[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "objectClass");
					if (multiValuedAttrVal == null || multiValuedAttrVal.Length == 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: couldn't get objectclasses for object {0}", accountDN);
						throw FaultGenerator.GenerateCustomActionFault<T>(AdwsStrings.NoSuchAuthenticablePrincipal);
					}
					Predicate<string> match = (string oc) => string.Compare(oc, "user", ignoreCase: true, CultureInfo.InvariantCulture) == 0;
					Predicate<string> match2 = (string oc) => string.Compare(oc, "computer", ignoreCase: true, CultureInfo.InvariantCulture) == 0;
					if (Array.FindIndex(multiValuedAttrVal, match) == -1 || (!isSetPassword && Array.FindIndex(multiValuedAttrVal, match2) != -1))
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: object {0} is not a valid principal", accountDN);
						throw FaultGenerator.GenerateCustomActionFault<T>(AdwsStrings.NoSuchAuthenticablePrincipal);
					}
				}
				System.DirectoryServices.Protocols.ModifyRequest modifyRequest = new System.DirectoryServices.Protocols.ModifyRequest();
				modifyRequest.DistinguishedName = accountDN;
				if (isSetPassword)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: performing Set Password");
					DirectoryAttributeModification directoryAttributeModification = new DirectoryAttributeModification();
					AddPasswordToDirAttrMod(directoryAttributeModification, newPassword, flag);
					directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Replace;
					modifyRequest.Modifications.Add(directoryAttributeModification);
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: performing Change Password");
					DirectoryAttributeModification directoryAttributeModification2 = new DirectoryAttributeModification();
					AddPasswordToDirAttrMod(directoryAttributeModification2, oldPassword, flag);
					directoryAttributeModification2.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Delete;
					DirectoryAttributeModification directoryAttributeModification3 = new DirectoryAttributeModification();
					AddPasswordToDirAttrMod(directoryAttributeModification3, newPassword, flag);
					directoryAttributeModification3.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Add;
					modifyRequest.Modifications.Add(directoryAttributeModification2);
					modifyRequest.Modifications.Add(directoryAttributeModification3);
				}
				DirectoryUtilities.CheckForReferralsAndThrow((ModifyResponse)ldapConnection.SendRequest(modifyRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout)), "SetOrChangePasswordCommon", accountDN, instanceType);
			}
			catch (DirectoryOperationException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: got DirectoryOperationException: {0}", ex.ToString());
				throw FaultGenerator.GenerateCustomActionFault<T>(ex);
			}
			catch (LdapException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: got LdapException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<T>(ex2);
			}
			catch (ArgumentException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: generating fault, got ArgumentException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<T>(ex3);
			}
			finally
			{
				if (customActionConnCacheEntry != null)
				{
					CustomActionCaches.ReleaseLdapConnection(customActionConnCacheEntry);
					customActionConnCacheEntry = null;
					ldapConnection = null;
				}
			}
		}
		catch (Exception ex4)
		{
			if (!(ex4 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "SetOrChangePasswordCommon: fatal exception {0}", ex4.ToString());
			}
			throw;
		}
	}

	private void AddPasswordToDirAttrMod(DirectoryAttributeModification pwdMod, string password, bool isADLDS)
	{
		if (isADLDS)
		{
			pwdMod.Name = "userPassword";
			if (password.Length > 0)
			{
				pwdMod.Add(password);
			}
		}
		else
		{
			string s = "\"" + password + "\"";
			byte[] bytes = Encoding.Unicode.GetBytes(s);
			pwdMod.Name = "unicodePwd";
			pwdMod.Add(bytes);
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADPrincipalAuthorizationGroupResponse GetADPrincipalAuthorizationGroup(GetADPrincipalAuthorizationGroupRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.GetPrincAuthzGroupOpsRate.Increment();
		try
		{
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan maxGroupOrMemberEntriesTimeout = AdwsConstants.MaxGroupOrMemberEntriesTimeout;
			uint maxGroupOrMemberEntries = ConfigurationSettings.MaxGroupOrMemberEntries;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			if (request.PartitionDN == null || request.PartitionDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: req.PartitionDN is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(new ArgumentException(AdwsStrings.MustSupplyPartitionDn, "PartitionDN"));
			}
			if (request.PrincipalDN == null || request.PrincipalDN.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: req.PrincipalDN is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(new ArgumentException(AdwsStrings.MustSupplyPrincipalDn, "PrincipalDN"));
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: PrincipalDN = '{0}', PartitionDN = '{1}'", request.PrincipalDN, request.PartitionDN);
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: instance {0} isn't NTDS or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got request for {0} ({1}), isADLDS={2}", instanceType.InstanceName, request.Server, flag);
			GetADPrincipalAuthorizationGroupResponse getADPrincipalAuthorizationGroupResponse = new GetADPrincipalAuthorizationGroupResponse();
			PrincipalContext principalContext = null;
			try
			{
				VerifyImpersonationLevel();
				ContextType ctxType = ((!flag) ? ContextType.Domain : ContextType.ApplicationDirectory);
				principalContext = BuildPrincipalContext(ctxType, instanceType.Port, request.PartitionDN, out var hostname);
				using CustomUserPrincipal customUserPrincipal = CustomUserPrincipal.FindByIdentity(principalContext, IdentityType.DistinguishedName, request.PrincipalDN);
				if (customUserPrincipal == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: couldn't find principal {0} in {1}", request.PrincipalDN, request.PartitionDN);
					throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(AdwsStrings.NoSuchSecurityPrincipal);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: found principal {0} ({1})", (customUserPrincipal.DistinguishedName != null) ? customUserPrincipal.DistinguishedName : "NULL", (customUserPrincipal.SamAccountName != null) ? customUserPrincipal.SamAccountName : "NULL");
				PrincipalSearchResult<Principal> authorizationGroups = customUserPrincipal.GetAuthorizationGroups();
				List<ActiveDirectoryGroup> list = new List<ActiveDirectoryGroup>();
				using (authorizationGroups)
				{
					uint num = 0u;
					foreach (GroupPrincipal item in authorizationGroups)
					{
						using (item)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: member of {0} ({1})", (item.DistinguishedName != null) ? item.DistinguishedName : "NULL", (item.SamAccountName != null) ? item.SamAccountName : "NULL");
							VerifyLimitsExceededAndThrowWin32Exception(utcNow, maxGroupOrMemberEntriesTimeout, ++num, maxGroupOrMemberEntries);
							ActiveDirectoryGroup activeDirectoryGroup = new ActiveDirectoryGroup();
							PopulateActiveDirectoryGroupProperties(activeDirectoryGroup, item, hostname, instanceType);
							list.Add(activeDirectoryGroup);
						}
					}
				}
				getADPrincipalAuthorizationGroupResponse.MemberOf = list.ToArray();
			}
			catch (MultipleMatchesException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got MultipleMatchesException: {0}", ex.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex);
			}
			catch (NoMatchingPrincipalException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got NoMatchingPrincipalException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex2);
			}
			catch (PrincipalOperationException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got PrincipalOperationException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex3);
			}
			catch (PrincipalException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got PrincipalException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex4);
			}
			catch (DirectoryServicesCOMException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got DirectoryServicesCOMException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex5);
			}
			catch (LdapException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got LdapException: {0}", ex6.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex6);
			}
			catch (ActiveDirectoryServerDownException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: got ActiveDirectoryServerDownException: {0}", ex7.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex7);
			}
			catch (ArgumentException ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: generating fault, got ArgumentException: {0}", ex8.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex8);
			}
			catch (InvalidOperationException ex9)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: generating fault, got InvalidOperationException: {0}", ex9.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex9);
			}
			catch (Win32Exception ex10)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: generating fault, got Win32Exception: {0}", ex10.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADPrincipalAuthorizationGroupFault>(ex10.Message, ex10);
			}
			finally
			{
				if (principalContext != null)
				{
					principalContext.Dispose();
					principalContext = null;
				}
			}
			return getADPrincipalAuthorizationGroupResponse;
		}
		catch (Exception ex11)
		{
			if (!(ex11 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADPrincipalAuthorizationGroup: fatal exception {0}", ex11.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public TranslateNameResponse TranslateName(TranslateNameRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "TranslateName: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.TranslateNameOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "TranslateName: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			if (request.Names == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: req.Names is null");
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.MustSupplyNamesForTranslateName, "Names"));
			}
			string[] names = request.Names;
			for (int i = 0; i < names.Length; i++)
			{
				if (names[i] == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: req.Names has a null element");
					throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.MustSupplyNonNullNameForTranslateName, "Names"));
				}
			}
			if (!Enum.IsDefined(typeof(ActiveDirectoryNameFormat), request.FormatOffered))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: req.FormatOffered is not supported ({0})", request.FormatOffered.ToString());
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.UnsupportedInputFormatForTranslateName, "FormatOffered"));
			}
			if (!Enum.IsDefined(typeof(ActiveDirectoryNameFormat), request.FormatDesired))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: req.FormatDesired is not supported ({0})", request.FormatDesired.ToString());
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.UnsupportedOutputFormatForTranslateName, "FormatDesired"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS && instanceType.DirectoryType != DirectoryType.GC)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: instance {0} isn't NTDS, GC, or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(new ArgumentException(AdwsStrings.NonNTDSOrGCOrADLDSInstanceInTheHeader, "Server"));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			bool flag2 = instanceType.DirectoryType == DirectoryType.GC;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "TranslateName: got request for {0} ({1}), isADLDS={2}, isGC={3}", instanceType.InstanceName, request.Server, flag, flag2);
			TranslateNameResponse translateNameResponse = new TranslateNameResponse();
			try
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "TranslateName: {0} names to translate", request.Names.Length);
				VerifyImpersonationLevel();
				NativeMethods.DS_NAME_FLAGS flags = (flag2 ? NativeMethods.DS_NAME_FLAGS.DS_NAME_FLAG_GCVERIFY : NativeMethods.DS_NAME_FLAGS.DS_NAME_NO_FLAGS);
				NativeMethods.DS_NAME_FORMAT formatOffered = MapNameFormat(request.FormatOffered);
				NativeMethods.DS_NAME_FORMAT formatDesired = MapNameFormat(request.FormatDesired);
				ActiveDirectoryNameTranslateResult[] nameTranslateResult;
				try
				{
					nameTranslateResult = DirectoryUtilities.CrackNames(instanceType, flags, formatOffered, formatDesired, request.Names);
				}
				catch (Win32Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: can't DsCrackNames, err={0}", ex.NativeErrorCode);
					throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(AdwsStrings.CantCrackNamesForTranslateName, ex);
				}
				translateNameResponse.NameTranslateResult = nameTranslateResult;
			}
			catch (LdapException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: got LdapException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(ex2);
			}
			catch (ArgumentException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateName: generating fault, got ArgumentException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<TranslateNameFault>(ex3);
			}
			return translateNameResponse;
		}
		catch (Exception ex4)
		{
			if (!(ex4 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "TranslateName: fatal exception {0}", ex4.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	private NativeMethods.DS_NAME_FORMAT MapNameFormat(ActiveDirectoryNameFormat format)
	{
		NativeMethods.DS_NAME_FORMAT result;
		switch (format)
		{
		case ActiveDirectoryNameFormat.DistinguishedName:
			result = NativeMethods.DS_NAME_FORMAT.DS_FQDN_1779_NAME;
			break;
		case ActiveDirectoryNameFormat.CanonicalName:
			result = NativeMethods.DS_NAME_FORMAT.DS_CANONICAL_NAME;
			break;
		default:
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MapNameFormat: unknown format {0}", format.ToString());
			throw new ArgumentException(AdwsStrings.UnknownFormatForNameTranslate, "FormatOffered/FormatDesired");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MapNameFormat: got {0}, returning {1}", format.ToString(), result.ToString());
		return result;
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADDomainControllerResponse GetADDomainController(GetADDomainControllerRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: entered");
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		EnterMethod();
		PerfCounters.GetDomCtrlOpsRate.Increment();
		try
		{
			TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.NtdsSettingsDN == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: req.NtdsSettingsDN is null");
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(new ArgumentException(AdwsStrings.MustSupplyNtdsSettingsDNArrayForGetADDomainController, "NtdsSettingsDN"));
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: instance {0} isn't NTDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(new ArgumentException(AdwsStrings.NonNTDSInstanceInTheHeader, "Server"));
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: got request for {0} ({1})", instanceType.InstanceName, request.Server);
			GetADDomainControllerResponse getADDomainControllerResponse = new GetADDomainControllerResponse();
			getADDomainControllerResponse.DomainControllers = new ActiveDirectoryDomainController[request.NtdsSettingsDN.Length];
			int num = 0;
			ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> customActionConnCacheEntry = null;
			LdapConnection ldapConnection = null;
			try
			{
				VerifyImpersonationLevel();
				customActionConnCacheEntry = CustomActionCaches.GetLdapConnection(instanceType);
				ldapConnection = customActionConnCacheEntry.ConnectionObject;
				string configurationNamingContext;
				try
				{
					GetNamingContexts(ldapConnection, instanceType, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), out var _, out configurationNamingContext, out var _);
				}
				catch (InvalidOperationException e)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve rootDSE");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(e);
				}
				if (configurationNamingContext == null || configurationNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve configurationNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveConfigurationNamingContext);
				}
				string text = "CN=Sites," + configurationNamingContext;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: using Sites container '{0}'", text);
				string text2 = "CN=Enterprise Configuration,CN=Partitions," + configurationNamingContext;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: using Enterprise Configuration DN '{0}'", text2);
				string schemaHolder;
				string domainHolder;
				string pdcHolder;
				string ridHolder;
				string infraHolder;
				try
				{
					GetADDSRoleHolders(instanceType, out schemaHolder, out domainHolder, out pdcHolder, out ridHolder, out infraHolder);
				}
				catch (Win32Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve AD DS roles: {0}", ex.NativeErrorCode);
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveAddsFsmoRoles);
				}
				for (int i = 0; i < request.NtdsSettingsDN.Length; i++)
				{
					string text3 = request.NtdsSettingsDN[i];
					if (text3 == null || text3.Length == 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: req.NtdsSettingsDN[] contains null or empty NtdsSettingsDN ");
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(new ArgumentException(AdwsStrings.MustSupplyNtdsSettingsDNArrayForGetADDomainController, "NtdsSettingsDN"));
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: processing NtdsSettingsDN={0}", text3);
					string[] attrsToRetrieve = new string[11]
					{
						"invocationId", "distinguishedName", "parentGUID", "msDS-hasDomainNCs", "options", "msDS-hasMasterNCs", "msDS-hasFullReplicaNCs", "hasPartialReplicaNCs", "msDS-PortLDAP", "msDS-PortSSL",
						"msDS-DefaultNamingContext"
					};
					SearchResultEntry objectByClass = GetObjectByClass(ldapConnection, instanceType, text3, "ntdsDsa", attrsToRetrieve, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					if (objectByClass == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve NTDS Settings object {0}", text3);
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveNtdsSettingsObjectForGetADDomainController);
					}
					string[] attrsToRetrieve2 = new string[3] { "distinguishedName", "objectGUID", "serverReference" };
					SearchResultEntry parentObject = GetParentObject(ldapConnection, instanceType, text3, "(objectClass=*)", attrsToRetrieve2, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					if (parentObject == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve Server object for {0}", text3);
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveServerObjectForGetADDomainController);
					}
					string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject, "serverReference");
					if (singleValuedAttrVal == null || singleValuedAttrVal.Length == 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve serverReference from Server object {0}", parentObject.DistinguishedName);
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveServerReferenceFromServerObjectForGetADDomainController);
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: got Computer DN={0}", singleValuedAttrVal);
					string[] attrsToRetrieve3 = new string[8] { "dnsHostName", "samAccountName", "distinguishedName", "operatingSystem", "operatingSystemVersion", "operatingSystemServicePack", "operatingSystemHotfix", "userAccountControl" };
					SearchResultEntry objectByClass2 = GetObjectByClass(ldapConnection, instanceType, singleValuedAttrVal, "computer", attrsToRetrieve3, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					if (objectByClass2 == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve Computer object {0}", singleValuedAttrVal);
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveComputerObjectForGetADDomainController);
					}
					string[] attrsToRetrieve4 = new string[1] { "dnsRoot" };
					SearchResultEntry objectByClass3 = GetObjectByClass(ldapConnection, instanceType, text2, "crossRef", attrsToRetrieve4, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					if (objectByClass3 == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: couldn't retrieve enterprise config object {0}", text2);
						throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntRetrieveEnterpriseConfigObjectForGetADDomainController);
					}
					ActiveDirectoryDomainController activeDirectoryDomainController = new ActiveDirectoryDomainController();
					FillInDirectoryServerInformation(instanceType.DirectoryType, activeDirectoryDomainController, objectByClass, parentObject, objectByClass2, schemaHolder, domainHolder, pdcHolder, ridHolder, infraHolder);
					activeDirectoryDomainController.ComputerDN = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "distinguishedName");
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: ComputerDN={0}", (activeDirectoryDomainController.ComputerDN != null) ? activeDirectoryDomainController.ComputerDN : "NULL");
					activeDirectoryDomainController.Domain = null;
					string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass, "msDS-hasDomainNCs");
					if (singleValuedAttrVal2 != null && singleValuedAttrVal2.Length > 0)
					{
						try
						{
							activeDirectoryDomainController.Domain = DirectoryUtilities.GetDnsNameFromDN(instanceType, singleValuedAttrVal2);
						}
						catch (Win32Exception ex2)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: couldn't convert domain DN to DNS name: {0}", ex2.NativeErrorCode);
							throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(AdwsStrings.CouldntConvertDomainDnToDomainDnsNameForGetADDomainController);
						}
					}
					activeDirectoryDomainController.Forest = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass3, "dnsRoot");
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: Domain={0}, Forest={1}", (activeDirectoryDomainController.Domain != null) ? activeDirectoryDomainController.Domain : "NULL", (activeDirectoryDomainController.Forest != null) ? activeDirectoryDomainController.Forest : "NULL");
					string singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass, "options");
					int result = 0;
					activeDirectoryDomainController.IsGlobalCatalog = false;
					if (singleValuedAttrVal3 != null && int.TryParse(singleValuedAttrVal3, out result))
					{
						activeDirectoryDomainController.IsGlobalCatalog = (result & 1) == 1;
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: got IsGC: {0}", activeDirectoryDomainController.IsGlobalCatalog.ToString());
					}
					activeDirectoryDomainController.IsReadOnly = CheckObjectExistence(ldapConnection, instanceType, text3, "(objectCategory=ntdsDsaRo)", DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					activeDirectoryDomainController.OSName = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "operatingSystem");
					activeDirectoryDomainController.OSVersion = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "operatingSystemVersion");
					activeDirectoryDomainController.OSServicepack = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "operatingSystemServicePack");
					activeDirectoryDomainController.OSHotFix = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "operatingSystemHotfix");
					string singleValuedAttrVal4 = DirectoryUtilities.GetSingleValuedAttrVal<string>(objectByClass2, "userAccountControl");
					activeDirectoryDomainController.Enabled = true;
					if (singleValuedAttrVal4 != null && int.TryParse(singleValuedAttrVal4, out var result2))
					{
						activeDirectoryDomainController.Enabled = (result2 & 2) == 0;
					}
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomainController: OSName={0}, OSVersion={1}, OSServicePack={2}, OSHotFix={3}, Enabled={4}", (activeDirectoryDomainController.OSName != null) ? activeDirectoryDomainController.OSName : "NULL", (activeDirectoryDomainController.OSVersion != null) ? activeDirectoryDomainController.OSVersion : "NULL", (activeDirectoryDomainController.OSServicepack != null) ? activeDirectoryDomainController.OSServicepack : "NULL", (activeDirectoryDomainController.OSHotFix != null) ? activeDirectoryDomainController.OSHotFix : "NULL", activeDirectoryDomainController.Enabled);
					getADDomainControllerResponse.DomainControllers[num] = activeDirectoryDomainController;
					num++;
				}
			}
			catch (DirectoryOperationException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: got DirectoryOperationException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(ex3);
			}
			catch (LdapException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: got LdapException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(ex4);
			}
			catch (ArgumentException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomainController: got ArgumentException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainControllerFault>(ex5);
			}
			finally
			{
				if (customActionConnCacheEntry != null)
				{
					CustomActionCaches.ReleaseLdapConnection(customActionConnCacheEntry);
					customActionConnCacheEntry = null;
					ldapConnection = null;
				}
			}
			return getADDomainControllerResponse;
		}
		catch (Exception ex6)
		{
			if (!(ex6 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomainController: fatal exception {0}", ex6.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	private void FillInDirectoryServerInformation(DirectoryType dirType, ActiveDirectoryDirectoryServer dsInfo, SearchResultEntry ntdsSettingsEntry, SearchResultEntry serverEntry, SearchResultEntry computerEntry, string schemaHolder, string domainHolder, string pdcHolder, string ridHolder, string infraHolder)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: entered");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: NTDS Settings={0}", ntdsSettingsEntry.DistinguishedName);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: Server={0}", serverEntry.DistinguishedName);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: Computer={0}", (computerEntry != null) ? computerEntry.DistinguishedName : "NULL");
		dsInfo.LdapPort = AdwsConstants.AdLdapPort;
		dsInfo.SslPort = AdwsConstants.AdLdapsPort;
		string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(ntdsSettingsEntry, "msDS-PortLDAP");
		string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(ntdsSettingsEntry, "msDS-PortSSL");
		if (singleValuedAttrVal != null && int.TryParse(singleValuedAttrVal, out var result))
		{
			dsInfo.LdapPort = result;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: got alternative LDAP port {0}", result);
		}
		if (singleValuedAttrVal2 != null && int.TryParse(singleValuedAttrVal2, out result))
		{
			dsInfo.SslPort = result;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: got alternative SSL port {0}", result);
		}
		if (dirType == DirectoryType.NTDS)
		{
			dsInfo.HostName = DirectoryUtilities.GetSingleValuedAttrVal<string>(computerEntry, "dnsHostName");
			dsInfo.Name = DirectoryUtilities.GetSingleValuedAttrVal<string>(computerEntry, "samAccountName");
			if (dsInfo.Name != null && dsInfo.Name.Length > 0 && dsInfo.Name[dsInfo.Name.Length - 1] == '$')
			{
				dsInfo.Name = dsInfo.Name.Substring(0, dsInfo.Name.Length - 1);
			}
		}
		else
		{
			dsInfo.HostName = null;
			dsInfo.Name = null;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: HostName={0}, Name={1}", (dsInfo.HostName != null) ? dsInfo.HostName : "NULL", (dsInfo.Name != null) ? dsInfo.Name : "NULL");
		dsInfo.InvocationId = Guid.Empty;
		byte[] singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(ntdsSettingsEntry, "invocationId");
		if (singleValuedAttrVal3 != null && singleValuedAttrVal3.Length == 16)
		{
			dsInfo.InvocationId = new Guid(singleValuedAttrVal3);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: InvocationId={0}", dsInfo.InvocationId);
		dsInfo.NTDSSettingsObjectDN = DirectoryUtilities.GetSingleValuedAttrVal<string>(ntdsSettingsEntry, "distinguishedName");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: NTDSSettingsObjectDN={0}", (dsInfo.NTDSSettingsObjectDN != null) ? dsInfo.NTDSSettingsObjectDN : "NULL");
		dsInfo.ServerObjectDN = DirectoryUtilities.GetSingleValuedAttrVal<string>(serverEntry, "distinguishedName");
		dsInfo.ServerObjectGuid = Guid.Empty;
		byte[] singleValuedAttrVal4 = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(serverEntry, "objectGUID");
		if (singleValuedAttrVal4 != null && singleValuedAttrVal4.Length == 16)
		{
			dsInfo.ServerObjectGuid = new Guid(singleValuedAttrVal4);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: ServerObjectDN={0}, ServerObjectGuid={1}", (dsInfo.ServerObjectDN != null) ? dsInfo.ServerObjectDN : "NULL", dsInfo.ServerObjectGuid);
		GetPartitions(dsInfo, ntdsSettingsEntry);
		if (GlobalDebug.Info)
		{
			string[] partitions = dsInfo.Partitions;
			foreach (string text in partitions)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: partition={0}", (text != null) ? text : "NULL");
			}
		}
		if (dirType == DirectoryType.NTDS)
		{
			dsInfo.DefaultPartition = DirectoryUtilities.GetSingleValuedAttrVal<string>(ntdsSettingsEntry, "msDS-hasDomainNCs");
		}
		else
		{
			dsInfo.DefaultPartition = DirectoryUtilities.GetSingleValuedAttrVal<string>(ntdsSettingsEntry, "msDS-DefaultNamingContext");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: DefaultPartition={0}", (dsInfo.DefaultPartition != null) ? dsInfo.DefaultPartition : "NULL");
		if (dsInfo.ServerObjectDN != null && dsInfo.ServerObjectDN.Length > 0)
		{
			string rdnFromDn = DirectoryUtilities.GetRdnFromDn(dsInfo.ServerObjectDN, out var trimmedDn);
			if (trimmedDn != null && trimmedDn.Length > 0)
			{
				rdnFromDn = DirectoryUtilities.GetRdnFromDn(trimmedDn, out var trimmedDn2);
				if (trimmedDn2 != null && trimmedDn2.Length > 0)
				{
					rdnFromDn = DirectoryUtilities.GetRdnFromDn(trimmedDn2, out var _);
					if (rdnFromDn != null && rdnFromDn.Length >= 4 && rdnFromDn.StartsWith("CN=", ignoreCase: true, CultureInfo.InvariantCulture))
					{
						dsInfo.Site = rdnFromDn.Substring(3);
					}
				}
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInDirectoryServerInformation: Site={0}", (dsInfo.Site != null) ? dsInfo.Site : "NULL");
		if (dirType == DirectoryType.NTDS && dsInfo.NTDSSettingsObjectDN != null && dsInfo.NTDSSettingsObjectDN.Length > 0)
		{
			FillInADDSRoles(dsInfo, dsInfo.NTDSSettingsObjectDN, schemaHolder, domainHolder, pdcHolder, ridHolder, infraHolder);
		}
	}

	private void GetPartitions(ActiveDirectoryDirectoryServer dsInfo, SearchResultEntry ntdsSettingsEntry)
	{
		List<string> list = new List<string>();
		string[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<string>(ntdsSettingsEntry, "msDS-hasMasterNCs");
		string[] multiValuedAttrVal2 = DirectoryUtilities.GetMultiValuedAttrVal<string>(ntdsSettingsEntry, "msDS-hasFullReplicaNCs");
		string[] multiValuedAttrVal3 = DirectoryUtilities.GetMultiValuedAttrVal<string>(ntdsSettingsEntry, "hasPartialReplicaNCs");
		Utils.CaseInsensitiveStringComparer comparer = new Utils.CaseInsensitiveStringComparer();
		Utils.MergeArrayIntoList(list, multiValuedAttrVal, comparer);
		Utils.MergeArrayIntoList(list, multiValuedAttrVal2, comparer);
		Utils.MergeArrayIntoList(list, multiValuedAttrVal3, comparer);
		dsInfo.Partitions = new string[list.Count];
		list.CopyTo(dsInfo.Partitions, 0);
	}

	private void GetADDSRoleHolders(InstanceType instance, out string schemaHolder, out string domainHolder, out string pdcHolder, out string ridHolder, out string infraHolder)
	{
		ICustomActionConnCacheEntry<SidAndInstanceType, DsBindHandle> customActionConnCacheEntry = null;
		DsBindHandle dsBindHandle = null;
		schemaHolder = null;
		domainHolder = null;
		pdcHolder = null;
		ridHolder = null;
		infraHolder = null;
		try
		{
			try
			{
				customActionConnCacheEntry = CustomActionCaches.GetDsRpcBinding(instance);
			}
			catch (Win32Exception ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDSRoleHolders: can't GetDsRpcBinding, err={0}", ex.NativeErrorCode);
				throw;
			}
			dsBindHandle = customActionConnCacheEntry.ConnectionObject;
			IntPtr ppResult = IntPtr.Zero;
			try
			{
				uint num = NativeMethods.DsListRolesW(dsBindHandle.hDS, out ppResult);
				if (num != 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDSRoleHolders: can't DsListRolesW, err={0}", num);
					throw new Win32Exception((int)num);
				}
				NativeMethods.DS_NAME_RESULTW dS_NAME_RESULTW = (NativeMethods.DS_NAME_RESULTW)Marshal.PtrToStructure(ppResult, typeof(NativeMethods.DS_NAME_RESULTW));
				IntPtr ptr = dS_NAME_RESULTW.rItems;
				for (int i = 0; i < dS_NAME_RESULTW.cItems; i++)
				{
					NativeMethods.DS_NAME_RESULT_ITEMW dS_NAME_RESULT_ITEMW = (NativeMethods.DS_NAME_RESULT_ITEMW)Marshal.PtrToStructure(ptr, typeof(NativeMethods.DS_NAME_RESULT_ITEMW));
					if (dS_NAME_RESULT_ITEMW.status != 0 || dS_NAME_RESULT_ITEMW.pName == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDSRoleHolders: DsListRolesW didn't return value, status={0}", dS_NAME_RESULT_ITEMW.status);
					}
					switch ((NativeMethods.DS_ROLE)i)
					{
					case NativeMethods.DS_ROLE.DS_ROLE_SCHEMA_OWNER:
						schemaHolder = dS_NAME_RESULT_ITEMW.pName;
						break;
					case NativeMethods.DS_ROLE.DS_ROLE_DOMAIN_OWNER:
						domainHolder = dS_NAME_RESULT_ITEMW.pName;
						break;
					case NativeMethods.DS_ROLE.DS_ROLE_PDC_OWNER:
						pdcHolder = dS_NAME_RESULT_ITEMW.pName;
						break;
					case NativeMethods.DS_ROLE.DS_ROLE_RID_OWNER:
						ridHolder = dS_NAME_RESULT_ITEMW.pName;
						break;
					case NativeMethods.DS_ROLE.DS_ROLE_INFRASTRUCTURE_OWNER:
						infraHolder = dS_NAME_RESULT_ITEMW.pName;
						break;
					}
					ptr = (IntPtr)(ptr.ToInt64() + Marshal.SizeOf(typeof(NativeMethods.DS_NAME_RESULT_ITEMW)));
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDSRoleHolders: Schema Holder = {0}", (schemaHolder != null) ? schemaHolder : "NULL");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDSRoleHolders: Domain Holder = {0}", (domainHolder != null) ? domainHolder : "NULL");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDSRoleHolders: PDC Holder = {0}", (pdcHolder != null) ? pdcHolder : "NULL");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDSRoleHolders: RID Holder = {0}", (ridHolder != null) ? ridHolder : "NULL");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDSRoleHolders: Infra Holder = {0}", (infraHolder != null) ? infraHolder : "NULL");
			}
			finally
			{
				if (ppResult != IntPtr.Zero)
				{
					NativeMethods.DsFreeNameResultW(ppResult);
					ppResult = IntPtr.Zero;
				}
			}
		}
		finally
		{
			if (customActionConnCacheEntry != null)
			{
				CustomActionCaches.ReleaseDsRpcBinding(customActionConnCacheEntry);
				customActionConnCacheEntry = null;
				dsBindHandle = null;
			}
		}
	}

	private void FillInADDSRoles(ActiveDirectoryDirectoryServer dsInfo, string ntdsSettingsDn, string schemaHolder, string domainHolder, string pdcHolder, string ridHolder, string infraHolder)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "FillInADDSRoles: Checking roles for {0}", ntdsSettingsDn);
		List<ActiveDirectoryOperationMasterRole> list = new List<ActiveDirectoryOperationMasterRole>();
		if (schemaHolder != null && string.Compare(ntdsSettingsDn, schemaHolder, ignoreCase: true, CultureInfo.InvariantCulture) == 0)
		{
			list.Add(ActiveDirectoryOperationMasterRole.SchemaMaster);
		}
		if (domainHolder != null && string.Compare(ntdsSettingsDn, domainHolder, ignoreCase: true, CultureInfo.InvariantCulture) == 0)
		{
			list.Add(ActiveDirectoryOperationMasterRole.DomainNamingMaster);
		}
		if (pdcHolder != null && string.Compare(ntdsSettingsDn, pdcHolder, ignoreCase: true, CultureInfo.InvariantCulture) == 0)
		{
			list.Add(ActiveDirectoryOperationMasterRole.PDCEmulator);
		}
		if (ridHolder != null && string.Compare(ntdsSettingsDn, ridHolder, ignoreCase: true, CultureInfo.InvariantCulture) == 0)
		{
			list.Add(ActiveDirectoryOperationMasterRole.RIDMaster);
		}
		if (infraHolder != null && string.Compare(ntdsSettingsDn, infraHolder, ignoreCase: true, CultureInfo.InvariantCulture) == 0)
		{
			list.Add(ActiveDirectoryOperationMasterRole.InfrastructureMaster);
		}
		dsInfo.OperationMasterRole = new ActiveDirectoryOperationMasterRole[list.Count];
		list.CopyTo(dsInfo.OperationMasterRole, 0);
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public MoveADOperationMasterRoleResponse MoveADOperationMasterRole(MoveADOperationMasterRoleRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.MoveRoleOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: instance {0} isn't NTDS or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: got request for {0} ({1}), isADLDS={2}", instanceType.InstanceName, request.Server, flag);
			MoveADOperationMasterRoleResponse moveADOperationMasterRoleResponse = new MoveADOperationMasterRoleResponse();
			try
			{
				VerifyImpersonationLevel();
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: move role {0}, seize={1}", request.OperationMasterRole.ToString(), request.Seize);
				if (flag)
				{
					moveADOperationMasterRoleResponse.WasSeized = MoveRole(isADLDS: true, instanceType.Port, request.OperationMasterRole, request.Seize);
				}
				else
				{
					moveADOperationMasterRoleResponse.WasSeized = MoveRole(isADLDS: false, null, request.OperationMasterRole, request.Seize);
				}
			}
			catch (ActiveDirectoryOperationException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got ActiveDirectoryOperationException: {0}", ex.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex);
			}
			catch (ActiveDirectoryServerDownException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got ActiveDirectoryServerDownException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex2);
			}
			catch (OutOfMemoryException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got OutOfMemoryException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex3);
			}
			catch (AuthenticationException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got AuthenticationException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex4);
			}
			catch (UnauthorizedAccessException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got UnauthorizedAccessException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex5);
			}
			catch (InvalidOperationException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got InvalidOperationException: {0}", ex6.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex6);
			}
			catch (LdapException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: got LdapException: {0}", ex7.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex7);
			}
			catch (ArgumentException ex8)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: generating fault, got ArgumentException: {0}", ex8.ToString());
				throw FaultGenerator.GenerateCustomActionFault<MoveADOperationMasterRoleFault>(ex8);
			}
			return moveADOperationMasterRoleResponse;
		}
		catch (Exception ex9)
		{
			if (!(ex9 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "MoveADOperationMasterRole: fatal exception {0}", ex9.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	private bool MoveRole(bool isADLDS, int? ldapPort, ActiveDirectoryOperationMasterRole role, bool seize)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: entered, role={0}, seize={1}, isADLDS={2}", role.ToString(), seize, isADLDS);
		bool result = false;
		bool flag = false;
		ActiveDirectoryRole activeDirectoryRole = (ActiveDirectoryRole)(-1);
		AdamRole role2 = (AdamRole)(-1);
		if (isADLDS)
		{
			role2 = TranslateADLDSRole(role);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: port={0}, AD LDS fsmo={1}", ldapPort.Value, role2.ToString());
		}
		else
		{
			activeDirectoryRole = TranslateADDSRole(role);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: AD DS fsmo={0}", activeDirectoryRole.ToString());
		}
		DirectoryContext directoryContext = null;
		directoryContext = ((!isADLDS) ? DirectoryUtilities.CreateDirectoryContext("localhost") : DirectoryUtilities.CreateDirectoryContext("localhost:" + ldapPort.Value));
		DomainController domainController = null;
		AdamInstance adamInstance = null;
		Domain domain = null;
		try
		{
			if (isADLDS)
			{
				adamInstance = AdamInstance.GetAdamInstance(directoryContext);
			}
			else
			{
				domainController = DomainController.GetDomainController(directoryContext);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: trying to transfer role {0}", role.ToString());
			try
			{
				if (!isADLDS && activeDirectoryRole == ActiveDirectoryRole.PdcRole)
				{
					domain = domainController.Domain;
					TransferPdcFsmo(domainController, domain);
				}
				else if (!isADLDS)
				{
					domainController.TransferRoleOwnership(activeDirectoryRole);
				}
				else
				{
					adamInstance.TransferRoleOwnership(role2);
				}
				flag = true;
			}
			catch (ActiveDirectoryOperationException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveRole: got ActiveDirectoryOperationException trying to transfer");
				if (!seize)
				{
					throw;
				}
			}
			catch (ActiveDirectoryServerDownException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveRole: got ActiveDirectoryServerDownException trying to transfer");
				if (!seize)
				{
					throw;
				}
			}
			catch (InvalidOperationException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "MoveRole: got InvalidOperationException trying to transfer");
				if (!seize)
				{
					throw;
				}
			}
			if (!flag && seize)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: trying to seize role {0}", role.ToString());
				if (!isADLDS)
				{
					domainController.SeizeRoleOwnership(activeDirectoryRole);
				}
				else
				{
					adamInstance.SeizeRoleOwnership(role2);
				}
				result = true;
			}
		}
		finally
		{
			if (domain != null)
			{
				domain.Dispose();
				domain = null;
			}
			if (domainController != null)
			{
				domainController.Dispose();
				domainController = null;
			}
			if (adamInstance != null)
			{
				adamInstance.Dispose();
				adamInstance = null;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "MoveRole: successfully moved role {0}", role.ToString());
		return result;
	}

	private ActiveDirectoryRole TranslateADDSRole(ActiveDirectoryOperationMasterRole role)
	{
		switch (role)
		{
		case ActiveDirectoryOperationMasterRole.PDCEmulator:
			return ActiveDirectoryRole.PdcRole;
		case ActiveDirectoryOperationMasterRole.RIDMaster:
			return ActiveDirectoryRole.RidRole;
		case ActiveDirectoryOperationMasterRole.InfrastructureMaster:
			return ActiveDirectoryRole.InfrastructureRole;
		case ActiveDirectoryOperationMasterRole.SchemaMaster:
			return ActiveDirectoryRole.SchemaRole;
		case ActiveDirectoryOperationMasterRole.DomainNamingMaster:
			return ActiveDirectoryRole.NamingRole;
		default:
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateADDSRole: unknown role {0}", role.ToString());
			throw new ArgumentException(AdwsStrings.UnknownRoleForMoveADOperationMasterRole, "OperationMasterRole");
		}
	}

	private AdamRole TranslateADLDSRole(ActiveDirectoryOperationMasterRole role)
	{
		switch (role)
		{
		case ActiveDirectoryOperationMasterRole.SchemaMaster:
			return AdamRole.SchemaRole;
		case ActiveDirectoryOperationMasterRole.DomainNamingMaster:
			return AdamRole.NamingRole;
		default:
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TranslateADLDSRole: unknown role {0}", role.ToString());
			throw new ArgumentException(AdwsStrings.UnknownRoleForMoveADOperationMasterRole, "OperationMasterRole");
		}
	}

	private void TransferPdcFsmo(DomainController dc, Domain domain)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "TransferPdcFsmo: entered");
		try
		{
			DirectoryEntry directoryEntry = domain.GetDirectoryEntry();
			directoryEntry.RefreshCache(new string[1] { "objectSID" });
			PropertyValueCollection propertyValueCollection = directoryEntry.Properties["objectSID"];
			if (propertyValueCollection == null || propertyValueCollection.Count != 1)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TransferPdcFsmo: objectSid property was null or did not contain 1 value");
				throw new InvalidOperationException(AdwsStrings.ObjectSidCouldNotBeRetrievedForPdcFsmoTransfer);
			}
			byte[] array = (byte[])propertyValueCollection.Value;
			if (array == null || array.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TransferPdcFsmo: objectSid property value was null or empty");
				throw new InvalidOperationException(AdwsStrings.ObjectSidCouldNotBeRetrievedForPdcFsmoTransfer);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "TransferPdcFsmo: domain SID={0}", new SecurityIdentifier(array, 0).ToString());
			using DirectoryEntry directoryEntry2 = DirectoryUtilities.CreateDirectoryEntry("LDAP://" + dc.Name + "/rootDse");
			directoryEntry2.Properties["becomePdc"].Value = array;
			directoryEntry2.CommitChanges();
		}
		catch (DirectoryServicesCOMException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "TransferPdcFsmo: got a DirectoryServicesCOMException, hr='{0}', message='{1}'", ex.ErrorCode, ex.Message);
			throw new ActiveDirectoryOperationException(AdwsStrings.CouldNotTransferPdcFsmo, ex, ex.ErrorCode);
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADDomainResponse GetADDomain(GetADDomainRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: entered");
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		EnterMethod();
		PerfCounters.GetDomainOpsRate.Increment();
		try
		{
			TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: instance {0} isn't NTDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(new ArgumentException(AdwsStrings.NonNTDSInstanceInTheHeader, "Server"));
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: got request for {0} ({1})", instanceType.InstanceName, request.Server);
			GetADDomainResponse getADDomainResponse = new GetADDomainResponse();
			getADDomainResponse.Domain = new ActiveDirectoryDomain();
			ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> customActionConnCacheEntry = null;
			LdapConnection ldapConnection = null;
			try
			{
				VerifyImpersonationLevel();
				customActionConnCacheEntry = CustomActionCaches.GetLdapConnection(instanceType);
				ldapConnection = customActionConnCacheEntry.ConnectionObject;
				string defaultNamingContext;
				string configurationNamingContext;
				string rootDomainNamingContext;
				try
				{
					GetNamingContexts(ldapConnection, instanceType, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), out defaultNamingContext, out configurationNamingContext, out rootDomainNamingContext);
				}
				catch (InvalidOperationException e)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve rootDSE");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(e);
				}
				if (defaultNamingContext == null || defaultNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve defaultNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveDefaultNamingContext);
				}
				if (configurationNamingContext == null || configurationNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve configurationNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveConfigurationNamingContext);
				}
				if (rootDomainNamingContext == null || rootDomainNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve rootDomainNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveRootDomainNamingContext);
				}
				string pdcHolder;
				string ridHolder;
				string infraHolder;
				try
				{
					GetADDSRoleHolders(instanceType, out var _, out var _, out pdcHolder, out ridHolder, out infraHolder);
				}
				catch (Win32Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve AD DS roles: {0}", ex.NativeErrorCode);
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveAddsFsmoRoles);
				}
				string[] attrsToRetrieve = new string[13]
				{
					"msDS-AllowedDNSSuffixes", "objectSid", "objectGuid", "gpLink", "distinguishedName", "objectClass", "managedBy", "name", "subRefs", "msDS-LogonTimeSyncInterval",
					"wellKnownObjects", "msDS-Behavior-Version", "msDS-masteredBy"
				};
				SearchResultEntry searchResultEntry = GetObject(ldapConnection, instanceType, defaultNamingContext, "(objectClass=*)", attrsToRetrieve, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
				if (searchResultEntry == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve domain partition information");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveDomainPartition);
				}
				string[] attrsToRetrieve2 = new string[5] { "distinguishedName", "nETBIOSName", "dnsRoot", "nCName", "trustParent" };
				List<SearchResultEntry> list = SearchObjects(ldapConnection, instanceType, "CN=Partitions," + configurationNamingContext, "(systemflags:1.2.840.113556.1.4.803:=3)", attrsToRetrieve2, System.DirectoryServices.Protocols.SearchScope.OneLevel, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve domain NC crossref objects");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveDomainCrossRefs);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: found {0} domain crossrefs", list.Count);
				SearchResultEntry searchResultEntry2 = null;
				foreach (SearchResultEntry item in list)
				{
					string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(item, "ncName");
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: is this our domain: {0}", (singleValuedAttrVal == null) ? "-" : singleValuedAttrVal);
					if (singleValuedAttrVal != null && singleValuedAttrVal == defaultNamingContext)
					{
						searchResultEntry2 = item;
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: this our domain: {0}", (singleValuedAttrVal == null) ? "-" : singleValuedAttrVal);
						break;
					}
				}
				if (searchResultEntry2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve domain crossref object");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveDomainCrossRef);
				}
				SearchResultEntry searchResultEntry3 = null;
				string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "trustParent");
				string singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "distinguishedName");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: dn: {0} trustParent: {1}", (singleValuedAttrVal3 == null) ? "-" : singleValuedAttrVal3, (singleValuedAttrVal2 == null) ? "-" : singleValuedAttrVal2);
				if (singleValuedAttrVal2 != null)
				{
					foreach (SearchResultEntry item2 in list)
					{
						string singleValuedAttrVal4 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item2, "distinguishedName");
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: parentcheck: {0}", (singleValuedAttrVal4 == null) ? "-" : singleValuedAttrVal4);
						if (singleValuedAttrVal4 != null && singleValuedAttrVal4 == singleValuedAttrVal2)
						{
							searchResultEntry3 = item2;
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: found parent: {0}", (singleValuedAttrVal4 == null) ? "-" : singleValuedAttrVal4);
							break;
						}
					}
					if (searchResultEntry3 == null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve parent Xref object");
					}
				}
				IList<string> list2 = new List<string>();
				foreach (SearchResultEntry item3 in list)
				{
					singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item3, "trustParent");
					string singleValuedAttrVal5 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item3, "dnsRoot");
					if (singleValuedAttrVal5 != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: childcheck {0}", singleValuedAttrVal5);
						if (singleValuedAttrVal2 != null && singleValuedAttrVal2 == singleValuedAttrVal3)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: childmatch {0}", singleValuedAttrVal5);
							list2.Add(singleValuedAttrVal5);
						}
					}
					else
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve dnsRoot from a domain xRef");
					}
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: childList count: {0}", list2.Count);
				getADDomainResponse.Domain.ChildDomains = Utils.ToArray(list2);
				SearchResultEntry searchResultEntry4 = null;
				foreach (SearchResultEntry item4 in list)
				{
					string singleValuedAttrVal6 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item4, "ncName");
					if (singleValuedAttrVal6 != null && singleValuedAttrVal6 == rootDomainNamingContext)
					{
						searchResultEntry4 = item4;
						break;
					}
				}
				if (searchResultEntry4 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve rootdomain crossref object");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveRootDomainCrossRef);
				}
				Dictionary<string, string> dictionary = new Dictionary<string, string>();
				string[] multiValuedAttrVal = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "wellKnownObjects");
				if (multiValuedAttrVal == null || multiValuedAttrVal.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve well known objects");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveWellKnownObjects);
				}
				string[] array = multiValuedAttrVal;
				foreach (string input in array)
				{
					Match match = AdwsConstants.dnWithBinaryRegex.Match(input);
					try
					{
						dictionary[match.Groups["prefix"].Value] = match.Groups["dn"].Value;
					}
					catch (ArgumentException)
					{
					}
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: got all info, now processing");
				getADDomainResponse.Domain.Name = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "name");
				byte[] singleValuedAttrVal7 = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(searchResultEntry, "objectGuid");
				if (singleValuedAttrVal7 == null || singleValuedAttrVal7.Length != 16)
				{
					singleValuedAttrVal7 = null;
				}
				else
				{
					getADDomainResponse.Domain.ObjectGuid = new Guid(singleValuedAttrVal7);
				}
				string singleValuedAttrVal8 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "distinguishedName");
				getADDomainResponse.Domain.DistinguishedName = singleValuedAttrVal8;
				string[] multiValuedAttrVal2 = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "objectClass");
				if (multiValuedAttrVal2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve objectClass from domain parition object");
					throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(AdwsStrings.CouldntRetrieveObjectClassFromDomainPartitionObject);
				}
				getADDomainResponse.Domain.ObjectClass = multiValuedAttrVal2[multiValuedAttrVal2.Length - 1];
				getADDomainResponse.Domain.ObjectTypes = multiValuedAttrVal2;
				getADDomainResponse.Domain.ReferenceServer = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "dnsRoot");
				getADDomainResponse.Domain.DNSRoot = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "dnsRoot");
				getADDomainResponse.Domain.LostAndFoundContainer = searchDictionary(dictionary, "B:32:AB8153B7768811D1ADED00C04FD8D5CD:");
				getADDomainResponse.Domain.DeletedObjectsContainer = searchDictionary(dictionary, "B:32:18E2EA80684F11D2B9AA00C04F79F805:");
				getADDomainResponse.Domain.QuotasContainer = searchDictionary(dictionary, "B:32:6227F0AF1FC2410D8E3BB10615BB5B0F:");
				getADDomainResponse.Domain.SubordinateReferences = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "subRefs");
				IList<string> list3 = new List<string>();
				IList<string> list4 = new List<string>();
				string dn = "CN=Sites," + configurationNamingContext;
				string filter = "(&(objectClass=nTDSDSA)(msDS-hasDomainNCs=" + defaultNamingContext + "))";
				string[] attrsToRetrieve3 = new string[2] { "distinguishedName", "objectCategory" };
				List<SearchResultEntry> list5 = SearchObjects(ldapConnection, instanceType, dn, filter, attrsToRetrieve3, System.DirectoryServices.Protocols.SearchScope.Subtree, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list5 != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: found {0} DC and RODC candidates", list5.Count);
					foreach (SearchResultEntry item5 in list5)
					{
						string singleValuedAttrVal9 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item5, "distinguishedName");
						string singleValuedAttrVal10 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item5, "objectCategory");
						string trimmedDn = null;
						string rdnFromDn = DirectoryUtilities.GetRdnFromDn(singleValuedAttrVal10, out trimmedDn);
						if (singleValuedAttrVal9 == null || singleValuedAttrVal10 == null || singleValuedAttrVal10.Length <= 0 || rdnFromDn == null)
						{
							continue;
						}
						SearchResultEntry parentObject = GetParentObject(ldapConnection, instanceType, singleValuedAttrVal9, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
						if (parentObject == null)
						{
							continue;
						}
						string singleValuedAttrVal11 = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject, "dnsHostName");
						if (singleValuedAttrVal11 != null)
						{
							if (string.Compare(rdnFromDn, "CN=NTDS-DSA-RO", ignoreCase: true, CultureInfo.InvariantCulture) == 0)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: found RODC {0} objectCategory {1}", singleValuedAttrVal11, rdnFromDn);
								list4.Add(singleValuedAttrVal11);
							}
							else
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: found DC {0} objectCategory {1}", singleValuedAttrVal11, rdnFromDn);
								list3.Add(singleValuedAttrVal11);
							}
						}
					}
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: couldn't retrieve domain DC's and RODC's");
				}
				getADDomainResponse.Domain.ReplicaDirectoryServer = Utils.ToArray(list3);
				getADDomainResponse.Domain.ReadOnlyReplicaDirectoryServer = Utils.ToArray(list4);
				getADDomainResponse.Domain.ManagedBy = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "managedBy");
				string text = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "msDS-Behavior-Version");
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADDomain: domainMode: [{0}]", (text == null) ? "-" : text);
				if (text == null)
				{
					text = "0";
				}
				if (!int.TryParse(text, out getADDomainResponse.Domain.DomainMode))
				{
					getADDomainResponse.Domain.DomainMode = 0;
				}
				getADDomainResponse.Domain.InfrastructureMaster = null;
				if (infraHolder != null)
				{
					SearchResultEntry parentObject2 = GetParentObject(ldapConnection, instanceType, infraHolder, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					getADDomainResponse.Domain.InfrastructureMaster = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject2, "dnsHostName");
				}
				getADDomainResponse.Domain.RIDMaster = null;
				if (ridHolder != null)
				{
					SearchResultEntry parentObject3 = GetParentObject(ldapConnection, instanceType, ridHolder, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					getADDomainResponse.Domain.RIDMaster = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject3, "dnsHostName");
				}
				getADDomainResponse.Domain.PDCEmulator = null;
				if (pdcHolder != null)
				{
					SearchResultEntry parentObject4 = GetParentObject(ldapConnection, instanceType, pdcHolder, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					getADDomainResponse.Domain.PDCEmulator = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject4, "dnsHostName");
				}
				getADDomainResponse.Domain.ParentDomain = null;
				if (searchResultEntry3 != null)
				{
					getADDomainResponse.Domain.ParentDomain = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry3, "dnsRoot");
				}
				getADDomainResponse.Domain.NetBIOSName = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "nETBIOSName");
				getADDomainResponse.Domain.AllowedDNSSuffixes = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "msDS-AllowedDNSSuffixes");
				getADDomainResponse.Domain.DomainSID = DirectoryUtilities.GetSingleValuedAttrVal<byte[]>(searchResultEntry, "objectSid");
				getADDomainResponse.Domain.AppliedGroupPolicies = null;
				string singleValuedAttrVal12 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "gpLink");
				if (singleValuedAttrVal12 != null && singleValuedAttrVal12.Length > 0)
				{
					string[] array2 = singleValuedAttrVal12.Split(new char[2] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
					if (array2 != null && array2.Length != 0)
					{
						getADDomainResponse.Domain.AppliedGroupPolicies = new string[array2.Length];
						int num = 0;
						array = array2;
						foreach (string input2 in array)
						{
							Match match2 = AdwsConstants.gpLinkRegEx.Match(input2);
							getADDomainResponse.Domain.AppliedGroupPolicies[num++] = match2.Groups["dn"].Value;
						}
					}
				}
				string singleValuedAttrVal13 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "msDS-LogonTimeSyncInterval");
				if (singleValuedAttrVal13 == null)
				{
					getADDomainResponse.Domain.LastLogonReplicationInterval = null;
				}
				else
				{
					int num2 = 0;
					try
					{
						num2 = int.Parse(singleValuedAttrVal13);
						getADDomainResponse.Domain.LastLogonReplicationInterval = new TimeSpan(num2, 0, 0, 0);
					}
					catch (FormatException)
					{
					}
					catch (OverflowException)
					{
					}
				}
				getADDomainResponse.Domain.Forest = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry4, "dnsRoot");
				getADDomainResponse.Domain.UsersContainer = searchDictionary(dictionary, "B:32:A9D1CA15768811D1ADED00C04FD8D5CD:");
				getADDomainResponse.Domain.ComputersContainer = searchDictionary(dictionary, "B:32:AA312825768811D1ADED00C04FD8D5CD:");
				getADDomainResponse.Domain.DomainControllersContainer = searchDictionary(dictionary, "B:32:A361B2FFFFD211D1AA4B00C04FD7D83A:");
				getADDomainResponse.Domain.ForeignSecurityPrincipalsContainer = searchDictionary(dictionary, "B:32:22B70C67D56E4EFB91E9300FCA3DC1AA:");
				getADDomainResponse.Domain.SystemsContainer = searchDictionary(dictionary, "B:32:AB1D30F3768811D1ADED00C04FD8D5CD:");
			}
			catch (DirectoryOperationException ex5)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: got DirectoryOperationException: {0}", ex5.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(ex5);
			}
			catch (LdapException ex6)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: got LdapException: {0}", ex6.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(ex6);
			}
			catch (ArgumentException ex7)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADDomain: got ArgumentException: {0}", ex7.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADDomainFault>(ex7);
			}
			finally
			{
				if (customActionConnCacheEntry != null)
				{
					CustomActionCaches.ReleaseLdapConnection(customActionConnCacheEntry);
					customActionConnCacheEntry = null;
					ldapConnection = null;
				}
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: returning");
			return getADDomainResponse;
		}
		catch (Exception ex8)
		{
			if (!(ex8 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADDomain: fatal exception {0}", ex8.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetADForestResponse GetADForest(GetADForestRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: entered");
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		EnterMethod();
		PerfCounters.GetForestOpsRate.Increment();
		try
		{
			TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADForest: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: instance {0} isn't NTDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(new ArgumentException(AdwsStrings.NonNTDSInstanceInTheHeader, "Server"));
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: got request for {0} ({1})", instanceType.InstanceName, request.Server);
			GetADForestResponse getADForestResponse = new GetADForestResponse();
			getADForestResponse.Forest = new ActiveDirectoryForest();
			ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> customActionConnCacheEntry = null;
			LdapConnection ldapConnection = null;
			try
			{
				VerifyImpersonationLevel();
				customActionConnCacheEntry = CustomActionCaches.GetLdapConnection(instanceType);
				ldapConnection = customActionConnCacheEntry.ConnectionObject;
				string defaultNamingContext;
				string configurationNamingContext;
				string rootDomainNamingContext;
				try
				{
					GetNamingContexts(ldapConnection, instanceType, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), out defaultNamingContext, out configurationNamingContext, out rootDomainNamingContext);
				}
				catch (InvalidOperationException e)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve rootDSE");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(e);
				}
				if (defaultNamingContext == null || defaultNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve defaultNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveDefaultNamingContext);
				}
				if (configurationNamingContext == null || configurationNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve configurationNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveConfigurationNamingContext);
				}
				if (rootDomainNamingContext == null || rootDomainNamingContext.Length == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve rootDomainNamingContext");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveRootDomainNamingContext);
				}
				string schemaHolder;
				string domainHolder;
				try
				{
					GetADDSRoleHolders(instanceType, out schemaHolder, out domainHolder, out var _, out var _, out var _);
				}
				catch (Win32Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve AD DS roles: {0}", ex.NativeErrorCode);
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveAddsFsmoRoles);
				}
				string[] attrsToRetrieve = new string[3] { "uPNSuffixes", "msDS-SPNSuffixes", "msDS-Behavior-Version" };
				string dn = "CN=Partitions," + configurationNamingContext;
				SearchResultEntry searchResultEntry = GetObject(ldapConnection, instanceType, dn, "(objectClass=*)", attrsToRetrieve, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
				if (searchResultEntry == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve config partition information");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveConfigurationPartition);
				}
				List<SearchResultEntry> list = SearchObjects(ldapConnection, instanceType, dn, "(|(!(systemFlags=*))(systemFlags=0))", new string[1] { "distinguishedName" }, System.DirectoryServices.Protocols.SearchScope.OneLevel, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				string[] attrsToRetrieve2 = new string[3] { "distinguishedName", "dnsRoot", "nCName" };
				List<SearchResultEntry> list2 = SearchObjects(ldapConnection, instanceType, "CN=Partitions," + configurationNamingContext, "(systemflags:1.2.840.113556.1.4.803:=3)", attrsToRetrieve2, System.DirectoryServices.Protocols.SearchScope.OneLevel, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve domain NC crossref objects");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveDomainCrossRefs);
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: found {0} domain crossrefs", list2.Count);
				getADForestResponse.Forest.Domains = ProcessEntries(list2, "dnsRoot");
				SearchResultEntry searchResultEntry2 = null;
				foreach (SearchResultEntry item in list2)
				{
					string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(item, "ncName");
					if (singleValuedAttrVal != null && singleValuedAttrVal == rootDomainNamingContext)
					{
						searchResultEntry2 = item;
						break;
					}
				}
				if (searchResultEntry2 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: couldn't retrieve rootdomain crossref object");
					throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(AdwsStrings.CouldntRetrieveRootDomainCrossRef);
				}
				getADForestResponse.Forest.Name = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry2, "dnsRoot");
				getADForestResponse.Forest.RootDomain = getADForestResponse.Forest.Name;
				string text = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "msDS-Behavior-Version");
				if (text == null)
				{
					text = "0";
				}
				if (!int.TryParse(text, out getADForestResponse.Forest.ForestMode))
				{
					getADForestResponse.Forest.ForestMode = 0;
				}
				getADForestResponse.Forest.UPNSuffixes = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "UPNSuffixes");
				getADForestResponse.Forest.SPNSuffixes = DirectoryUtilities.GetMultiValuedAttrVal<string>(searchResultEntry, "msDS-SPNSuffixes");
				if (list != null)
				{
					getADForestResponse.Forest.CrossForestReferences = new string[list.Count];
					int num = 0;
					foreach (SearchResultEntry item2 in list)
					{
						getADForestResponse.Forest.CrossForestReferences[num++] = DirectoryUtilities.GetSingleValuedAttrVal<string>(item2, "distinguishedName");
					}
				}
				else
				{
					getADForestResponse.Forest.CrossForestReferences = null;
				}
				getADForestResponse.Forest.SchemaMaster = null;
				if (schemaHolder != null)
				{
					SearchResultEntry parentObject = GetParentObject(ldapConnection, instanceType, schemaHolder, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					getADForestResponse.Forest.SchemaMaster = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject, "dnsHostName");
				}
				getADForestResponse.Forest.DomainNamingMaster = null;
				if (domainHolder != null)
				{
					SearchResultEntry parentObject2 = GetParentObject(ldapConnection, instanceType, domainHolder, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
					getADForestResponse.Forest.DomainNamingMaster = DirectoryUtilities.GetSingleValuedAttrVal<string>(parentObject2, "dnsHostName");
				}
				List<SearchResultEntry> list3 = SearchObjects(ldapConnection, instanceType, "CN=Partitions," + configurationNamingContext, "(systemflags:1.2.840.113556.1.4.803:=5)", new string[1] { "ncName" }, System.DirectoryServices.Protocols.SearchScope.OneLevel, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list3 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: no application crossref objects found");
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: found {0} application crossrefs", list3.Count);
				}
				getADForestResponse.Forest.ApplicationPartitions = ProcessEntries(list3, "ncName");
				List<SearchResultEntry> list4 = SearchObjects(ldapConnection, instanceType, "CN=Sites," + configurationNamingContext, "(&(objectClass=ntdsdsa)(Options:1.2.840.113556.1.4.803:=1))", new string[2] { "distinguishedName", "systemFlags" }, System.DirectoryServices.Protocols.SearchScope.Subtree, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list4 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: no gc ntdsdsa objects found");
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: found {0} gc ntdsdsa objects", list4.Count);
				}
				IList<string> list5 = new List<string>();
				if (list4 != null)
				{
					foreach (SearchResultEntry item3 in list4)
					{
						string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(item3, "distinguishedName");
						if (singleValuedAttrVal2 != null)
						{
							string singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<string>(GetParentObject(ldapConnection, instanceType, singleValuedAttrVal2, "(objectClass=*)", new string[1] { "dnsHostName" }, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout)), "dnsHostName");
							if (singleValuedAttrVal3 != null)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: found gc: {0}", singleValuedAttrVal3);
								list5.Add(singleValuedAttrVal3);
							}
						}
					}
				}
				getADForestResponse.Forest.GlobalCatalogs = Utils.ToArray(list5);
				List<SearchResultEntry> list6 = SearchObjects(ldapConnection, instanceType, "CN=Sites," + configurationNamingContext, "(objectClass=site)", new string[1] { "cn" }, System.DirectoryServices.Protocols.SearchScope.OneLevel, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout), doPageSearch: true);
				if (list6 == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: no site objects found");
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetADForest: found {0} sites", list6.Count);
				}
				getADForestResponse.Forest.Sites = ProcessEntries(list6, "cn");
			}
			catch (DirectoryOperationException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: got DirectoryOperationException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(ex2);
			}
			catch (LdapException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: got LdapException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(ex3);
			}
			catch (ArgumentException ex4)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "GetADForest: got ArgumentException: {0}", ex4.ToString());
				throw FaultGenerator.GenerateCustomActionFault<GetADForestFault>(ex4);
			}
			finally
			{
				if (customActionConnCacheEntry != null)
				{
					CustomActionCaches.ReleaseLdapConnection(customActionConnCacheEntry);
					customActionConnCacheEntry = null;
					ldapConnection = null;
				}
			}
			return getADForestResponse;
		}
		catch (Exception ex5)
		{
			if (!(ex5 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetADForest: fatal exception {0}", ex5.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public ChangeOptionalFeatureResponse ChangeOptionalFeature(ChangeOptionalFeatureRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "ChangeOptionalFeature: entered");
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		EnterMethod();
		PerfCounters.ChangeOptionalFeatureOpsRate.Increment();
		try
		{
			TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "ChangeOptionalFeature: request is null");
				throw new ArgumentNullException("request");
			}
			if (request.Server == null || request.Server.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: req.Server is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(new ArgumentException(AdwsStrings.MustSupplyServerNameForCustomActions, "Server"));
			}
			if (request.DistinguishedName == null || request.DistinguishedName.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: req.DistinguishedName is null or empty");
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(new ArgumentException(AdwsStrings.MustSupplyDistinguishedNameForChangeOptionalFeature, "DistinguishedName"));
			}
			if (request.FeatureId.Equals(Guid.Empty))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: req.FeatureId is empty GUID");
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(new ArgumentException(AdwsStrings.MustSupplyFeatureIdForChangeOptionalFeature, "FeatureId"));
			}
			InstanceType instanceType = ClassManager.GlobalInstanceMap.GetInstanceType(request.Server);
			if (instanceType == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: unknown instance {0}", request.Server);
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(new ArgumentException(AdwsStrings.InvalidInstanceInTheHeader, "Server"));
			}
			if (instanceType.DirectoryType != DirectoryType.NTDS && instanceType.DirectoryType != DirectoryType.ADLDS)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: instance {0} isn't NTDS or AD LDS, is {1} instead", request.Server, instanceType.DirectoryType.ToString());
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(new NotSupportedException(AdwsStrings.NonNTDSOrADLDSInstanceInTheHeader));
			}
			bool flag = instanceType.DirectoryType == DirectoryType.ADLDS;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "ChangeOptionalFeature: got request for {0} ({1}), isADLDS={2}", instanceType.InstanceName, request.Server, flag);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "ChangeOptionalFeature: feature = {0}, dn={1}, enable={2}", request.FeatureId.ToString(), request.DistinguishedName, request.Enable);
			ChangeOptionalFeatureResponse result = new ChangeOptionalFeatureResponse();
			ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> customActionConnCacheEntry = null;
			try
			{
				VerifyImpersonationLevel();
				customActionConnCacheEntry = CustomActionCaches.GetLdapConnection(instanceType);
				LdapConnection connectionObject = customActionConnCacheEntry.ConnectionObject;
				bool enable = request.Enable;
				string distinguishedName = request.DistinguishedName;
				Guid featureId = request.FeatureId;
				string value = distinguishedName + ":" + featureId.ToString();
				DirectoryAttributeModification directoryAttributeModification = new DirectoryAttributeModification();
				directoryAttributeModification.Name = (enable ? "enableOptionalFeature" : "disableOptionalFeature");
				directoryAttributeModification.Operation = System.DirectoryServices.Protocols.DirectoryAttributeOperation.Add;
				directoryAttributeModification.Add(value);
				DirectoryUtilities.CheckForReferralsAndThrow((ModifyResponse)connectionObject.SendRequest(new System.DirectoryServices.Protocols.ModifyRequest
				{
					DistinguishedName = "",
					Modifications = { directoryAttributeModification }
				}, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout)), "ChangeOptionalFeature", "", instanceType);
			}
			catch (DirectoryOperationException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: got DirectoryOperationException: {0}", ex.ToString());
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(ex);
			}
			catch (LdapException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: got LdapException: {0}", ex2.ToString());
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(ex2);
			}
			catch (ArgumentException ex3)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ActiveDirectoryWebServices", "ChangeOptionalFeature: generating fault, got ArgumentException: {0}", ex3.ToString());
				throw FaultGenerator.GenerateCustomActionFault<ChangeOptionalFeatureFault>(ex3);
			}
			finally
			{
				if (customActionConnCacheEntry != null)
				{
					CustomActionCaches.ReleaseLdapConnection(customActionConnCacheEntry);
					customActionConnCacheEntry = null;
				}
			}
			return result;
		}
		catch (Exception ex4)
		{
			if (!(ex4 is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "ChangeOptionalFeature: fatal exception {0}", ex4.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	[OperationBehavior(Impersonation = ImpersonationOption.Required)]
	public GetVersionResponse GetVersion(GetVersionRequest request)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetVersion: entered");
		CheckDisposed();
		EnterMethod();
		PerfCounters.GetVersionOpsRate.Increment();
		try
		{
			if (request == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetVersion: request is null");
				throw new ArgumentNullException("request");
			}
			return new GetVersionResponse
			{
				VersionMajor = AdwsConstants.VersionMajor,
				VersionMinor = AdwsConstants.VersionMinor,
				VersionString = AdwsConstants.VersionString
			};
		}
		catch (Exception ex)
		{
			if (!(ex is FaultException))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetVersion: fatal exception {0}", ex.ToString());
			}
			throw;
		}
		finally
		{
			ExitMethod();
		}
	}

	private void GetNamingContexts(LdapConnection ldapConn, InstanceType instance, TimeSpan timeAllowed, out string defaultNamingContext, out string configurationNamingContext, out string rootDomainNamingContext)
	{
		defaultNamingContext = null;
		configurationNamingContext = null;
		string[] attrsToRetrieve = new string[3] { "defaultNamingContext", "configurationNamingContext", "rootDomainNamingContext" };
		SearchResultEntry searchResultEntry = GetObject(ldapConn, instance, "", "(objectClass=*)", attrsToRetrieve, timeAllowed);
		if (searchResultEntry == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ActiveDirectoryWebServices", "GetNamingContexts: couldn't retrieve rootDSE");
			throw new InvalidOperationException(AdwsStrings.CouldntRetrieveRootDSE);
		}
		defaultNamingContext = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "defaultNamingContext");
		configurationNamingContext = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "configurationNamingContext");
		rootDomainNamingContext = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResultEntry, "rootDomainNamingContext");
	}

	private SearchResultEntry GetObjectByClass(LdapConnection ldapConn, InstanceType instance, string dn, string expectedObjectClass, string[] attrsToRetrieve, TimeSpan timeAllowed)
	{
		return GetObject(ldapConn, instance, dn, "(objectClass=" + expectedObjectClass + ")", attrsToRetrieve, timeAllowed);
	}

	private SearchResultEntry GetParentObject(LdapConnection ldapConn, InstanceType instance, string dn, string filter, string[] attrsToRetrieve, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetParentObject: entered dn: {0}", (dn == null) ? "-" : dn);
		if (dn == null || dn.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetParentObject: nothing to do. Returning null.");
			return null;
		}
		DirectoryUtilities.GetRdnFromDn(dn, out var trimmedDn);
		if (trimmedDn != null && trimmedDn.Length >= 4)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetParentObject: parentDn: {0}", trimmedDn);
			return SearchObject(ldapConn, instance, trimmedDn, filter, attrsToRetrieve, System.DirectoryServices.Protocols.SearchScope.Base, timeAllowed);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetParentObject: returning null");
		return null;
	}

	private bool CheckObjectExistence(LdapConnection ldapConn, InstanceType instance, string dn, string filter, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "CheckObjectExistence: entered");
		string[] attrsToRetrieve = new string[1] { "1.1" };
		return GetObject(ldapConn, instance, dn, filter, attrsToRetrieve, timeAllowed) != null;
	}

	private SearchResultEntry GetObject(LdapConnection ldapConn, InstanceType instance, string dn, string filter, string[] attrsToRetrieve, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "GetObject: entered");
		return SearchObject(ldapConn, instance, dn, filter, attrsToRetrieve, System.DirectoryServices.Protocols.SearchScope.Base, timeAllowed);
	}

	private SearchResultEntry SearchObject(LdapConnection ldapConn, InstanceType instance, string dn, string filter, string[] attrsToRetrieve, System.DirectoryServices.Protocols.SearchScope scope, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SearchObject: entered");
		return SearchObjects(ldapConn, instance, dn, filter, attrsToRetrieve, scope, timeAllowed, doPageSearch: false)?[0];
	}

	private List<SearchResultEntry> SearchObjects(LdapConnection ldapConn, InstanceType instance, string dn, string filter, string[] attrsToRetrieve, System.DirectoryServices.Protocols.SearchScope scope, TimeSpan timeAllowed, bool doPageSearch)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SearchObjects: entered");
		DateTime utcNow = DateTime.UtcNow;
		SearchRequest searchRequest = new SearchRequest(dn, filter, scope, attrsToRetrieve);
		List<SearchResultEntry> list = new List<SearchResultEntry>();
		if (doPageSearch)
		{
			PageResultResponseControl pageResultResponseControl = null;
			PageResultRequestControl pageResultRequestControl = new PageResultRequestControl();
			pageResultRequestControl.PageSize = 1000;
			pageResultRequestControl.IsCritical = true;
			searchRequest.Controls.Add(pageResultRequestControl);
			do
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SearchObjects: doing paged search using size of {0}", pageResultRequestControl.PageSize);
				if (pageResultResponseControl != null && pageResultResponseControl.Cookie != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SearchObjects: using paged cookie from previous response");
					pageResultRequestControl.Cookie = pageResultResponseControl.Cookie;
				}
				SearchResponse searchResponse = (SearchResponse)ldapConn.SendRequest(searchRequest, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, timeAllowed));
				DirectoryUtilities.CheckForReferralsAndThrow(searchResponse, "SearchObjects", dn, instance);
				if (searchResponse == null || searchResponse.Entries == null)
				{
					continue;
				}
				foreach (SearchResultEntry entry in searchResponse.Entries)
				{
					list.Add(entry);
				}
				pageResultResponseControl = DirectoryUtilities.FindSpecifiedControl<PageResultResponseControl>(searchResponse.Controls, "1.2.840.113556.1.4.319");
				if (pageResultResponseControl != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ActiveDirectoryWebServices", "SearchObjects: paged cookie for next search (more results={0})", (pageResultResponseControl.Cookie.Length != 0) ? "true" : "false");
				}
			}
			while (pageResultResponseControl != null && pageResultResponseControl.Cookie != null && pageResultResponseControl.Cookie.Length != 0);
		}
		else
		{
			SearchResponse searchResponse2 = (SearchResponse)ldapConn.SendRequest(searchRequest, timeAllowed);
			DirectoryUtilities.CheckForReferralsAndThrow(searchResponse2, "SearchObjects", dn, instance);
			if (searchResponse2 != null && searchResponse2.Entries != null)
			{
				foreach (SearchResultEntry entry2 in searchResponse2.Entries)
				{
					list.Add(entry2);
				}
			}
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list;
	}

	private string searchDictionary(Dictionary<string, string> wellKnownObjectsDictionary, string wellKnownBinary)
	{
		string text = null;
		try
		{
			return wellKnownObjectsDictionary[wellKnownBinary];
		}
		catch (KeyNotFoundException)
		{
			return null;
		}
	}

	internal static string[] ProcessEntries(List<SearchResultEntry> searchEntries, string attrName)
	{
		if (attrName == null || searchEntries == null)
		{
			return null;
		}
		IList<string> list = new List<string>();
		foreach (SearchResultEntry searchEntry in searchEntries)
		{
			string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchEntry, attrName);
			if (singleValuedAttrVal != null)
			{
				list.Add(singleValuedAttrVal);
			}
		}
		return Utils.ToArray(list);
	}
}
