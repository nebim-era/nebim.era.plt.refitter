using System.Diagnostics;

using Microsoft.OpenApi.Models;

using Refitter.Core;
using Refitter.Validation;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Refitter;

public sealed class GenerateCommand : AsyncCommand<Settings>
{
    private static readonly string Crlf = Environment.NewLine;

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!settings.NoLogging)
            Analytics.Configure();

        return SettingsValidator.Validate(settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var refitGeneratorSettings = new RefitGeneratorSettings
        {
            OpenApiPath = settings.OpenApiPath!,
            Namespace = settings.Namespace ?? "GeneratedCode",
            AddAutoGeneratedHeader = !settings.NoAutoGeneratedHeader,
            AddAcceptHeaders = !settings.NoAcceptHeaders,
            GenerateContracts = !settings.InterfaceOnly,
            ReturnIApiResponse = settings.ReturnIApiResponse,
            UseCancellationTokens = settings.UseCancellationTokens,
            GenerateOperationHeaders = !settings.NoOperationHeaders,
            UseIsoDateFormat = settings.UseIsoDateFormat,
            TypeAccessibility = settings.InternalTypeAccessibility
                ? TypeAccessibility.Internal
                : TypeAccessibility.Public,
            AdditionalNamespaces = settings.AdditionalNamespaces!,
            MultipleInterfaces = settings.MultipleInterfaces,
            IncludePathMatches = settings.MatchPaths ?? Array.Empty<string>(),
            IncludeTags = settings.Tags ?? Array.Empty<string>(),
            GenerateDeprecatedOperations = !settings.NoDeprecatedOperations,
            OperationNameTemplate = settings.OperationNameTemplate,
            OptionalParameters = settings.OptionalNullableParameters,
            TrimUnusedSchema = settings.TrimUnusedSchema,
            KeepSchemaPatterns = settings.KeepSchemaPatterns ?? Array.Empty<string>()
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            AnsiConsole.MarkupLine($"[green]Refitter v{GetType().Assembly.GetName().Version!}[/]");
            AnsiConsole.MarkupLine(
                settings.NoLogging
                    ? "[green]Support key: Unavailable when logging is disabled[/]"
                    : $"[green]Support key: {SupportInformation.GetSupportKey()}[/]");

            if (!settings.SkipValidation)
                await ValidateOpenApiSpec(settings);

            if (!string.IsNullOrWhiteSpace(settings.SettingsFilePath))
            {
                var json = await File.ReadAllTextAsync(settings.SettingsFilePath);
                refitGeneratorSettings = Serializer.Deserialize<RefitGeneratorSettings>(json);
                refitGeneratorSettings.OpenApiPath = settings.OpenApiPath!;
            }

            var generator = await RefitGenerator.CreateAsync(refitGeneratorSettings);
            var code = generator.Generate().ReplaceLineEndings();
            AnsiConsole.MarkupLine($"[green]Length: {code.Length} bytes[/]");

            var outputPath = GetOutputPath(settings, refitGeneratorSettings);
            AnsiConsole.MarkupLine($"[green]Output: {Path.GetFullPath(outputPath)}[/]");

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(outputPath, code);
            await Analytics.LogFeatureUsage(settings);

            AnsiConsole.MarkupLine($"[green]Duration: {stopwatch.Elapsed}{Crlf}[/]");

            DonationBanner();
            return 0;
        }
        catch (Exception exception)
        {
            if (exception is not OpenApiValidationException)
            {
                AnsiConsole.MarkupLine($"[red]Error:{Crlf}{exception.Message}[/]");
                AnsiConsole.MarkupLine($"[red]Exception:{Crlf}{exception.GetType()}[/]");
                AnsiConsole.MarkupLine($"[yellow]Stack Trace:{Crlf}{exception.StackTrace}[/]");
            }

            await Analytics.LogError(exception, settings);
            return exception.HResult;
        }
    }

    private static void DonationBanner()
    {
        AnsiConsole.MarkupLine("[dim]###################################################################[/]");
        AnsiConsole.MarkupLine("[dim]#  Do you find this tool useful and feel a bit generous?          #[/]");
        AnsiConsole.MarkupLine("[dim]#  https://github.com/sponsors/christianhelle                     #[/]");
        AnsiConsole.MarkupLine("[dim]#  https://www.buymeacoffee.com/christianhelle                    #[/]");
        AnsiConsole.MarkupLine("[dim]#                                                                 #[/]");
        AnsiConsole.MarkupLine("[dim]#  Does this tool not work or does it lack something you need?    #[/]");
        AnsiConsole.MarkupLine("[dim]#  https://github.com/christianhelle/refitter/issues              #[/]");
        AnsiConsole.MarkupLine("[dim]###################################################################[/]");
        AnsiConsole.WriteLine();
    }

    private static string GetOutputPath(Settings settings, RefitGeneratorSettings refitGeneratorSettings)
    {
        var outputPath = settings.OutputPath != Settings.DefaultOutputPath && !string.IsNullOrWhiteSpace(settings.OutputPath)
                        ? settings.OutputPath
                        : refitGeneratorSettings.OutputFilename ?? "Output.cs";

        if (!string.IsNullOrWhiteSpace(refitGeneratorSettings.OutputFolder) &&
            refitGeneratorSettings.OutputFolder != RefitGeneratorSettings.DefaultOutputFolder)
        {
            outputPath = Path.Combine(refitGeneratorSettings.OutputFolder, outputPath);
        }

        return outputPath;
    }

    private static async Task ValidateOpenApiSpec(Settings settings)
    {
        var validationResult = await OpenApiValidator.Validate(settings.OpenApiPath!);
        if (!validationResult.IsValid)
        {
            AnsiConsole.MarkupLine($"[red]{Crlf}OpenAPI validation failed:{Crlf}[/]");

            foreach (var error in validationResult.Diagnostics.Errors)
            {
                TryWriteLine(error, "red", "Error");
            }

            foreach (var warning in validationResult.Diagnostics.Warnings)
            {
                TryWriteLine(warning, "yellow", "Warning");
            }

            validationResult.ThrowIfInvalid();
        }

        AnsiConsole.MarkupLine($"[green]{Crlf}OpenAPI statistics:{Crlf}{validationResult.Statistics}{Crlf}[/]");
    }

    private static void TryWriteLine(
        OpenApiError error,
        string color,
        string label)
    {
        try
        {
            AnsiConsole.MarkupLine($"[{color}]{label}:{Crlf}{error}{Crlf}[/]");
        }
        catch
        {
            // ignored
        }
    }
}