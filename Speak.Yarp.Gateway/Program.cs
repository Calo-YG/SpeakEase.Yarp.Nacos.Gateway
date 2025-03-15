using Nacos.Microsoft.Extensions.Configuration;
using Nacos.V2;
using Serilog;
using Serilog.Events;
using Speak.Yarp.Gateway.Core.Core;
using Speak.Yarp.Gateway.Core.Core.Interface;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);


Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File("Logs/LogInformation.txt", rollingInterval: RollingInterval.Day,restrictedToMinimumLevel: LogEventLevel.Information)
      .WriteTo.File("Logs/LogError.txt", rollingInterval: RollingInterval.Day,restrictedToMinimumLevel: LogEventLevel.Error)
      .WriteTo.File("Logs/LogWarning.txt", rollingInterval: RollingInterval.Day,restrictedToMinimumLevel: LogEventLevel.Warning)
      .CreateLogger();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

# region  配置nacos
var nacosoptions = builder.Configuration.GetSection("NacosConfig").Get<NacosSdkOptions>();
var listener = builder.Configuration.GetSection("NacosConfig:Listeners").Get<List<ConfigListener>>();
builder.Configuration.AddNacosV2Configuration(naop =>
{
      naop.SecretKey= nacosoptions.SecretKey;
      naop.ServerAddresses = nacosoptions.ServerAddresses;
      naop.ListenInterval = nacosoptions.ListenInterval;
      naop.Listeners = listener;
      naop.ConfigFilterAssemblies = nacosoptions.ConfigFilterAssemblies;
      naop.ConfigFilterExtInfo = nacosoptions.ConfigFilterExtInfo;
      naop.ConfigUseRpc = nacosoptions.ConfigUseRpc;
      naop.NamingUseRpc = nacosoptions.NamingUseRpc;
      naop.Password = nacosoptions.Password;
      naop.EndPoint = nacosoptions.EndPoint;
      naop.DefaultTimeOut = nacosoptions.DefaultTimeOut;
      naop.NamingCacheRegistryDir = nacosoptions.NamingCacheRegistryDir;
      naop.NamingLoadCacheAtStart = nacosoptions.NamingLoadCacheAtStart;
      naop.NamingUseRpc = nacosoptions.NamingUseRpc;
      naop.UserName = nacosoptions.UserName;
      naop.Namespace = nacosoptions.Namespace;
});
builder.Services.AddHttpClient("Nacos");
builder.Services.AddSingleton<DefaultHttpClientProxy>();
builder.Services.AddSingleton<INacosService, NacosService>();
builder.Services.Configure<DefaultNacosOptions>(options =>
{
      var defaultConfig = builder.Configuration.GetSection("DefaultNacosOptions").Get<DefaultNacosOptions>();
      options.DefaultGroup = defaultConfig!.DefaultGroup;
      options.Count = defaultConfig.Count;
});
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
      app.UseSwagger();
      app.UseSwaggerUI(options =>
      {
            var currentpapp = app;
            using var serviceScope = currentpapp.Services.CreateScope();
            var nacosServiceProvider = currentpapp.Services.GetRequiredService<INacosService>();
            var instances = nacosServiceProvider.GetAllInstance();

            if (instances == null || !instances.Any())
            {
                  options.SwaggerEndpoint("/swagger/v1/swagger.json", "Speak.Yarp.Gateway");
                  options.EnableDeepLinking();
                  options.DocExpansion(DocExpansion.None);
            }
            else
            {
                  foreach (var item in instances)
                  {
                        options.SwaggerEndpoint($"http://{item.Ip}:{item.Port}/swagger/v1/swagger.json",$"{ item.ServiceName}-{item.Ip}-{item.Port}");
                        options.EnableDeepLinking();
                        options.DocExpansion(DocExpansion.None);
                  }
            }
      });
}

app.UseHttpsRedirection();

app.Run();