namespace Speak.Yarp.Gateway.Core.Core;

public class GatewayServiceOption
{
      public List<ServicesOption> ServicesOptions { get; set; }

      /// <summary>
      /// 前缀
      /// </summary>
      public string Prefix { get; set; } = "api";

      /// <summary>
      /// 格式化服务名称
      /// </summary>
      public Func<string, string> ServiceNameFormatter { get; set; }
}

public sealed class ServicesOption
{
      /// <summary>
      /// 服务名称
      /// </summary>
      public string ServiceName { get; set; }
      
      /// <summary>
      /// 所在群组
      /// </summary>
      public string GroupName { get; set; }

      /// <summary>
      /// 负载均衡
      /// </summary>
      public string LoadBalancingPolicy { get; set; } = "PowerOfTwoChoices";
      
      /// <summary>
      /// 健康检查选项
      /// </summary>
      public HealthyOption HealthyOption { get; set; }
}

public sealed class HealthyOption
{
      /// <summary>
      /// 健康检查测率
      /// </summary>
      public string HealthyPolicy { get; set; } = "ConsecutiveFailures";

      /// <summary>
      /// 超时时间
      /// </summary>
      public int TimeOut { get; set; } = 3;

      /// <summary>
      /// 间隔时间
      /// </summary>
      public int Interval { get; set; } = 10;
}