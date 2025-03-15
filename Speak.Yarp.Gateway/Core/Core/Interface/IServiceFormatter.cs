namespace Speak.Yarp.Gateway.Core.Core.Interface;

public interface IServiceFormatter
{
      Func<string, string> ServiceNameFormatter { get; }
}