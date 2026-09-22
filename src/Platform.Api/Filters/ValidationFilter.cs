using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Platform.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator (from <c>Platform.Shared</c>) for every
/// action argument that has one, and returns 400 with a ValidationProblem
/// before the action runs. Registered globally, so no controller calls a
/// validator by hand.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates the filter.
    /// </summary>
    /// <param name="serviceProvider">Resolves validators by argument type.</param>
    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Validates the action arguments, short-circuiting with 400 on failure.
    /// </summary>
    /// <param name="context">Action being executed.</param>
    /// <param name="next">Continues to the action.</param>
    /// <returns>A task that completes when the pipeline finishes.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            ValidationResult result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (ValidationFailure failure in result.Errors)
            {
                context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
            });
            return;
        }

        await next();
    }
}
