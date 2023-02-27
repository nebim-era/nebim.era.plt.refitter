﻿using System.ComponentModel;
using Refitter.Core;
using Spectre.Console.Cli;
using ValidationResult = Spectre.Console.ValidationResult;


var app = new CommandApp<GenerateCommand>();
app.Configure(
    config => config
        .SetApplicationName("refitter")
        .SetApplicationVersion(typeof(GenerateCommand).Assembly.GetName().Version!.ToString())
        .AddExample(
            new[]
            {
                "./openapi.json",
                "--namespace",
                "\"Your.Namespace.Of.Choice.GeneratedCode\"",
                "--output",
                "./Output.cs"
            }));
return app.Run(args);

internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to OpenAPI Specification file")]
        [CommandArgument(0, "[input file]")]
        public string? OpenApiPath { get; set; }

        [Description("Default namespace to use for generated types")]
        [CommandOption("-n|--namespace")]
        [DefaultValue("GeneratedCode")]
        public string? Namespace { get; set; }

        [Description("Path to Output file")]
        [CommandOption("-o|--output")]
        [DefaultValue("Output.cs")]
        public string? OutputPath { get; set; }

        [Description("Don't add <auto-generated> header to output file")]
        [CommandOption("--no-auto-generated-header")]
        [DefaultValue(false)]
        public bool NoAutoGeneratedHeader { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenApiPath))
            return ValidationResult.Error("Input file is required");

        return File.Exists(settings.OpenApiPath)
            ? base.Validate(context, settings)
            : ValidationResult.Error($"File not found - {settings.OpenApiPath}");
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var refitGeneratorSettings = new RefitGeneratorSettings
        {
            OpenApiPath = settings.OpenApiPath!,
            Namespace = settings.Namespace ?? "GeneratedCode",
            AddAutoGeneratedHeader = !settings.NoAutoGeneratedHeader
        };

        var generator = await RefitGenerator.CreateAsync(refitGeneratorSettings);
        var code = generator.Generate();
        await File.WriteAllTextAsync(settings.OutputPath ?? "Output.cs", code);

        return 0;
    }
}