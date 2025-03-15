using Microsoft.Extensions.Primitives;

namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultProxyConfigReloadToken:IChangeToken
{
      public CancellationTokenSource _cts = new CancellationTokenSource();
      
      public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)=> _cts.Token.Register(callback, state);

      public bool ActiveChangeCallbacks => true;

      public bool HasChanged => _cts.IsCancellationRequested;

      public void OnReload() => _cts.Cancel();
}