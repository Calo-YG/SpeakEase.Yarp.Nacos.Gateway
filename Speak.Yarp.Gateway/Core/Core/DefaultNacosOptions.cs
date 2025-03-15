namespace Speak.Yarp.Gateway.Core.Core;

public class DefaultNacosOptions
{
      /// <summary>
      /// 默认群组
      /// </summary>
      public List<string> DefaultGroup { get; set; }
      
      /// <summary>
      /// 数量
      /// </summary>
      public int Count { get; set; }
      
      /// <summary>
      /// background 同步 延迟时间
      /// </summary>
      public int Delay { get; set; }
}