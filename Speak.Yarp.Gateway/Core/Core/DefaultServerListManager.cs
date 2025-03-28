using Nacos.V2;
using Nacos.V2.Common;
using Nacos.V2.Naming.Utils;
using Nacos.V2.Utils;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultServerListManager
{
        private readonly IHttpClientFactory _httpClientFactory;

        private const int DEFAULT_TIMEOUT = 5000;

        private readonly ILogger _logger;

        private long _refreshServerListInternal = 30000;

        private int _currentIndex = 0;

        private List<string> _serversFromEndpoint = new List<string>();

        private List<string> _serverList = new List<string>();

        private Timer _refreshServerListTimer;

        private string _endpoint;

        private string _nacosDomain;

        private long _lastServerListRefreshTime = 0L;

        private readonly string _namespace;

        public DefaultServerListManager(ILogger logger, NacosSdkOptions options, string @namespace,IHttpClientFactory httpClientFactory)
        {
            this._logger = logger;
            this._namespace = @namespace;
            this._httpClientFactory = httpClientFactory;
            InitServerAddr(options);
        }

        private void InitServerAddr(NacosSdkOptions options)
        {
            this._endpoint = options.EndPoint;

            if (!string.IsNullOrWhiteSpace(_endpoint))
            {
                this._serversFromEndpoint = GetServerListFromEndpoint()
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                this._refreshServerListTimer = new Timer(
                    async x =>
                    {
                        await RefreshSrvIfNeedAsync().ConfigureAwait(false);
                    }, null, 0, _refreshServerListInternal);
            }
            else
            {
                var serverlist = options.ServerAddresses;
                this._serverList.AddRange(serverlist);
                if (this._serverList.Count == 1)
                {
                    this._nacosDomain = serverlist[0];
                }
            }
        }

        private async Task<List<string>> GetServerListFromEndpoint()
        {
            var list = new List<string>();
            try
            {
                var q = _namespace.IsNotNullOrWhiteSpace() ? $"namespace={_namespace}" : string.Empty;

                var url = $"http://{_endpoint}/nacos/serverlist?{q}";

                var header = NamingHttpUtil.BuildHeader();

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT));

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
                
                foreach (var item in header) req.Headers.TryAddWithoutValidation(item.Key, item.Value);

                using var httpClient = _httpClientFactory.CreateClient("Nacos");
                using var resp = await httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while requesting: {url} . Server returned: {resp.StatusCode}");
                }

                var str = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                using StringReader sr = new StringReader(str);
                while (true)
                {
                    var line = await sr.ReadLineAsync().ConfigureAwait(false);
                    if (line == null || line.Length <= 0)
                        break;

                    list.Add(line.Trim());
                }

                return list;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SERVER-LIST] failed to update server list.");
                return null;
            }
        }

        private async Task RefreshSrvIfNeedAsync()
        {
            try
            {
                if (_serverList != null && _serverList.Count > 0) return;

                _logger?.LogDebug("server list provided by user: {0}", string.Join(",", _serverList));

                if (DateTimeOffset.Now.ToUnixTimeSeconds() - _lastServerListRefreshTime < _refreshServerListInternal) return;

                var list = await GetServerListFromEndpoint().ConfigureAwait(false);

                if (list == null || list.Count <= 0)
                    throw new Exception("Can not acquire Nacos list");

                List<string> newServerAddrList = new List<string>();

                foreach (var server in list)
                {
                    if (server.StartsWith(Constants.HTTPS, StringComparison.OrdinalIgnoreCase)
                        || server.StartsWith(Constants.HTTP, StringComparison.OrdinalIgnoreCase))
                    {
                        newServerAddrList.Add(server);
                    }
                    else
                    {
                        newServerAddrList.Add($"{Constants.HTTP}{server}");
                    }
                }

                _serversFromEndpoint = newServerAddrList;
                _lastServerListRefreshTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "failed to update server list");
            }
        }

        public string GenNextServer()
        {
            int index = Interlocked.Increment(ref _currentIndex) % GetServerList().Count;
            return GetServerList()[index];
        }

        public string GetCurrentServer()
            => GetServerList()[_currentIndex % GetServerList().Count];

        public List<string> GetServerList()
            => _serverList == null || !_serverList.Any() ? _serversFromEndpoint : _serverList;

        internal bool IsDomain() => !string.IsNullOrWhiteSpace(_nacosDomain);

        internal string GetNacosDomain() => _nacosDomain;

        public void Dispose()
        {
            _refreshServerListTimer?.Dispose();
        }
}