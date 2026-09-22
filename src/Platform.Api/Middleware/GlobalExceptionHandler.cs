using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Common.Exceptions;

namespace Platform.Api.Middleware;

/// <summary>
/// Turns every unhandled exception into an RFC 7807 ProblemDetails response,
/// so every error from the API has the same shape and no controller needs a
/// try/catch.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="problemDetailsService">Writes the ProblemDetails body.</param>
    /// <param name="logger">Logs unexpected errors.</param>
    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <summary>
    /// Maps the exception to a status code and writes the response.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="exception">Exception thrown further down the pipeline.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>True once the response is written.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = ToProblem(exception);
        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>
    /// Chooses status, title and a safe-to-show detail for an exception.
    /// Unexpected exceptions never leak their message.
    /// </summary>
    /// <param name="exception">Exception to translate.</param>
    /// <returns>The ProblemDetails to return.</returns>
    private static ProblemDetails ToProblem(Exception exception)
    {
        if (exception is not AppException app)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server error",
                Detail = "An unexpected error occurred.",
            };
        }

        ProblemDetails problem = app.FieldErrors is { } errors
            ? new ValidationProblemDetails(errors)
            : new ProblemDetails();

        problem.Status = app.StatusCode;
        problem.Title = app.Title;
        problem.Detail = app.Message;
        return problem;
    }
}
