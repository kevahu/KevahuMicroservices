namespace Kevahu.Microservices.Core.RemoteProcedureCall.Attributes
{
    /// <summary>
    /// Marks a class as an implementation of a Remote Procedure Call (RPC) interface. This
    /// attribute is used by the system to discover and register RPC implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcImplementationAttribute : Attribute;
}