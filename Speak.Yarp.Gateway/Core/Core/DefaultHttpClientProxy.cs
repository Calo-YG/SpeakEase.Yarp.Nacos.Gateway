using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nacos.V2;
using Nacos.V2.Common;
using Nacos.V2.Exceptions;
using Nacos.V2.Naming.Cache;
using Nacos.V2.Naming.Core;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using Nacos.V2.Naming.Utils;
using Nacos.V2.Remote;
using Nacos.V2.Security;
using Nacos.V2.Utils;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultHttpClientProxy
{
      private static readonly int DEFAULT_SERVER_PORT = 8848;

      private static readonly string NAMING_SERVER_PORT = "nacos.naming.exposed.port";

      private ILogger _logger;

      private readonly IHttpClientFactory _clientFactory;

      private string namespaceId;

      private SecurityProxy _securityProxy;

      private DefaultServerListManager _defaultServerListManager;

      private ServiceInfoHolder serviceInfoHolder;

      private PushReceiver pushReceiver;

      private int serverPort = DEFAULT_SERVER_PORT;

      private NacosSdkOptions _options;

      private readonly InstancesChangeNotifier _changeNotifier;

      private readonly ServiceInfoHolder _serviceInfoHolder;

      private readonly IConfiguration _configuration;

      public DefaultHttpClientProxy(ILoggerFactory loggerFactory, IOptions<NacosSdkOptions> options, IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
      {
            this._options = options.Value;
            this._clientFactory = httpClientFactory;
            this._logger = loggerFactory.CreateLogger("DefaultHttpClientProxy");
            this.namespaceId = options.Value.Namespace;
            _configuration = configuration;
            this.SetServerAddress();
            this.SetServerPort(DEFAULT_SERVER_PORT);
            this.SetNameSpeace();
            this._defaultServerListManager = new DefaultServerListManager(_logger, _options, namespaceId, httpClientFactory);
            this._securityProxy = new SecurityProxy(_options, _logger);
            this._changeNotifier = new InstancesChangeNotifier();
            this._serviceInfoHolder = new ServiceInfoHolder(_logger, this.namespaceId, _options, _changeNotifier);
      }

      private void SetServerAddress()
      {
            if (_options?.ServerAddresses == null || !_options.ServerAddresses.Any())
            {
                  _options.ServerAddresses = _configuration!.GetSection("NacosConfig:ServerAddresses").Get<List<string>>();
            }
      }

      private void SetNameSpeace()
      {
            if (string.IsNullOrEmpty(_options.Namespace))
            {
                  _options.Namespace = _configuration.GetSection("NacosConfig:Namespace").Get<string>();
                  namespaceId = _options.Namespace;
            }
      }

      private void SetServerPort(int serverPort)
      {
            this.serverPort = serverPort;
            // env first
            var env = EnvUtil.GetEnvValue(NAMING_SERVER_PORT);

            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var port))
            {
                  this.serverPort = port;
            }
      }

      public async Task<ListView<string>> GetServiceList(int pageNo, int pageSize, string groupName, AbstractSelector? selector)
      {
            var paramters = new Dictionary<string, string>()
            {
                  { CommonParams.NAMESPACE_ID, namespaceId },
                  { CommonParams.GROUP_NAME, groupName },
                  { "pageNo", pageNo.ToString() },
                  { "pageSize", pageSize.ToString() },
            };

            if (selector != null && selector.Type.Equals("label"))
            {
                  paramters[CommonParams.SELECTOR_PARAM] = selector.ToJsonString();
            }

            var result = await ReqApi(UtilAndComs.NacosUrlBase + "/service/list", paramters, HttpMethod.Get).ConfigureAwait(false);

            var json = JsonSerializer.Deserialize<ServiceModel>(result);
            var count = json.count;
            var data = json.doms;

            ListView<string> listView = new ListView<string>(count, data);
            return listView;
      }

      public async Task<ServiceInfo> QueryInstancesOfService(string serviceName, string groupName, string clusters, int udpPort, bool healthyOnly)
      {
            string groupedServiceName = NamingUtils.GetGroupedName(serviceName, groupName);

            var paramters = new Dictionary<string, string>()
            {
                  { CommonParams.NAMESPACE_ID, namespaceId },
                  { CommonParams.SERVICE_NAME, groupedServiceName },
                  { CommonParams.CLUSTERS_PARAM, clusters },
                  { CommonParams.UDP_PORT_PARAM, udpPort.ToString() },
                  { CommonParams.CLIENT_IP_PARAM, NetUtils.LocalIP() },
                  { CommonParams.HEALTHY_ONLY_PARAM, healthyOnly.ToString() },
            };

            var result = await ReqApi(UtilAndComs.NacosUrlBase + "/instance/list", paramters, HttpMethod.Get).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(result))
            {
                  return result.ToObj<ServiceInfo>();
            }

            return new ServiceInfo(groupedServiceName, clusters);
      }

      private async Task<string> ReqApi(string url, Dictionary<string, string> paramters, HttpMethod method)
            => await ReqApi(url, paramters, new Dictionary<string, string>(), method).ConfigureAwait(false);

      private async Task<string> ReqApi(string url, Dictionary<string, string> paramters, Dictionary<string, string> body, HttpMethod method)
            => await ReqApi(url, paramters, body, _defaultServerListManager.GetServerList(), method).ConfigureAwait(false);

      private async Task<string> ReqApi(string url, Dictionary<string, string> paramters, Dictionary<string, string> body, List<string> servers,
            HttpMethod method)
      {
            paramters[CommonParams.NAMESPACE_ID] = namespaceId;

            if ((servers == null || !servers.Any()) && _defaultServerListManager.IsDomain())
                  throw new NacosException(NacosException.INVALID_PARAM, "no server available");

            NacosException exception = new NacosException(string.Empty);

            if (servers != null && servers.Any())
            {
                  int index = Random.Shared.Next(servers.Count);

                  for (int i = 0; i < servers.Count; i++)
                  {
                        var server = servers[i];
                        try
                        {
                              return await CallServer(url, paramters, body, server, method).ConfigureAwait(false);
                        }
                        catch (NacosException e)
                        {
                              exception = e;
                              _logger?.LogDebug(e, "request {0} failed.", server);
                        }

                        index = (index + 1) % servers.Count;
                  }
            }

            if (_defaultServerListManager.IsDomain())
            {
                  for (int i = 0; i < UtilAndComs.REQUEST_DOMAIN_RETRY_COUNT; i++)
                  {
                        try
                        {
                              return await CallServer(url, paramters, body, _defaultServerListManager.GetNacosDomain(), method).ConfigureAwait(false);
                        }
                        catch (NacosException e)
                        {
                              exception = e;
                              _logger?.LogDebug(e, "request {0} failed.", _defaultServerListManager.GetNacosDomain());
                        }
                  }
            }

            _logger?.LogError("request: {0} failed, servers: {1}, code: {2}, msg: {3}", url, servers, exception.ErrorCode, exception.ErrorMsg);

            throw new NacosException(exception.ErrorCode, $"failed to req API: {url} after all servers({servers}) tried: {exception.ErrorMsg}");
      }

      private async Task<string> CallServer(string api, Dictionary<string, string> paramters, Dictionary<string, string> body, string curServer,
            HttpMethod method)
      {
            InjectSecurityInfo(paramters);

            var headers = NamingHttpUtil.BuildHeader();

            var url = string.Empty;

            if (curServer.StartsWith(UtilAndComs.HTTPS) || curServer.StartsWith(UtilAndComs.HTTP))
            {
                  url = curServer.TrimEnd('/') + api;
            }
            else
            {
                  if (IPUtil.ContainsPort(curServer))
                  {
                        curServer = curServer + IPUtil.IP_PORT_SPLITER + serverPort;
                  }

                  // TODO http or https
                  url = UtilAndComs.HTTP + curServer + api;
            }

            try
            {
                  var client = _clientFactory?.CreateClient(Constants.ClientName) ?? new HttpClient();

                  using var cts = new CancellationTokenSource();
                  cts.CancelAfter(TimeSpan.FromMilliseconds(8000));

                  var requestUrl = $"{url}?{InitParams(paramters, body)}";
                  var requestMessage = new HttpRequestMessage(method, requestUrl);

                  BuildHeader(requestMessage, headers);

                  var responseMessage = await client.SendAsync(requestMessage, cts.Token).ConfigureAwait(false);

                  if (responseMessage.IsSuccessStatusCode)
                  {
                        var content = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return content;
                  }
                  else if (responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified)
                  {
                        return string.Empty;
                  }

                  // response body will contains some error message
                  var msg = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

                  throw new NacosException((int)responseMessage.StatusCode, $"{responseMessage.StatusCode}--{msg}");
            }
            catch (Exception ex)
            {
                  _logger?.LogError(ex, "[NA] failed to request");
                  throw new NacosException(NacosException.SERVER_ERROR, ex.Message);
            }
      }

      private void InjectSecurityInfo(Dictionary<string, string> paramters)
      {
            if (!string.IsNullOrWhiteSpace(_securityProxy.GetAccessToken()))
            {
                  paramters[Constants.ACCESS_TOKEN] = _securityProxy.GetAccessToken();
            }

            paramters[CommonParams.APP_FILED] = AppDomain.CurrentDomain.FriendlyName;
            if (string.IsNullOrWhiteSpace(_options.AccessKey)
                || string.IsNullOrWhiteSpace(_options.SecretKey))
                  return;

            string signData = paramters.ContainsKey(CommonParams.SERVICE_NAME_PARAM) && !string.IsNullOrWhiteSpace(paramters[CommonParams.SERVICE_NAME_PARAM])
                  ? DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() + CommonParams.SEPARATOR + paramters[CommonParams.SERVICE_NAME_PARAM]
                  : DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            string signature = HashUtil.GetHMACSHA1(signData, _options.SecretKey);
            paramters[CommonParams.SIGNATURE_FILED] = signature;
            paramters[CommonParams.DATA_FILED] = signData;
            paramters[CommonParams.AK_FILED] = _options.AccessKey;
      }

      private void BuildHeader(HttpRequestMessage requestMessage, Dictionary<string, string> headers)
      {
            requestMessage.Headers.Clear();

            if (headers != null)
            {
                  foreach (var item in headers)
                  {
                        requestMessage.Headers.TryAddWithoutValidation(item.Key, item.Value);
                  }
            }
      }

      public bool ServerHealthy()
      {
            try
            {
                  string result = ReqApi(UtilAndComs.NacosUrlBase + "/operator/metrics", new Dictionary<string, string>(),
                        HttpMethod.Get).ConfigureAwait(false).GetAwaiter().GetResult();

                  var json = System.Text.Json.Nodes.JsonNode.Parse(result).AsObject();

                  string serverStatus = json["status"]?.GetValue<string>();
                  return "UP".Equals(serverStatus);
            }
            catch
            {
                  return false;
            }
      }

      private string InitParams(Dictionary<string, string> dict, Dictionary<string, string> body)
      {
            var builder = new StringBuilder(1024);
            if (dict != null && dict.Any())
            {
                  foreach (var item in dict)
                  {
                        builder.Append($"{item.Key}={item.Value.UrlEncode()}&");
                  }
            }

            if (body != null && body.Any())
            {
                  foreach (var item in body)
                  {
                        builder.Append($"{item.Key}={item.Value.UrlEncode()}&");
                  }
            }

            return builder.ToString().TrimEnd('&');
      }
}