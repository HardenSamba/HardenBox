using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdentityManagementOperationAttribute : Attribute, IOperationBehavior
{
	public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters)
	{
	}

	public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation)
	{
	}

	public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
	{
		if (operationDescription == null)
		{
			throw new ArgumentNullException("operationDescription");
		}
		if (dispatchOperation == null)
		{
			throw new ArgumentNullException("dispatchOperation");
		}
		if ((dispatchOperation.Parent != null) & (dispatchOperation.Parent.MessageInspectors != null))
		{
			dispatchOperation.Parent.MessageInspectors.Add(new IdentityManagementMessageInspector());
		}
	}

	public void Validate(OperationDescription operationDescription)
	{
	}
}
