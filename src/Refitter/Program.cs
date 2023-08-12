﻿using Refitter;
using Spectre.Console.Cli;


Analytics.Configure();

var app = new CommandApp<GenerateCommand>();
app.Configure(
    configuration =>
    {
        configuration
            .SetApplicationName("refitter")
            .SetApplicationVersion(typeof(GenerateCommand).Assembly.GetName().Version!.ToString());

        configuration
            .AddExample("./openapi.json");

        configuration
            .AddExample("https://petstore3.swagger.io/api/v3/openapi.yaml");

        configuration
            .AddExample(
                "./openapi.json",
                "--namespace",
                "\"Your.Namespace.Of.Choice.GeneratedCode\"",
                "--output",
                "./GeneratedCode.cs");

        configuration
            .AddExample(
                "./openapi.json",
                "--namespace",
                "\"Your.Namespace.Of.Choice.GeneratedCode\"",
                "--internal");

        configuration
            .AddExample(
                "./openapi.json",
                "--output",
                "./IGeneratedCode.cs",
                "--interface-only");

        configuration
            .AddExample(
                "./openapi.json",
                "--use-api-response");

        configuration
            .AddExample(
                "./openapi.json",
                "--cancellation-tokens");

        configuration
            .AddExample(
                "./openapi.json",
                "--no-operation-headers");

        configuration
            .AddExample(
                "./openapi.json",
                "--use-iso-date-format");

        configuration
            .AddExample(
                "./openapi.json",
                "--additional-namespace",
                "\"Your.Additional.Namespace\"",
                "--additional-namespace",
                "\"Your.Other.Additional.Namespace\"");

        configuration
            .AddExample(
                "./openapi.json",
                "--multiple-interfaces");
    });

return app.Run(args);