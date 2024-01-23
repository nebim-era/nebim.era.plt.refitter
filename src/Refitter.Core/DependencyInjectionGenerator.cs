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
            $"public static IServiceCollection Add{iocSettings.ServiceName}Clients(this IServiceCollection services, Uri? baseUrl = null, Action<IHttpClientBuilder>? builder = default)";
        var configureRefitClient = ".ConfigureHttpClient(c => c.BaseAddress = baseUrl)";

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
                        private static RefitSettings CreateRefitSettings()
                        {
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
                        
                            return refitSettings;
                        }
                  
                      {{methodDeclaration}}
                      {
                      
                      var platformSvcSettings =
                         services.BuildServiceProvider().GetRequiredService<IOptions<PlatformServicesSettings>>();
              
                      if (baseUrl is null && string.IsNullOrWhiteSpace(platformSvcSettings.Value.{{iocSettings.ServiceBaseUrlSettingsKey}}))
                          throw new InvalidOperationException("{{iocSettings.ServiceName}} address is not configured.");
                      
                      baseUrl ??= new Uri(platformSvcSettings.Value.AccountServiceAddress!);
              """");
        foreach (var interfaceName in interfaceNames)
        {
            var clientBuilderName = $"clientBuilder{interfaceName}";
            code.Append(
                $$"""
                              var {{clientBuilderName}} = services
                                  .AddRefitClient<{{interfaceName}}>(CreateRefitSettings())
                                  {{configureRefitClient}}
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