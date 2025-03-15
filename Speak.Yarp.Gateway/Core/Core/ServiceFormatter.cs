using Speak.Yarp.Gateway.Core.Core.Interface;

namespace Speak.Yarp.Gateway.Core.Core;

public class ServiceFormatter:IServiceFormatter
{ 
      public Func<string, string> ServiceNameFormatter { get; }

      public ServiceFormatter(Func<string, string> serviceNameFormatter)
      {
            ServiceNameFormatter = serviceNameFormatter;
      }
}