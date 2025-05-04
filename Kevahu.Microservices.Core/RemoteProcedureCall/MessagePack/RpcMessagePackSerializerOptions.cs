using MessagePack;

namespace Kevahu.Microservices.Core.RemoteProcedureCall.MessagePack
{
    public class RpcMessagePackSerializerOptions : MessagePackSerializerOptions
    {
        #region Public Constructors

        public RpcMessagePackSerializerOptions(IFormatterResolver resolver) : base(resolver)
        {
        }

        public RpcMessagePackSerializerOptions(MessagePackSerializerOptions copyFrom) : base(copyFrom)
        {
        }

        #endregion Public Constructors

        #region Public Methods

        public override Type? LoadType(string typeName)
        {
            Type? result = Type.GetType(typeName, false);
            string assemblyName = typeName.Substring(typeName.IndexOf(',') + 2);
            typeName = typeName.Substring(0, typeName.IndexOf(','));
            if (result == null)
            {
                if (this.AllowAssemblyVersionMismatch)
                {
                    assemblyName = assemblyName.Substring(0, assemblyName.IndexOf(','));
                }
                result = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith(assemblyName)).GetType(typeName);
            }

            return result;
        }

        #endregion Public Methods
    }
}