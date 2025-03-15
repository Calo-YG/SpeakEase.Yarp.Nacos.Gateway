using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nacos.V2;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Yarp.ReverseProxy.Configuration;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultProxyConfigStorage : IDefaultProxyConfigStorage
{
      private static object _lockObject = new();
      
      private ConcurrentDictionary<string, IProxyConfigStorage> _defaultStorages;

      private readonly ILogger _logger;

      private readonly INacosService _nacosService;

      private readonly INacosNamingService _nacosNamingService;

      private readonly IServiceProvider _serviceProvider;

      private readonly DefaultHttpClientProxy _defaultHttpClientProxy;

      private DefaultProxyConfigReloadToken _reloadToken = new();

      private readonly IServiceFormatter _serviceFormatter;

      public DefaultProxyConfigStorage(ILoggerFactory loggerFactory, INacosService nacosService, INacosNamingService namingService,
            IServiceProvider serviceProvider, DefaultHttpClientProxy defaultHttpClientProxy, IServiceFormatter serviceFormatter)
      {
            _logger = loggerFactory.CreateLogger("DefaultProxyConfigStorage");
            _nacosService = nacosService;
            _nacosNamingService = namingService;
            _defaultStorages = new ConcurrentDictionary<string, IProxyConfigStorage>();
            _serviceProvider = serviceProvider;
            _defaultHttpClientProxy = defaultHttpClientProxy;
            _serviceFormatter = serviceFormatter;
      }

      private IProxyConfig GetProxyConfigFromMemory() => new DefaultProxyConfig
      {
            Routes = _defaultStorages.Select(p => p.Value.RouteConfig).ToList(),
            Clusters = _defaultStorages.Select(p => p.Value.ClusterConfig).ToList()
      };

      public async Task Clear()
      {
            foreach (var item in _defaultStorages)
            {
                  await _nacosNamingService.Unsubscribe(item.Value.ServiceName, item.Value.GroupName, item.Value.DefaultServiceChangeListner);
            }

            _defaultStorages.Clear();
      }

      /// <summary>
      /// 获取重载令牌
      /// get reloadToken
      /// </summary>
      /// <returns></returns>
      public IChangeToken GetReloadToken() => _reloadToken;

      /// <summary>
      /// 重新载入配置
      /// reload configuration
      /// </summary>
      public void Reload() => Interlocked.Exchange(ref _reloadToken, new DefaultProxyConfigReloadToken()).OnReload();

      public IProxyConfig GetProxyConfig()
      {
            lock (_lockObject)
            {
                  IProxyConfig config;

                  if (_defaultStorages.Any())
                  {
                        config = GetProxyConfigFromMemory();
                  }
                  else
                  {
                        config = GetConfig().ConfigureAwait(false).GetAwaiter().GetResult();
                  }

                  return config;
            }
      }

      private async Task<IProxyConfig> GetConfig()
      {
            var instances =await _nacosService.GetAllInstancesAsync(CancellationToken.None);

            if (instances == null || !instances.Any())
            {
                  return new DefaultProxyConfig();
            }

            List<RouteConfig> routeConfigs = new List<RouteConfig>(1);
            List<ClusterConfig> clusterConfigs = new List<ClusterConfig>(1);

            var serverdic = instances.GroupBy(p => p.ServiceName).ToDictionary(p => p.Key, p => p.ToList());


            using var scope = _serviceProvider.CreateScope();

            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<GatewayServiceOption>>()?.Value;

            if (options == null)
            {
                  _logger.LogError("Please Setting Your GatewayOption");

                  throw new ArgumentNullException(nameof(options), "Please Setting Your GatewayOption");
            }

            foreach (var item in serverdic)
            {
                  var array = item.Key.Split("@@");
                  var servicename = array[1];
                  var groupname = array[0];
                  
                  var key = ProxyConfigExtensions.BuilderUniqueId(servicename, groupname);
                  
                  var tryGetValue = _defaultStorages.TryGetValue(key, out var proxyConfigStorage);

                  var currentoptions = options.ServicesOptions?.Find(p => p.ServiceName == servicename) ?? new ServicesOption
                  {
                       ServiceName = servicename,
                       GroupName = groupname
                  };
                  
                  var temp = _serviceFormatter.ServiceNameFormatter.Invoke(servicename);
                  
                  var routeconfig = new RouteConfig
                  {
                        ClusterId = key,
                        RouteId = $"{servicename}{groupname}",
                        Match = new RouteMatch()
                        {
                              Path = $"{options.Prefix}/{temp}/{{**catch-all}}"
                        },
                  };

                  var clusterconfig = new ClusterConfig()
                  {
                        ClusterId = key,
                        LoadBalancingPolicy = currentoptions.LoadBalancingPolicy,
                        HealthCheck = new HealthCheckConfig
                        {
                              Active = new ActiveHealthCheckConfig
                              {
                                    Enabled = true,
                                    Interval = TimeSpan.FromSeconds(currentoptions.HealthyOption.Interval),
                                    Timeout = TimeSpan.FromSeconds(currentoptions.HealthyOption.TimeOut)
                              }
                        },
                        Destinations = ProxyConfigExtensions.CreatDestination(item.Value!)
                  };
                  
                  IEventListener eventListener = new DefaultServiceChangeListner(_logger, _defaultHttpClientProxy, this, currentoptions);
                  
                  if (tryGetValue)
                  {
                        var listener = proxyConfigStorage!.DefaultServiceChangeListner;
                        await _nacosNamingService.Unsubscribe(servicename, groupname, listener);
                        proxyConfigStorage.SetRouteConfig(routeconfig);
                        proxyConfigStorage.SetListener(eventListener);
                        proxyConfigStorage.SetClusterConfig(clusterconfig);
                  }
                  else
                  {
                        proxyConfigStorage = new ProxyConfigStorage(serviceName: servicename, groupName: groupname, routeconfig, clusterconfig, eventListener);
                        _defaultStorages.TryAdd(key, proxyConfigStorage);
                  }

                  routeConfigs.Add(routeconfig);
                  clusterConfigs.Add(clusterconfig);
            }

            return new DefaultProxyConfig
            {
                  Clusters = clusterConfigs,
                  Routes = routeConfigs
            };
      }
      
      public async Task RemoveByKey(string key)
      {
            var success = _defaultStorages.Remove(key, out var proxyConfigStorage);

            if (success)
            {
                  await _nacosNamingService.Unsubscribe(proxyConfigStorage!.ServiceName, proxyConfigStorage.GroupName,
                        proxyConfigStorage.DefaultServiceChangeListner);

                  _logger.LogInformation($"Remove DefaultStoarage Success {key}");
            }
            else
            {
                  _logger.LogWarning($"Remove DefaultStoarage Faild {key}");
            }
      }

      public void Update(string key, ClusterConfig clusterConfig)
      {
            var trygetvalue = _defaultStorages.TryGetValue(key, out var proxyConfigStorage);

            if (!trygetvalue)
            {
                  _logger.LogWarning($"DefaultStoarage Get Config Faild {key}");
            }
            else
            {
                  proxyConfigStorage!.SetClusterConfig(clusterConfig);
            }
      }

      /// <summary>
      /// 刷新路由集群配置
      /// </summary>
      /// <param name="cancellationToken"></param>
      public async Task RefreshProxyConfig(CancellationToken cancellationToken)
      {
             await GetConfig();
            
            Reload();
      }
}