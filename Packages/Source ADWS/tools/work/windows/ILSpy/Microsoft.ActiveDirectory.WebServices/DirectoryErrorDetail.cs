using System;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
[XmlRoot(ElementName = "DirectoryError", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", IsNullable = true)]
public class DirectoryErrorDetail
{
	private string message;

	private string shortMessage;

	private string errorCode;

	private string extendedErrorMessage;

	private string matchedDN;

	private string[] referral;

	private string win32ErrorCode;

	[XmlElement(ElementName = "Message")]
	public string Message
	{
		get
		{
			return message;
		}
		set
		{
			message = value;
		}
	}

	[XmlElement(ElementName = "ErrorCode")]
	public string ErrorCode
	{
		get
		{
			return errorCode;
		}
		set
		{
			errorCode = value;
		}
	}

	[XmlElement(ElementName = "ExtendedErrorMessage")]
	public string ExtendedErrorMessage
	{
		get
		{
			return extendedErrorMessage;
		}
		set
		{
			extendedErrorMessage = value;
		}
	}

	[XmlElement(ElementName = "MatchedDN")]
	public string MatchedDN
	{
		get
		{
			return matchedDN;
		}
		set
		{
			matchedDN = value;
		}
	}

	[XmlElement(ElementName = "Referral")]
	public string[] Referral
	{
		get
		{
			return referral;
		}
		set
		{
			referral = value;
		}
	}

	[XmlElement(ElementName = "Win32ErrorCode")]
	public string Win32ErrorCode
	{
		get
		{
			return win32ErrorCode;
		}
		set
		{
			win32ErrorCode = value;
		}
	}

	[XmlElement(ElementName = "ShortMessage")]
	[DataMember(Order = 2)]
	public string ShortMessage
	{
		get
		{
			return shortMessage;
		}
		set
		{
			shortMessage = value;
		}
	}

	public DirectoryErrorDetail()
	{
		message = null;
		shortMessage = null;
		errorCode = null;
		extendedErrorMessage = null;
		matchedDN = null;
		referral = null;
		win32ErrorCode = null;
	}

	public DirectoryErrorDetail(DirectoryOperationException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.DirectoryOperationException;
		}
		if (e.Response == null)
		{
			return;
		}
		errorCode = ((int)e.Response.ResultCode).ToString();
		if (AdwsConstants.LdapErrorToWin32Error.TryGetValue((int)e.Response.ResultCode, out var value2))
		{
			win32ErrorCode = value2.ToString();
		}
		else
		{
			win32ErrorCode = 8341.ToString();
		}
		extendedErrorMessage = e.Response.ErrorMessage;
		matchedDN = e.Response.MatchedDN;
		if (e.Response.Referral == null)
		{
			return;
		}
		referral = new string[e.Response.Referral.Length];
		for (int i = 0; i < e.Response.Referral.Length; i++)
		{
			if (e.Response.Referral[i] != null)
			{
				referral[i] = e.Response.Referral[i].ToString();
			}
		}
	}

	public DirectoryErrorDetail(LdapException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		if (AdwsConstants.LdapErrorToWin32Error.TryGetValue(e.ErrorCode, out var value))
		{
			win32ErrorCode = value.ToString();
		}
		else
		{
			win32ErrorCode = 8341.ToString();
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value2))
		{
			shortMessage = value2;
		}
		else
		{
			shortMessage = WebServiceConstants.SDSP_LdapException;
		}
		errorCode = e.ErrorCode.ToString();
		extendedErrorMessage = e.ServerErrorMessage;
	}

	public DirectoryErrorDetail(ActiveDirectoryOperationException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SDSAD_ActiveDirectoryOperationException;
		}
		win32ErrorCode = (errorCode = e.ErrorCode.ToString());
	}

	public DirectoryErrorDetail(ActiveDirectoryObjectNotFoundException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SDSAD_ActiveDirectoryObjectNotFoundException;
		}
		extendedErrorMessage = e.Name;
	}

	public DirectoryErrorDetail(ActiveDirectoryServerDownException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SDSAD_ActiveDirectoryServerDownException;
		}
		win32ErrorCode = (errorCode = e.ErrorCode.ToString());
		extendedErrorMessage = e.Name;
	}

	public DirectoryErrorDetail(PrincipalOperationException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SDSAM_PrincipalOperationException;
		}
		win32ErrorCode = (errorCode = e.ErrorCode.ToString());
	}

	public DirectoryErrorDetail(DirectoryServicesCOMException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SDS_DirectoryServicesCOMException;
		}
		win32ErrorCode = (errorCode = e.ExtendedError.ToString());
		extendedErrorMessage = e.ExtendedErrorMessage;
	}

	public DirectoryErrorDetail(Win32Exception e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.SCM_Win32Exception;
		}
		win32ErrorCode = (errorCode = e.NativeErrorCode.ToString());
	}
}
