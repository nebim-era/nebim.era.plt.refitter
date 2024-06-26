﻿using System.Runtime.Serialization;

namespace Refitter.Validation;

public class OpenApiValidationException : Exception
{
    public OpenApiValidationResult ValidationResult { get; } = null!;

    public OpenApiValidationException(
        OpenApiValidationResult validationResult) 
        : base("OpenAPI validation failed")
    {
        ValidationResult = validationResult;
    }
}