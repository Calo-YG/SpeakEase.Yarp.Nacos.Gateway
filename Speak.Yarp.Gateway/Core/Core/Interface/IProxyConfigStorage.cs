using Nacos.V2;
using Yarp.ReverseProxy.Configuration;

namespace Speak.Yarp.Gateway.Core.Core.Interface;

public interface IProxyConfigStorage
{
      /// <summary>
      /// 群组
      /// </summary>
      public string GroupName { get; }
      
      /// <summary>
      /// 服务
      /// </summary>
      public string ServiceName { get; }
      /// <summary>
      /// 路由配置
      /// </summary>
      public RouteConfig RouteConfig { get;  }
      
      /// <summary>
      /// 集群配置
      /// </summary>
      public ClusterConfig ClusterConfig { get;  }
      
      /// <summary>
      /// 服务事件监听
      /// </summary>
      public IEventListener? DefaultServiceChangeListner { get; }

       void SetRouteConfig(RouteConfig routeConfig);

       void SetClusterConfig(ClusterConfig clusterConfig);

       void SetListener(IEventListener eventListener);
}