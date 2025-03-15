using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Speak.Yarp.Gateway.Core.Core.Interface;

public interface IDefaultProxyConfigStorage
{
      Task RemoveByKey(string key);

      void Update(string key, ClusterConfig clusterConfig);

      Task Clear();

      IChangeToken GetReloadToken();

      void Reload();

      IProxyConfig GetProxyConfig();

      Task RefreshProxyConfig(CancellationToken cancellationToken);
}