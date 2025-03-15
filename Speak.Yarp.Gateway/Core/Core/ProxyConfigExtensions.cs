using System.Collections.ObjectModel;
using Nacos.V2.Naming.Dtos;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace Speak.Yarp.Gateway.Core.Core;

public static class ProxyConfigExtensions
{
      private static readonly string HTTP = "http://";
      private static readonly string HTTPS = "https://";
      private static readonly string Secure = "secure";
      private static readonly string MetadataPrefix = "yarp";
      
      public static string BuilderUniqueId(string servicename, string groupname) => $"{servicename}-{groupname}";

      public static  Dictionary<string, DestinationConfig>  CreatDestination(List<Instance> instances)
      {
            var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

            var index = 1;
            
            foreach (var instance in instances.Where(x => x.Healthy && x.Enabled))
            {
                  var address = instance.Metadata.TryGetValue(Secure, out _) ? $"{HTTPS}{instance.Ip}:{instance.Port}" : $"{HTTP}{instance.Ip}:{instance.Port}";

                  // filter the metadata from instance
                  var meta = instance.Metadata.Where(x => x.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)).ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

                  // 被动健康检查处理
                  meta.TryAdd(TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, "0.5");
                  //meta.TryAdd(YarpNacosConstants.InstanceWeight, instance.Weight.ToString());

                  var metadata = new ReadOnlyDictionary<string, string>(meta ?? new Dictionary<string, string>());

                  var destination = new DestinationConfig
                  {
                        Address = address,
                        Metadata = metadata
                  };
                  
                  destinations.Add($"{instance.ClusterName}({instance.ServiceName}-{index})", destination);
            }

            return destinations;
      }

      public static void AddDefualtYapNacosExtensions(this WebApplicationBuilder builder,Func<string,string> func = null,string section = "GatewayServiceOptions")
      {
            builder.Services.Configure<GatewayServiceOption>(builder.Configuration.GetSection("GatewayServiceOptions"));
            builder.Services.AddSingleton<IServiceFormatter, ServiceFormatter>(sp =>
            {
                  return new ServiceFormatter((str) => str.Split('_')[1].ToLower());
            });
            builder.Services.AddSingleton<IProxyConfigProvider, DefaultProxyConfigProvider>();
            builder.Services.AddSingleton<IDefaultProxyConfigStorage, DefaultProxyConfigStorage>();
      }
}