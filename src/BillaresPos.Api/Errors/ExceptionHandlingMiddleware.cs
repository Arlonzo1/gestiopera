using BillaresPos.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BillaresPos.Api.Errors;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Validation failed", ex.Message,
                ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
        catch (ValidationAppException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, "Validation failed", ex.Message, ex.Errors);
        }
        catch (AppException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, "Request failed", ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            logger.LogWarning(ex, "Invalid HTTP request body for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Invalid request", "The request body is invalid or has an unsupported value.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            var detail = environment.IsDevelopment() || environment.IsEnvironment("Testing") ? ex.ToString() : "An unexpected error occurred.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Unexpected error", detail);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ValidationProblemDetails(errors?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, string[]>())
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        return context.Response.WriteAsJsonAsync(problem);
    }
}


