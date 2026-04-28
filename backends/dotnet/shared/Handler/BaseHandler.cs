// -----------------------------------------------------------------------
// <copyright file="BaseHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

using System.Diagnostics;
using D2.Shared.Result;
using FluentValidation;
using Microsoft.Extensions.Logging;

/// <summary>
/// A base implementation of the <see cref="IHandler{TInput, TOutput}"/> interface that provides
/// common functionality for handling requests, including logging, timing, and exception
/// handling.
/// </summary>
///
/// <typeparam name="THandler">
/// The type of the handler's implementation.
/// </typeparam>
/// <typeparam name="TInput">
/// The type of input the handler processes.
/// </typeparam>
/// <typeparam name="TOutput">
/// The type of output the handler produces.
/// </typeparam>
public abstract partial class BaseHandler<THandler, TInput, TOutput> : IHandler<TInput, TOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseHandler{THandler, TInput, TOutput}"/>
    /// class.
    /// </summary>
    ///
    /// <param name="context">
    /// The handler context for the current operation.
    /// </param>
    protected BaseHandler(IHandlerContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Gets the handler context for the current operation.
    /// </summary>
    protected IHandlerContext Context { get; }

    /// <summary>
    /// Gets the trace ID associated with the current request.
    /// </summary>
    protected string? TraceId => Context.Request.TraceId;

    /// <summary>
    /// Gets the default handler options for this handler type.
    /// Override to customize logging behavior (e.g., suppress input/output for sensitive data).
    /// </summary>
    protected virtual HandlerOptions DefaultOptions => new();

    /// <inheritdoc />
    /// <remarks>
    /// This method wraps the execution of the handler with logging, timing, and exception
    /// handling. It logs the start and end of the handler execution, measures the elapsed time,
    /// and logs any unhandled exceptions that occur during execution. The actual handler logic is
    /// implemented in the abstract <see cref="ExecuteAsync"/> method, which must be overridden by
    /// derived classes.
    /// </remarks>
    public async ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        // Enrich the parent span (e.g., gRPC server span, HTTP span) with context
        // attributes so they appear on the top-level span in Tempo — not just on
        // this handler's child span. Harmless if the Gateway middleware already set them.
        EnrichActivity(Activity.Current, Context.Request);

        // Create a new activity for tracing.
        using var activity = BHASW.SR_ActivitySource.StartActivity(typeof(THandler).Name);

        // Add metadata to the activity.
        activity?.SetTag("handler.type", typeof(THandler).FullName ?? typeof(THandler).Name);
        activity?.SetTag("trace.id", Context.Request.TraceId);

        SetTagIfNotNull(activity, "user.id", Context.Request.UserId);
        SetTagIfNotNull(activity, "agent.org.id", Context.Request.AgentOrgId);
        SetTagIfNotNull(activity, "target.org.id", Context.Request.TargetOrgId);

        // Push request context fields into MEL scope so they appear on ALL log entries
        // within this handler — including on gRPC services that don't run the Gateway's
        // RequestContextLoggingMiddleware. Uses ILogger.BeginScope so both OTel OTLP
        // exporter (IncludeScopes) and Serilog (maps to LogContext) see the properties.
        var logScope = BuildLogScope();
        using var scope = logScope.Count > 0 ? Context.Logger.BeginScope(logScope) : null;

        // Start the stopwatch to measure elapsed time.
        var sw = Stopwatch.StartNew();

        // Use default options if none are provided.
        options ??= DefaultOptions;

        // Record invocation metric.
        var tags = new TagList { { "handler.name", typeof(THandler).Name } };
        BHASW.SR_Invocations.Add(1, tags);

        // Wrap the execution in a try-catch to handle exceptions.
        try
        {
            // Log that we are now executing the handler.
            LogHandlerExecuting(Context.Logger, typeof(THandler).Name, Context.Request.TraceId);

            // Log the input as debug if enabled.
            if (options.LogInput)
            {
                LogHandlerReceivedInput(Context.Logger, typeof(THandler).Name, input, Context.Request.TraceId);
            }

            // Execute the handler's logic.
            var result = await ExecuteAsync(input, ct);

            // Auto-inject traceId if the handler didn't set it explicitly.
            result = InjectTraceId(result);

            // Stop the stopwatch to measure elapsed time.
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            // Log the output as debug if enabled.
            if (options.LogOutput)
            {
                LogHandlerProducedResult(Context.Logger, typeof(THandler).Name, result, Context.Request.TraceId);
            }

            // Add result metadata to the activity.
            activity?.SetTag("handler.success", result.Success);
            activity?.SetTag("handler.status.code", result.StatusCode);
            activity?.SetTag("handler.error.code", result.ErrorCode);
            activity?.SetTag("handler.elapsed.ms", elapsedMs);
            activity?.SetStatus(result.Success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

            // Record duration metric and optional failure metric.
            BHASW.SR_Duration.Record(elapsedMs, tags);
            if (!result.Success)
            {
                BHASW.SR_Failures.Add(1, tags);
            }

            // Determine the log level based on success and elapsed time.
            var level = options.SuppressTimeWarnings
                ? result.Success
                    ? LogLevel.Information
                    : LogLevel.Warning
                : result.Success && elapsedMs < options.WarningThresholdMs
                    ? LogLevel.Information
                    : elapsedMs < options.CriticalThresholdMs
                        ? LogLevel.Warning
                        : LogLevel.Critical;

            // Log the completion of the handler execution.
            LogHandlerExecuted(
                Context.Logger,
                level,
                typeof(THandler).Name,
                result.Success ? "successfully" : "unsuccessfully",
                elapsedMs,
                Context.Request.TraceId);

            // Return the result.
            return result;
        }
        catch (Exception ex)
        {
            // Stop the stopwatch.
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            // Create an unhandled exception result.
            var errorResult = D2Result<TOutput?>.UnhandledException(
                traceId: Context.Request.TraceId);

            // Add exception metadata to the activity.
            activity?.SetTag("handler.success", errorResult.Success);
            activity?.SetTag("handler.status.code", errorResult.StatusCode);
            activity?.SetTag("handler.error.code", errorResult.ErrorCode);
            activity?.SetTag("handler.elapsed.ms", elapsedMs);
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);

            // Record exception duration and exception counter metrics.
            BHASW.SR_Duration.Record(elapsedMs, tags);
            BHASW.SR_Exceptions.Add(1, tags);

            // Log the unhandled exception.
            LogHandlerUnhandledException(Context.Logger, ex, typeof(THandler).Name, elapsedMs, Context.Request.TraceId);

            // Return the error result.
            return errorResult;
        }
    }

    /// <summary>
    /// Executes the core logic of the handler.
    /// </summary>
    ///
    /// <param name="input">
    /// The input to be processed by the handler.
    /// </param>
    /// <param name="ct">
    /// A cancellation token to cancel the operation.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask{D2Result}"/> containing the result of the handler's execution.
    /// </returns>
    protected abstract ValueTask<D2Result<TOutput?>> ExecuteAsync(
        TInput input,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the input using configurable <paramref name="options"/>.
    /// </summary>
    ///
    /// <param name="options">
    /// An optional action to configure the validator.
    /// </param>
    /// <param name="input">
    /// The input to be validated.
    /// </param>
    /// <param name="ct">
    /// A cancellation token to cancel the validation operation.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask{D2Result}"/> containing the result of the validation.
    /// </returns>
    protected async ValueTask<D2Result> ValidateInput(
        Action<AbstractValidator<TInput>>? options,
        TInput input,
        CancellationToken ct = default)
    {
        // Create the validator with the provided configuration.
        var validator = new ConfigurableValidator(options);

        // Validate the input asynchronously.
        var validationResult = await validator.ValidateAsync(input, ct);

        // If valid, return OK.
        if (validationResult.IsValid)
        {
            return D2Result.Ok();
        }

        // Group errors by property name and select into a 2D list.
        var errors = validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .Select(g =>
                new List<string> { g.Key }
                    .Concat(g.Select(e => e.ErrorMessage))
                    .ToList())
            .ToList();

        // Return validation failed result with errors.
        return D2Result.ValidationFailed(
            inputErrors: errors);
    }

    /// <summary>
    /// Enriches an activity with standard request context span attributes.
    /// Uses the same attribute names as the Gateway's RequestContextLoggingMiddleware.
    /// </summary>
    private static void EnrichActivity(Activity? activity, IRequestContext req)
    {
        if (activity is null)
        {
            return;
        }

        SetTagIfNotNull(activity, "userId", req.UserId);
        SetTagIfNotNull(activity, "username", req.Username);
        SetTagIfNotNull(activity, "agentOrgId", req.AgentOrgId);
        SetTagIfNotNull(activity, "agentOrgType", req.AgentOrgType);
        SetTagIfNotNull(activity, "agentOrgRole", req.AgentOrgRole);
        SetTagIfNotNull(activity, "targetOrgId", req.TargetOrgId);
        SetTagIfNotNull(activity, "targetOrgType", req.TargetOrgType);

        // Auth/trust flags — skip when null (unknown in pre-auth handlers).
        if (req.IsAuthenticated is not null)
        {
            activity.SetTag("isAuthenticated", (bool)req.IsAuthenticated);
        }

        if (req.IsTrustedService is not null)
        {
            activity.SetTag("isTrustedService", (bool)req.IsTrustedService);
        }

        if (req.IsAuthenticated is true)
        {
            activity.SetTag("isOrgEmulating", req.IsOrgEmulating ?? false);
        }
    }

    /// <summary>
    /// Sets a tag on an activity only if the value is non-null.
    /// </summary>
    private static void SetTagIfNotNull(Activity? activity, string key, object? value)
    {
        if (value is not null)
        {
            activity?.SetTag(key, value.ToString());
        }
    }

    /// <summary>
    /// Adds a key-value pair to a dictionary only if the value is non-null.
    /// </summary>
    private static void AddIfNotNull(Dictionary<string, object?> dict, string key, object? value)
    {
        if (value is not null)
        {
            dict[key] = value.ToString();
        }
    }

    /// <summary>
    /// Logs that a handler is beginning execution.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Executing handler {HandlerName}. TraceId: {TraceId}.")]
    private static partial void LogHandlerExecuting(ILogger logger, string handlerName, string? traceId);

    /// <summary>
    /// Logs the input received by a handler at debug level.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Handler {HandlerName} received input: {Input}. TraceId: {TraceId}.")]
    private static partial void LogHandlerReceivedInput(ILogger logger, string handlerName, object? input, string? traceId);

    /// <summary>
    /// Logs the result produced by a handler at debug level.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Handler {HandlerName} produced result: {Output}. TraceId: {TraceId}.")]
    private static partial void LogHandlerProducedResult(ILogger logger, string handlerName, object? output, string? traceId);

    /// <summary>
    /// Logs the completion of a handler execution with a dynamic log level determined by
    /// elapsed time thresholds and success status.
    /// </summary>
    [LoggerMessage(EventId = 4, Message = "Executed handler {HandlerName} {Status} in {ElapsedMilliseconds}ms. TraceId: {TraceId}.")]
    private static partial void LogHandlerExecuted(ILogger logger, LogLevel level, string handlerName, string status, long elapsedMilliseconds, string? traceId);

    /// <summary>
    /// Logs an unhandled exception that occurred during handler execution.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Handler {HandlerName} encountered an unhandled exception after {ElapsedMilliseconds}ms. TraceId: {TraceId}.")]
    private static partial void LogHandlerUnhandledException(ILogger logger, Exception ex, string handlerName, long elapsedMilliseconds, string? traceId);

    /// <summary>
    /// Builds a dictionary of non-PII request context fields for log scope enrichment.
    /// </summary>
    private Dictionary<string, object?> BuildLogScope()
    {
        var req = Context.Request;
        var scope = new Dictionary<string, object?>();

        AddIfNotNull(scope, "userId", req.UserId);
        AddIfNotNull(scope, "username", req.Username);
        AddIfNotNull(scope, "agentOrgId", req.AgentOrgId);
        AddIfNotNull(scope, "agentOrgType", req.AgentOrgType);
        AddIfNotNull(scope, "agentOrgRole", req.AgentOrgRole);
        AddIfNotNull(scope, "targetOrgId", req.TargetOrgId);
        AddIfNotNull(scope, "targetOrgType", req.TargetOrgType);
        if (req.IsAuthenticated is not null)
        {
            scope["isAuthenticated"] = (bool)req.IsAuthenticated;
        }

        if (req.IsAuthenticated == true)
        {
            scope["isOrgEmulating"] = req.IsOrgEmulating ?? false;
        }

        return scope;
    }

    /// <summary>
    /// Injects the ambient TraceId into the result if the handler didn't set it explicitly.
    /// This eliminates the need for every handler to pass <c>traceId: TraceId</c> manually.
    /// </summary>
    private D2Result<TOutput?> InjectTraceId(D2Result<TOutput?> result)
    {
        if (result.TraceId is not null || TraceId is null)
        {
            return result;
        }

        return new D2Result<TOutput?>(
            result.Success,
            result.Data,
            result.Messages,
            result.InputErrors,
            result.StatusCode,
            result.ErrorCode,
            TraceId);
    }

    /// <summary>
    /// A configurable validator that allows dynamic configuration of validation rules via an
    /// action.
    /// </summary>
    private sealed class ConfigurableValidator : AbstractValidator<TInput>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurableValidator"/> class.
        /// </summary>
        ///
        /// <param name="configure">
        /// An optional action to configure the validator.
        /// </param>
        public ConfigurableValidator(Action<AbstractValidator<TInput>>? configure = null)
            => configure?.Invoke(this);
    }
}
