using System.Globalization;
using System.Text;

namespace Refitter.Core;

internal static class DependencyInjectionGenerator
{
    public static string Generate(
        RefitGeneratorSettings settings,
        string[] interfaceNames)
    {
        var iocSettings = settings.DependencyInjectionSettings;
        if (iocSettings is null || !interfaceNames.Any())
            return string.Empty;

        var code = new StringBuilder();

        var methodDeclaration =
            $"public static IServiceCollection AddEra{iocSettings.ServiceName}Clients(this IServiceCollection services, Uri? baseUrl = null, Action<IHttpClientBuilder>? builder = default)";
        var configureHttpClient = """
                                  .ConfigureHttpClient(c =>
                                                  {
                                                      c.BaseAddress = baseUrl;
                                                      c.DefaultRequestVersion = HttpVersion.Version20;
                                                      c.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("br"));
                                                      c.DefaultRequestHeaders.ConnectionClose = false;
                                                      c.DefaultRequestHeaders.Add("Keep-Alive", "timeout=300, max=500");
                                                      c.Timeout = TimeSpan.FromSeconds(30);
                                                  })
                                  """;
        
        var configurePrimaryMessageHandler = """
                                             .ConfigurePrimaryHttpMessageHandler(() =>
                                             {
                                                 return new SocketsHttpHandler
                                                 {
                                                     PooledConnectionLifetime = TimeSpan.FromMinutes(5), // Bağlantılar 5 dakikada bir yenilenir
                                                     EnableMultipleHttp2Connections = true, // Paralel istekler için birden fazla HTTP/2 bağlantısına izin ver
                                                     AutomaticDecompression = DecompressionMethods.All,
                                                     PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2), // 2 dakika kullanılmayan bağlantıları kapat
                                             
                                                     // 8. Keep-alive ayarları
                                                     KeepAlivePingTimeout = TimeSpan.FromSeconds(15), // 15 saniye içinde yanıt gelmezse bağlantıyı kes
                                                     KeepAlivePingDelay = TimeSpan.FromSeconds(60),  // 60 saniyede bir ping gönder
                                                     KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always // Her durumda ping yap
                                                 };
                                             })
                                             """;

        var usings = iocSettings.UsePolly
            ? """
                  using System;
                  using Core.PlatformConfigs;
                  using Microsoft.Extensions.DependencyInjection;
                  using Microsoft.Extensions.Options;
                  using Polly;
                  using Polly.Contrib.WaitAndRetry;
                  using Polly.Extensions.Http;
              """
            : """
                  using System;
                  using Core.PlatformConfigs;
                  using Microsoft.Extensions.DependencyInjection;
                  using Microsoft.Extensions.Options;
              """;

        code.AppendLine();
        code.AppendLine();
        code.AppendLine(
            $$""""
              #nullable enable
              namespace {{settings.Namespace}}
              {
                  {{usings}}
                  
                  public static partial class IServiceCollectionExtensions
                  {
                        private static RefitSettings _refitSettings;
                  
                        private static RefitSettings CreateRefitSettings()
                        {
                           if (_refitSettings != null)
                           {
                               return _refitSettings;
                           }
                           
                            var refitSettings = new RefitSettings();
                            var serializerOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = JsonSerde.DefaultOptions.PropertyNameCaseInsensitive,
                                PropertyNamingPolicy = JsonSerde.DefaultOptions.PropertyNamingPolicy,
                                NumberHandling = JsonSerde.DefaultOptions.NumberHandling,
                                DefaultIgnoreCondition = JsonSerde.DefaultOptions.DefaultIgnoreCondition
                            };
                        
                            foreach (var converter in JsonSerde.DefaultOptions.Converters)
                            {
                                serializerOptions.Converters.Add(converter);
                            }
                        
                            serializerOptions.Converters.Add(new ObjectToInferredTypesConverter());
                            refitSettings.ContentSerializer = new SystemTextJsonContentSerializer(serializerOptions);
                        
                            _refitSettings = refitSettings;
                            return _refitSettings;
                        }
                  
                      {{methodDeclaration}}
                      {
                      
                      var platformSvcSettings =
                         services.BuildServiceProvider().GetRequiredService<IOptions<PlatformServicesSettings>>();
              
                      if (baseUrl is null && string.IsNullOrWhiteSpace(platformSvcSettings.Value.{{iocSettings.ServiceBaseUrlSettingsKey}}))
                          throw new InvalidOperationException("{{iocSettings.ServiceName}} address is not configured.");
                      
                      baseUrl ??= new Uri(platformSvcSettings.Value.{{iocSettings.ServiceBaseUrlSettingsKey}}!);
              """");
        foreach (var interfaceName in interfaceNames)
        {
            var clientBuilderName = $"clientBuilder{interfaceName}";
            code.Append(
                $$"""
                              var {{clientBuilderName}} = services
                                  .AddRefitClient<{{interfaceName}}>(CreateRefitSettings())
                                  {{configureHttpClient}}
                                  {{configurePrimaryMessageHandler}}
                  """);

            foreach (string httpMessageHandler in iocSettings.HttpMessageHandlers)
            {
                code.AppendLine();
                code.Append($"                .AddHttpMessageHandler<{httpMessageHandler}>()");
            }

            if (iocSettings.UsePolly)
            {
                var durationString = iocSettings.FirstBackoffRetryInSeconds.ToString(CultureInfo.InvariantCulture);
                code.AppendLine();
                code.Append(
                    $$"""
                                      .AddPolicyHandler(
                                          HttpPolicyExtensions
                                              .HandleTransientHttpError()
                                              .WaitAndRetryAsync(
                                                  Backoff.DecorrelatedJitterBackoffV2(
                                                      TimeSpan.FromSeconds({{durationString}}),
                                                      {{iocSettings.PollyMaxRetryCount}})))
                      """);
            }

            code.Append(";");
            code.AppendLine();
            code.Append($"            builder?.Invoke({clientBuilderName});");

            code.AppendLine();
            code.AppendLine();
        }

#pragma warning disable RS1035
        code.Remove(code.Length - Environment.NewLine.Length, Environment.NewLine.Length);
#pragma warning restore RS1035
        code.AppendLine();
        code.AppendLine("            return services;");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");
        code.AppendLine();
        return code.ToString();
    }
}