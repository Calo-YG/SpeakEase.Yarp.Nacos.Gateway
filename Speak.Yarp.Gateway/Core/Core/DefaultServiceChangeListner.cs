using Nacos.V2;
using Nacos.V2.Naming.Event;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Yarp.ReverseProxy.Configuration;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultServiceChangeListner:IEventListener
{
      private readonly ILogger _logger;

      private readonly DefaultHttpClientProxy _defaultHttpClientProxy;

      private readonly IDefaultProxyConfigStorage _defaultProxyConfigStorage;

      private readonly ServicesOption _option;
      public DefaultServiceChangeListner(ILogger logger,DefaultHttpClientProxy defaultHttpClientProxy,IDefaultProxyConfigStorage defaultProxyConfigStorage,ServicesOption servicesOption)
      {
            _logger = logger;
            _defaultHttpClientProxy = defaultHttpClientProxy;
            _defaultProxyConfigStorage = defaultProxyConfigStorage;
            _option = servicesOption;
      }
      
      public async Task OnEvent(IEvent @event)
      {
            if (@event is InstancesChangeEvent e)
            {
                  var service = e.ServiceName;
                  
                  var groupname = e.GroupName;

                  var serviceInfo =await _defaultHttpClientProxy.QueryInstancesOfService(service, groupname, "", 0, false);

                  var instances = serviceInfo?.Hosts;

                  var key = ProxyConfigExtensions.BuilderUniqueId(e.ServiceName, e.GroupName);
                  
                  if (instances == null || !instances.Any())
                  {
                        _logger.LogInformation($"Remove {e.ServiceName} - {e.GroupName}");

                        await _defaultProxyConfigStorage.RemoveByKey(key);
                  }
                  else
                  {
                        _logger.LogInformation($"Update {e.ServiceName}-{e.GroupName}");
                        
                        var clusterconfig = new ClusterConfig()
                        {
                              ClusterId = key,
                              LoadBalancingPolicy = _option.LoadBalancingPolicy,   
                              HealthCheck = new HealthCheckConfig
                              {
                                    Active = new ActiveHealthCheckConfig
                                    {
                                          Enabled = true,
                                          Interval = TimeSpan.FromSeconds(_option.HealthyOption.Interval),
                                          Timeout = TimeSpan.FromSeconds(_option.HealthyOption.TimeOut)
                                    }
                              },
                              Destinations = ProxyConfigExtensions.CreatDestination(instances)
                        };
                        
                        _defaultProxyConfigStorage.Update(key,clusterconfig);
                  }
                  
                  _defaultProxyConfigStorage.Reload();
            }
      }
}