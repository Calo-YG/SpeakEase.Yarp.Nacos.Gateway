using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Yarp.ReverseProxy.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultProxyConfigProvider:BackgroundService,IProxyConfigProvider
{
      private readonly object _lockObject = new();
      private readonly IDefaultProxyConfigStorage _defaultProxyConfigStorage;
      private readonly ILogger _logger;
      private CancellationTokenSource? _changeToken;
      private bool _disposed;
      private IDisposable? _subscription;
     
      private DefaultProxyConfig _snapshot;

      private readonly DefaultNacosOptions _defaultNacosOptions;

      public DefaultProxyConfigProvider(IDefaultProxyConfigStorage defaultProxyConfigStorage,ILoggerFactory loggerFactory,IOptions<DefaultNacosOptions> defaultNacosOptions)
      {
            _defaultProxyConfigStorage = defaultProxyConfigStorage;
            _logger = loggerFactory.CreateLogger("DefaultProxyConfigProvider");
            _defaultNacosOptions = defaultNacosOptions.Value;
      }
      
      public IProxyConfig GetConfig()
      {
            if (_snapshot == null)
            {
                  _subscription = ChangeToken.OnChange(_defaultProxyConfigStorage.GetReloadToken, UpdateSnapshot);
                  UpdateSnapshot();
            }

            return _snapshot;
      }
      
      [MemberNotNull(nameof(UpdateSnapshot))]
      private void UpdateSnapshot()
      {
            // Prevent overlapping updates, especially on startup.
            lock (_lockObject)
            {
                  
                  DefaultProxyConfig newSnapshot;
                  try
                  {
                        newSnapshot = _defaultProxyConfigStorage.GetProxyConfig() as DefaultProxyConfig;
                  }
                  catch (Exception ex)
                  {
                        _logger.LogError("Exceptions Message");

                        // Re-throw on the first time load to prevent app from starting.
                        if (_snapshot is null)
                        {
                              throw;
                        }

                        return;
                  }

                  var oldToken = _changeToken;
                  _changeToken = new CancellationTokenSource();
                  newSnapshot!.ChangeToken = new CancellationChangeToken(_changeToken.Token);
                  _snapshot = newSnapshot;

                  try
                  {
                        oldToken?.Cancel(throwOnFirstException: false);
                  }
                  catch (Exception ex)
                  {
                        _logger.LogError(ex.Message);
                  }
            }
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
            var delay = _defaultNacosOptions.Delay == 0 ? 10 * 1000 : _defaultNacosOptions.Delay * 1000;

            while (!stoppingToken.IsCancellationRequested)
            {
                  await Task.Delay(TimeSpan.FromMilliseconds(delay), stoppingToken);

                  await _defaultProxyConfigStorage.RefreshProxyConfig(stoppingToken).ConfigureAwait(false);
            }
      }
}