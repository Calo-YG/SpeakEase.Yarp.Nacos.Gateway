using Microsoft.Extensions.Options;
using Nacos.V2.Naming.Dtos;
using Speak.Yarp.Gateway.Core.Core.Interface;

namespace Speak.Yarp.Gateway.Core.Core;

public class NacosService(ILoggerFactory loggerFactory, DefaultHttpClientProxy defaultHttpClientProxy, IOptions<DefaultNacosOptions> defaultNacosOptions)
      : INacosService
{
      private readonly ILogger _logger = loggerFactory.CreateLogger<NacosService>();

      public List<Instance>? GetAllInstance()
      {
            if (defaultNacosOptions?.Value.DefaultGroup == null || !defaultNacosOptions.Value.DefaultGroup.Any())
            {
                  return null;
            }

            List<Instance> instances = new List<Instance>(1);

            foreach (var item in defaultNacosOptions.Value.DefaultGroup)
            {
                  try
                  {
                        int pageIndex = 1;

                        var listView = defaultHttpClientProxy.GetServiceList(1, defaultNacosOptions.Value.Count, item, null).ConfigureAwait(false).GetAwaiter()
                              .GetResult();

                        if (listView.Count == 0)
                        {
                              //groupServicesDict.Add(groupName, new List<string>());
                              continue;
                        }

                        var groupServices = listView.Data;

                        // 如果总数大于当前数量则继续翻页取出所有实例
                        if (listView.Count > defaultNacosOptions.Value.Count)
                        {
                              do
                              {
                                    pageIndex++;
                                    var tmp = defaultHttpClientProxy.GetServiceList(pageIndex, defaultNacosOptions.Value.Count, item, null)
                                          .ConfigureAwait(false).GetAwaiter().GetResult();
                                    groupServices.AddRange(tmp.Data);
                              } while (listView.Count > defaultNacosOptions.Value.Count * pageIndex);
                        }

                        foreach (var service in groupServices)
                        {
                              var _instances = defaultHttpClientProxy.QueryInstancesOfService(service, item, "", 0, true).ConfigureAwait(false).GetAwaiter()
                                    .GetResult();

                              if (_instances?.Hosts == null || !(_instances?.Hosts?.Any() ?? false))
                              {
                                    continue;
                              }

                              instances.AddRange(_instances.Hosts);
                        }
                  }
                  catch (Exception ex)
                  {
                        _logger?.LogError($"load service from nacos service group：{item}) failed", ex);
                  }
            }

            return instances;
      }

      public async Task<List<Instance>?> GetAllInstancesAsync(CancellationToken cancellationToken)
      {
            if (defaultNacosOptions?.Value.DefaultGroup == null || !defaultNacosOptions.Value.DefaultGroup.Any())
            {
                  return null;
            }

            List<Instance> instances = new List<Instance>(1);

            foreach (var item in defaultNacosOptions.Value.DefaultGroup)
            {
                  try
                  {
                        int pageIndex = 1;

                        var listView = await defaultHttpClientProxy.GetServiceList(1, defaultNacosOptions.Value.Count, item, null).ConfigureAwait(false);

                        if (listView.Count == 0)
                        {
                              //groupServicesDict.Add(groupName, new List<string>());
                              continue;
                        }

                        var groupServices = listView.Data;

                        // 如果总数大于当前数量则继续翻页取出所有实例
                        if (listView.Count > defaultNacosOptions.Value.Count)
                        {
                              do
                              {
                                    pageIndex++;
                                    var tmp = await defaultHttpClientProxy.GetServiceList(pageIndex, defaultNacosOptions.Value.Count, item, null)
                                          .ConfigureAwait(false);
                                    groupServices.AddRange(tmp.Data);
                              } while (listView.Count > defaultNacosOptions.Value.Count * pageIndex);
                        }

                        foreach (var service in groupServices)
                        {
                              var _instances = defaultHttpClientProxy.QueryInstancesOfService(service, item, "", 0, true).ConfigureAwait(false).GetAwaiter()
                                    .GetResult();

                              if (_instances?.Hosts == null || !(_instances?.Hosts?.Any() ?? false))
                              {
                                    continue;
                              }

                              instances.AddRange(_instances.Hosts);
                        }
                  }
                  catch (Exception ex)
                  {
                        _logger?.LogError($"load service from nacos service group：{item}) failed", ex);
                  }
            }

            return instances;
      }
}