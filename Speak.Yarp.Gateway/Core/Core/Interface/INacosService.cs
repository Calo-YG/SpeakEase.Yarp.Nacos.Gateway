using Nacos.V2.Naming.Dtos;

namespace Speak.Yarp.Gateway.Core.Core.Interface;

public interface INacosService
{
      List<Instance>? GetAllInstance();

      Task<List<Instance>?> GetAllInstancesAsync(CancellationToken cancellationToken);
}