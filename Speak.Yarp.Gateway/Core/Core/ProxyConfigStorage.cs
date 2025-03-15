using Nacos.V2;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Yarp.ReverseProxy.Configuration;

namespace Speak.Yarp.Gateway.Core.Core;

public class ProxyConfigStorage:IProxyConfigStorage
{
      /// <summary>
      /// 服务名称
      /// </summary>
      public string ServiceName { get; internal set; }
      
      /// <summary>
      /// 群组名称
      /// </summary>
      public string GroupName { get;internal set; }
      
      /// <summary>
      /// 路由配置
      /// </summary>
      public RouteConfig RouteConfig { get; internal set; }
      
      /// <summary>
      /// 集群配置
      /// </summary>
      public ClusterConfig ClusterConfig { get; internal set; }
      
      /// <summary>
      /// 服务事件监听
      /// </summary>
      public IEventListener? DefaultServiceChangeListner { get; internal set; }

      public ProxyConfigStorage(string serviceName,string groupName,RouteConfig routeConfig, ClusterConfig clusterConfig,IEventListener eventListener)
      {
            ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
            RouteConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
            ClusterConfig = clusterConfig ?? throw new ArgumentNullException(nameof(clusterConfig));
            DefaultServiceChangeListner = eventListener ?? throw new ArgumentNullException(nameof(eventListener));
      }

      public void SetRouteConfig(RouteConfig routeConfig) => RouteConfig = routeConfig;

      public void SetClusterConfig(ClusterConfig clusterConfig) => ClusterConfig = clusterConfig;

      public void SetListener(IEventListener eventListener)
      {
            if (DefaultServiceChangeListner == null)
            {
                  DefaultServiceChangeListner = eventListener;
            }
      }
}