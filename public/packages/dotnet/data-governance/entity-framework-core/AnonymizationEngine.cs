// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngine.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Runtime anonymization engine. Sweeps all registered ownership-marked entity types for
/// a given subject, overwriting PII fields according to their stored
/// <c>D2:Anonymize</c> annotations.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Tier routing:</strong>
/// <list type="bullet">
///   <item>
///     <strong>Tier A</strong> — constant/null/empty-rule columns on scalar, table-split-owned,
///     or complex-property shapes are anonymized via a single <c>ExecuteUpdateAsync</c> per
///     entity type. No rows are materialized. Compiled setter delegates are cached per entity
///     type and reused across sweeps.
///   </item>
///   <item>
///     <strong>Tier B</strong> — any entity type carrying at least one
///     <see cref="AnonymizeKind.Template"/> rule. Rows are materialized in chunks of
///     <see cref="AnonymizationEngineOptions.BatchSize"/>, mutated in CLR, and persisted via
///     <c>SaveChangesAsync</c>. Each chunk commits independently (no single long transaction
///     spanning all chunks). Concurrency conflicts trigger a bounded reload-retry
///     (see <see cref="AnonymizationEngineOptions.MaxConcurrencyRetries"/>). Partial
///     progress from committed chunks is idempotent — a re-run skips already-anonymized rows.
///   </item>
///   <item>
///     <strong>Tier C</strong> — a Tier-C entity reaching the engine at runtime is a
///     deploy-integrity failure (the startup guard should have blocked it at host-build time).
///     The engine returns <see cref="D2Result{TData}.UnhandledException"/> immediately and
///     performs no further writes for that subject.
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Idempotency:</strong> every query filters on <c>IsAnonymized == false</c>.
/// Re-running for the same subject is safe; already-anonymized rows are counted in
/// <see cref="AnonymizationOutcome.AlreadyAnonymizedRows"/> and not touched.
/// </para>
/// <para>
/// <strong>Fail-closed:</strong> if any entity type fails, the method returns a failure
/// result — never <c>Ok</c> with a partial count. The caller must treat a non-Ok result
/// as a partial anonymization and retry.
/// </para>
/// <para>
/// <strong>PII safety:</strong> the subject id is never logged. A fresh <c>sweepId</c>
/// (unrelated to the subject) is generated per call for log correlation.
/// </para>
/// </remarks>
internal sealed class AnonymizationEngine : IAnonymizationEngine
{
    // Per-entity-type plan cache (process-global, keyed by IEntityType).
    private static readonly ConcurrentDictionary<IEntityType, TierAPlan> sr_tierAPlans = new();
    private static readonly ConcurrentDictionary<IEntityType, TierBPlan> sr_tierBPlans = new();

    // Cached MethodInfo for RunTierAAsync<TSource> (one per CLR entity type).
    private static readonly ConcurrentDictionary<Type, MethodInfo> sr_runTierAMethods = new();

    // Cached MethodInfo for CountAlreadyAnonymizedGenericAsync<TSource>
    // and LoadChunkGenericAsync<TSource>.
    private static readonly ConcurrentDictionary<Type, MethodInfo> sr_countMethods = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> sr_loadMethods = new();

    private static readonly MethodInfo sr_runTierAOpenMethod =
        typeof(AnonymizationEngine)
            .GetMethod(nameof(RunTierAAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo sr_countOpenMethod =
        typeof(AnonymizationEngine).GetMethod(
            nameof(CountAlreadyAnonymizedGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo sr_loadOpenMethod =
        typeof(AnonymizationEngine).GetMethod(
            nameof(LoadChunkGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly DbContext r_db;
    private readonly int r_batchSize;
    private readonly int r_maxConcurrencyRetries;
    private readonly ILogger<AnonymizationEngine> r_logger;

    // Static constructor — validates that the open generic MethodInfos resolved correctly.
    static AnonymizationEngine()
    {
        Debug.Assert(
            sr_runTierAOpenMethod.IsGenericMethodDefinition,
            "RunTierAAsync must be a generic method definition.");
        Debug.Assert(
            sr_countOpenMethod.IsGenericMethodDefinition,
            "CountAlreadyAnonymizedGenericAsync must be a generic method definition.");
        Debug.Assert(
            sr_loadOpenMethod.IsGenericMethodDefinition,
            "LoadChunkGenericAsync must be a generic method definition.");
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AnonymizationEngine"/>.
    /// </summary>
    /// <param name="db">The host's scoped <see cref="DbContext"/>.</param>
    /// <param name="options">Engine configuration options.</param>
    /// <param name="logger">Logger for PII-safe sweep diagnostics.</param>
    public AnonymizationEngine(
        DbContext db,
        IOptions<AnonymizationEngineOptions> options,
        ILogger<AnonymizationEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_db = db;
        r_batchSize = options.Value.BatchSize;

        // Clamp negative values to 0 — negative is treated as "no retry" (same as 0).
        r_maxConcurrencyRetries = Math.Max(0, options.Value.MaxConcurrencyRetries);
        r_logger = logger;
    }

    /// <summary>
    /// Gets or sets a test seam invoked inside <c>SaveChunkWithRetryAsync</c> just before
    /// <c>SaveChangesAsync</c> on the first attempt. Tests install an action here to simulate
    /// a concurrent writer bumping a concurrency token, forcing
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> on the first
    /// save and exercising the reload-retry path. Cleared after first invocation.
    /// </summary>
    internal Action? OnBeforeFirstSave { get; set; }

    /// <inheritdoc />
    public Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        if (userId.Falsey())
            return Task.FromResult(D2Result<AnonymizationOutcome>.ValidationFailed());

        return RunSweepAsync(userId, ownershipKind: "user", ct);
    }

    /// <inheritdoc />
    public Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        if (orgId.Falsey())
            return Task.FromResult(D2Result<AnonymizationOutcome>.ValidationFailed());

        return RunSweepAsync(orgId, ownershipKind: "org", ct);
    }

    private static (
        Expression<Action<UpdateSettersBuilder<TSource>>>? Expr,
        bool IsFailClosed,
        string? FailReason)
        BuildSetterExpression<TSource>(
            IEntityType entityType,
            AnonymizationClassification classification)
        where TSource : class
    {
        var builderParam = Expression.Parameter(typeof(UpdateSettersBuilder<TSource>), "b");

        var setPropertyMethod = typeof(UpdateSettersBuilder<TSource>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(
                m => m.Name == "SetProperty"
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.IsGenericParameter);

        if (setPropertyMethod is null)
            return (null, false, null);

        var entityParam = Expression.Parameter(typeof(TSource), "e");

        var isAnonymizedProp =
            entityType.FindProperty(nameof(IAnonymizationTrackable.IsAnonymized));
        if (isAnonymizedProp?.PropertyInfo is null)
            return (null, false, null);

        var isAnonymizedResult = BuildOneSetterCall(
            builderParam,
            entityParam,
            setPropertyMethod,
            isAnonymizedProp,
            typeof(bool),
            true);

        if (isAnonymizedResult.Call is null)
            return (null, false, null);

        var statements = new List<Expression>();

        foreach (var col in classification.Columns)
        {
            if (col.Property.PropertyInfo is null)
                continue;

            object? fauxValue = col.Rule.Kind switch
            {
                AnonymizeKind.SetNull => null,
                AnonymizeKind.SetEmpty => string.Empty,
                AnonymizeKind.Constant => col.Rule.ConstantValue,
                _ => null,
            };

            var colResult = BuildOneSetterCall(
                builderParam,
                entityParam,
                setPropertyMethod,
                col.Property,
                col.Property.ClrType,
                fauxValue);

            if (colResult.IsFailClosed)
                return (null, true, colResult.FailReason);

            if (colResult.Call is not null)
                statements.Add(colResult.Call);
        }

        statements.Add(isAnonymizedResult.Call);

        var body = statements.Count == 1
            ? statements[0]
            : Expression.Block(statements);

        return (
            Expression.Lambda<Action<UpdateSettersBuilder<TSource>>>(body, builderParam),
            false,
            null);
    }

    private static SetterCallResult BuildOneSetterCall(
        ParameterExpression builderParam,
        ParameterExpression entityParam,
        MethodInfo setPropertyOpenMethod,
        IProperty efProperty,
        Type propertyClrType,
        object? value)
    {
        var propertyInfo = efProperty.PropertyInfo;
        if (propertyInfo is null)
            return SetterCallResult.Skip();

        // M-1: SetNull on a non-nullable value type is a misconfiguration — fail-closed.
        if (value is null
            && propertyClrType.IsValueType
            && Nullable.GetUnderlyingType(propertyClrType) is null)
        {
            return SetterCallResult.Fail(
                $"SetNull on non-nullable value-type property "
                + $"'{propertyInfo.DeclaringType?.Name}.{propertyInfo.Name}' "
                + $"(CLR type: {propertyClrType.FullName}). "
                + "Only nullable columns may use SetNull.");
        }

        try
        {
            var chain = BuildNavigationChain(efProperty);

            // Build the member-access expression by walking the navigation chain from the
            // entity root (e.g. e.ShippingAddress.City for a two-element chain).
            Expression current = entityParam;
            foreach (var nav in chain)
                current = Expression.Property(current, nav);
            var memberAccess = Expression.Property(current, propertyInfo);

            var selectorLambda = Expression.Lambda(memberAccess, entityParam);

            var valueConst = value is null
                ? Expression.Constant(null, propertyClrType)
                : Expression.Constant(
                    ConvertValue(value, propertyClrType, efProperty),
                    propertyClrType);

            var closedSetProperty = setPropertyOpenMethod.MakeGenericMethod(propertyClrType);

            return SetterCallResult.Ok(
                Expression.Call(builderParam, closedSetProperty, selectorLambda, valueConst));
        }
        catch
        {
            return SetterCallResult.Skip();
        }
    }

    /// <summary>
    /// Builds the ordered list of CLR navigation <see cref="PropertyInfo"/> values from the
    /// entity root down to the container that directly declares <paramref name="property"/>.
    /// Returns an empty list when the property is declared directly on the root entity type.
    /// Handles two nesting shapes:
    /// <list type="bullet">
    ///   <item>
    ///     <strong>Complex properties</strong> (e.g. <c>Invoice.BillingAddress.City</c>):
    ///     walks up via <c>IProperty.DeclaringType → IComplexType.ComplexProperty →
    ///     IComplexProperty.DeclaringType</c>. Because each step follows the EF model
    ///     navigation (not the CLR type), two navigations to the SAME complex-VO CLR type
    ///     (e.g. <c>BillingAddress</c> and <c>ShippingAddress</c>) resolve to their correct,
    ///     distinct <see cref="PropertyInfo"/> values.
    ///   </item>
    ///   <item>
    ///     <strong>Owned entities (table-split OwnsOne)</strong>: when
    ///     <c>IProperty.DeclaringType</c> is an <see cref="IEntityType"/> that has an
    ///     ownership FK, walks up via the ownership
    ///     <c>IForeignKey.DependentToPrincipal.PropertyInfo</c> chain.
    ///   </item>
    /// </list>
    /// </summary>
    private static IReadOnlyList<PropertyInfo> BuildNavigationChain(IProperty property)
    {
        var reversed = new List<PropertyInfo>();
        ITypeBase current = property.DeclaringType;

        while (true)
        {
            if (current is IComplexType complexType)
            {
                // Complex-property nesting: follow the owning IComplexProperty upward.
                var nav = complexType.ComplexProperty;
                if (nav.PropertyInfo is null)
                    break;

                reversed.Add(nav.PropertyInfo);
                current = nav.DeclaringType;
            }
            else if (current is IEntityType ownedEntityType)
            {
                // Owned-entity nesting: follow the ownership FK upward from the dependent
                // (owned) type to the principal (owner) type.
                var ownership = ownedEntityType.FindOwnership();
                if (ownership is null)
                    break; // root entity — stop

                // PrincipalToDependent is the navigation from the owner to the owned type
                // (e.g. TierAUser.Address), which is the PropertyInfo we need to build the
                // member-access expression (e.g. e.Address.Line1).
                var nav = ownership.PrincipalToDependent;
                if (nav?.PropertyInfo is null)
                    break;

                reversed.Add(nav.PropertyInfo);
                current = ownership.PrincipalEntityType;
            }
            else
            {
                break;
            }
        }

        if (reversed.Count == 0)
            return [];

        reversed.Reverse();
        return reversed;
    }

    private static Expression<Func<TSource, bool>> BuildOwnershipPredicate<TSource>(
        string propertyName,
        Guid subjectId)
        where TSource : class
    {
        var param = Expression.Parameter(typeof(TSource), "e");
        var efPropCall = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(Guid?)],
            param,
            Expression.Constant(propertyName));

        var subjectConst = Expression.Constant((Guid?)subjectId, typeof(Guid?));
        var equality = Expression.Equal(efPropCall, subjectConst);
        return Expression.Lambda<Func<TSource, bool>>(equality, param);
    }

    private static Expression<Func<TSource, bool>> BuildNotAnonymizedPredicate<TSource>()
        where TSource : class
    {
        var param = Expression.Parameter(typeof(TSource), "e");
        var efPropCall = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(bool)],
            param,
            Expression.Constant(nameof(IAnonymizationTrackable.IsAnonymized)));

        return Expression.Lambda<Func<TSource, bool>>(Expression.Not(efPropCall), param);
    }

    private static Expression<Func<TSource, bool>> BuildIsAnonymizedPredicate<TSource>()
        where TSource : class
    {
        var param = Expression.Parameter(typeof(TSource), "e");
        var efPropCall = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(bool)],
            param,
            Expression.Constant(nameof(IAnonymizationTrackable.IsAnonymized)));

        return Expression.Lambda<Func<TSource, bool>>(efPropCall, param);
    }

    private static TierAPlan BuildTierAPlan<TSource>(
        IEntityType entityType,
        AnonymizationClassification classification)
        where TSource : class
    {
        try
        {
            var (setterExpr, isFailClosed, failReason) =
                BuildSetterExpression<TSource>(entityType, classification);

            if (isFailClosed)
                return TierAPlan.FailClosed(failReason);

            if (setterExpr is null)
                return TierAPlan.Invalid;

            return new TierAPlan { SetterAction = setterExpr.Compile(), IsValid = true };
        }
        catch
        {
            return TierAPlan.Invalid;
        }
    }

    private static TierBPlan BuildTierBPlan(AnonymizationClassification classification)
    {
        var templatePlans = new Dictionary<string, AnonymizationTemplatePlan>();
        foreach (var col in classification.Columns)
        {
            if (col.Rule.Kind == AnonymizeKind.Template && col.Rule.Template is not null)
            {
                templatePlans[col.PropertyName] =
                    AnonymizationTemplateResolver.Parse(col.Rule.Template);
            }
        }

        return new TierBPlan { TemplatePlans = templatePlans };
    }

    private static void SetPropertyValue(IProperty property, object instance, object? value)
    {
        if (property.PropertyInfo is null)
            return;

        var propInfo = property.PropertyInfo;
        var chain = BuildNavigationChain(property);

        // Walk down the navigation chain to reach the complex-type sub-instance that
        // directly contains the target property. Using the EF model chain avoids the
        // CLR-reflection-by-type heuristic that incorrectly returns the first matching
        // navigation when an entity maps the same complex VO type more than once.
        var subInstance = instance;
        foreach (var nav in chain)
        {
            subInstance = nav.GetValue(subInstance);
            if (subInstance is null)
                return;
        }

        try
        {
            propInfo.SetValue(
                subInstance,
                ConvertValue(value, propInfo.PropertyType, property));
        }
        catch (Exception)
        {
            // Defensive: skip field on any CLR error
            // (type mismatch, null on non-nullable, etc.).
        }
    }

    private static object? ConvertValue(
        object? value,
        Type targetType,
        IProperty? property = null)
    {
        if (value is null)
            return null;

        // Fast-path: value already matches the CLR property type (most columns).
        if (targetType == value.GetType() || targetType.IsAssignableFrom(value.GetType()))
            return value;

        // Value-converter path: for value-converted properties (e.g. EmailAddress ↔ string)
        // the template resolver returns the provider-side type (string). Use the EF Core
        // type mapping's ConvertFromProviderExpression to convert back to the CLR type
        // (e.g. EmailAddress?). This is the correct inverse of the store→CLR direction
        // that EF applies on materialization, and avoids Convert.ChangeType which has no
        // knowledge of custom value converters.
        if (property is not null)
        {
            var converter = property.GetTypeMapping().Converter;
            if (converter is not null)
            {
                try
                {
                    var converted = converter.ConvertFromProvider(value);
                    if (converted is null
                        || targetType == converted.GetType()
                        || targetType.IsAssignableFrom(converted.GetType()))
                        return converted;
                }
                catch (Exception)
                {
                    // Converter threw — fall through to Convert.ChangeType.
                }
            }
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private async Task<D2Result<AnonymizationOutcome>> RunSweepAsync(
        Guid subjectId,
        string ownershipKind,
        CancellationToken ct)
    {
        if (r_batchSize < 1)
            return D2Result<AnonymizationOutcome>.ServiceUnavailable();

        var sweepId = Guid.NewGuid().ToString("N");
        var isUser = ownershipKind == "user";

        var allEntityTypes = r_db.Model.GetEntityTypes()
            .Where(et => isUser
                ? typeof(IUserOwned).IsAssignableFrom(et.ClrType)
                : typeof(IOrgOwned).IsAssignableFrom(et.ClrType))
            .ToList();

        var exemptTypes = allEntityTypes
            .Where(et => typeof(IExemptFromAnonymization).IsAssignableFrom(et.ClrType))
            .ToList();

        var processableTypes = allEntityTypes
            .Where(et => !typeof(IExemptFromAnonymization).IsAssignableFrom(et.ClrType))
            .ToList();

        AnonymizationEngineLog.SweepStarted(
            r_logger, sweepId, ownershipKind, processableTypes.Count);

        var totalRowsAnonymized = 0;
        var totalAlreadyAnonymized = 0;

        foreach (var entityType in processableTypes)
        {
            var classification = AnonymizationTierClassifier.Classify(entityType);

            D2Result<EntityResult> entityResult;

            try
            {
                entityResult = classification.Tier switch
                {
                    AnonymizationTier.TierA =>
                        await DispatchTierAAsync(
                            entityType, classification, subjectId, isUser, sweepId, ct),
                    AnonymizationTier.TierB =>
                        await RunTierBAsync(
                            entityType, classification, subjectId, isUser, sweepId, ct),
                    AnonymizationTier.TierC =>
                        HandleTierCAtRuntime(entityType, classification, sweepId),
                    _ =>
                        D2Result<EntityResult>.UnhandledException(),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AnonymizationEngineLog.EntityTypeError(
                    r_logger,
                    sweepId,
                    entityType.ClrType.Name,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex));
                return D2Result<AnonymizationOutcome>.ServiceUnavailable();
            }

            if (!entityResult.Success)
                return D2Result<AnonymizationOutcome>.BubbleFail(entityResult);

            var er = entityResult.Data!;
            totalRowsAnonymized += er.Anonymized;
            totalAlreadyAnonymized += er.AlreadyDone;

            AnonymizationEngineLog.EntityTypeDone(
                r_logger,
                sweepId,
                entityType.ClrType.Name,
                classification.Tier.ToString(),
                er.Anonymized);
        }

        var outcome = new AnonymizationOutcome
        {
            EntityTypesProcessed = processableTypes.Count,
            RowsAnonymized = totalRowsAnonymized,
            EntityTypesSkippedExempt = exemptTypes.Count,
            AlreadyAnonymizedRows = totalAlreadyAnonymized,
        };

        AnonymizationEngineLog.SweepComplete(
            r_logger,
            sweepId,
            outcome.EntityTypesProcessed,
            outcome.RowsAnonymized,
            outcome.EntityTypesSkippedExempt,
            outcome.AlreadyAnonymizedRows);

        return D2Result<AnonymizationOutcome>.Ok(outcome);
    }

    private Task<D2Result<EntityResult>> DispatchTierAAsync(
        IEntityType entityType,
        AnonymizationClassification classification,
        Guid subjectId,
        bool isUser,
        string sweepId,
        CancellationToken ct)
    {
        var clrType = entityType.ClrType;
        var method = sr_runTierAMethods.GetOrAdd(
            clrType,
            static t => sr_runTierAOpenMethod.MakeGenericMethod(t));

        return (Task<D2Result<EntityResult>>)method.Invoke(
            this,
            [entityType, classification, subjectId, isUser, sweepId, ct])!;
    }

    private async Task<D2Result<EntityResult>> RunTierAAsync<TSource>(
        IEntityType entityType,
        AnonymizationClassification classification,
        Guid subjectId,
        bool isUser,
        string sweepId,
        CancellationToken ct)
        where TSource : class
    {
        var plan = sr_tierAPlans.GetOrAdd(
            entityType,
            static (et, cl) => BuildTierAPlan<TSource>(et, cl),
            classification);

        if (!plan.IsValid)
        {
            if (plan.IsFailClosedMisconfiguration)
            {
                AnonymizationEngineLog.TierASetNullMisconfiguration(
                    r_logger, sweepId, entityType.ClrType.Name, plan.FailReason ?? "<unknown>");
            }
            else
            {
                AnonymizationEngineLog.TierAPlanInvalid(
                    r_logger, sweepId, entityType.ClrType.Name);
            }

            return D2Result<EntityResult>.UnhandledException();
        }

        var ownershipProp = isUser ? nameof(IUserOwned.UserId) : nameof(IOrgOwned.OrgId);

        var alreadyDone = await r_db.Set<TSource>()
            .Where(BuildOwnershipPredicate<TSource>(ownershipProp, subjectId))
            .Where(BuildIsAnonymizedPredicate<TSource>())
            .CountAsync(ct);

        var setterAction = (Action<UpdateSettersBuilder<TSource>>)plan.SetterAction!;

        var anonymized = await r_db.Set<TSource>()
            .Where(BuildOwnershipPredicate<TSource>(ownershipProp, subjectId))
            .Where(BuildNotAnonymizedPredicate<TSource>())
            .ExecuteUpdateAsync(setterAction, ct);

        return D2Result<EntityResult>.Ok(
            new EntityResult { Anonymized = anonymized, AlreadyDone = alreadyDone });
    }

    private async Task<D2Result<EntityResult>> RunTierBAsync(
        IEntityType entityType,
        AnonymizationClassification classification,
        Guid subjectId,
        bool isUser,
        string sweepId,
        CancellationToken ct)
    {
        var plan = sr_tierBPlans.GetOrAdd(
            entityType,
            static (_, cl) => BuildTierBPlan(cl),
            classification);

        var ownershipProp = isUser ? nameof(IUserOwned.UserId) : nameof(IOrgOwned.OrgId);
        var clrType = entityType.ClrType;
        var isAnonymizedPropInfo =
            clrType.GetProperty(nameof(IAnonymizationTrackable.IsAnonymized));

        var alreadyDone = await CountAlreadyAnonymizedAsync(
            clrType, subjectId, ownershipProp, ct);

        var totalAnonymized = 0;

        while (true)
        {
            var chunk = await LoadChunkAsync(clrType, subjectId, ownershipProp, r_batchSize, ct);
            if (chunk.Falsey())
                break;

            var success = await SaveChunkWithRetryAsync(
                chunk,
                entityType,
                clrType,
                subjectId,
                ownershipProp,
                classification,
                plan,
                isAnonymizedPropInfo,
                sweepId,
                ct);

            if (!success)
                return D2Result<EntityResult>.ServiceUnavailable();

            totalAnonymized += chunk.Count;

            if (chunk.Count < r_batchSize)
                break;
        }

        return D2Result<EntityResult>.Ok(
            new EntityResult { Anonymized = totalAnonymized, AlreadyDone = alreadyDone });
    }

    private async Task<int> CountAlreadyAnonymizedAsync(
        Type clrType,
        Guid subjectId,
        string ownershipProp,
        CancellationToken ct)
    {
        var method = sr_countMethods.GetOrAdd(
            clrType,
            static t => sr_countOpenMethod.MakeGenericMethod(t));

        return await (Task<int>)method.Invoke(this, [subjectId, ownershipProp, ct])!;
    }

    private async Task<int> CountAlreadyAnonymizedGenericAsync<TSource>(
        Guid subjectId,
        string ownershipProp,
        CancellationToken ct)
        where TSource : class
        => await r_db.Set<TSource>()
            .Where(BuildOwnershipPredicate<TSource>(ownershipProp, subjectId))
            .Where(BuildIsAnonymizedPredicate<TSource>())
            .CountAsync(ct);

    private async Task<List<object>> LoadChunkAsync(
        Type clrType,
        Guid subjectId,
        string ownershipProp,
        int batchSize,
        CancellationToken ct)
    {
        var method = sr_loadMethods.GetOrAdd(
            clrType,
            static t => sr_loadOpenMethod.MakeGenericMethod(t));

        return await (Task<List<object>>)method.Invoke(
            this,
            [subjectId, ownershipProp, batchSize, ct])!;
    }

    private async Task<List<object>> LoadChunkGenericAsync<TSource>(
        Guid subjectId,
        string ownershipProp,
        int batchSize,
        CancellationToken ct)
        where TSource : class
    {
        var items = await r_db.Set<TSource>()
            .Where(BuildOwnershipPredicate<TSource>(ownershipProp, subjectId))
            .Where(BuildNotAnonymizedPredicate<TSource>())
            .Take(batchSize)
            .ToListAsync(ct);

        return items.Cast<object>().ToList();
    }

    private async Task<bool> SaveChunkWithRetryAsync(
        List<object> chunk,
        IEntityType entityType,
        Type clrType,
        Guid subjectId,
        string ownershipProp,
        AnonymizationClassification classification,
        TierBPlan plan,
        PropertyInfo? isAnonymizedPropInfo,
        string sweepId,
        CancellationToken ct)
    {
        ownershipProp.ThrowIfFalsey();

        var currentChunk = chunk;
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                foreach (var instance in currentChunk)
                {
                    foreach (var col in classification.Columns)
                    {
                        if (col.Rule.Kind == AnonymizeKind.Template)
                        {
                            if (plan.TemplatePlans.TryGetValue(
                                    col.PropertyName,
                                    out var templatePlan))
                            {
                                var resolved = AnonymizationTemplateResolver.Resolve(
                                    templatePlan, entityType, instance);

                                SetPropertyValue(col.Property, instance, resolved);
                            }
                        }
                        else
                        {
                            object? fauxValue = col.Rule.Kind switch
                            {
                                AnonymizeKind.SetNull => null,
                                AnonymizeKind.SetEmpty => string.Empty,
                                AnonymizeKind.Constant => col.Rule.ConstantValue,
                                _ => null,
                            };

                            SetPropertyValue(col.Property, instance, fauxValue);
                        }
                    }

                    isAnonymizedPropInfo?.SetValue(instance, true);
                }

                // Fire the test hook exactly once (first attempt only) to allow tests to
                // simulate a concurrent writer bumping the concurrency token before our save.
                if (attempt == 1 && OnBeforeFirstSave is not null)
                {
                    var hook = OnBeforeFirstSave;
                    OnBeforeFirstSave = null;
                    hook();
                }

                await r_db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt > r_maxConcurrencyRetries)
                {
                    AnonymizationEngineLog.TierBConcurrencyExhausted(
                        r_logger, sweepId, entityType.ClrType.Name, r_maxConcurrencyRetries);
                    return false;
                }

                AnonymizationEngineLog.TierBConcurrencyRetry(
                    r_logger, sweepId, entityType.ClrType.Name, attempt, r_maxConcurrencyRetries);

                foreach (var entry in r_db.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;

                // Reload using the real subject id and ownership property so the
                // !IsAnonymized filter correctly re-fetches only the rows still pending
                // for this subject. Using Guid.Empty / empty string here would match
                // zero rows, silently returning success without anonymizing anything.
                currentChunk = await LoadChunkAsync(
                    clrType, subjectId, ownershipProp, r_batchSize, ct);

                if (currentChunk.Falsey())
                    return true;
            }
        }
    }

    private D2Result<EntityResult> HandleTierCAtRuntime(
        IEntityType entityType,
        AnonymizationClassification classification,
        string sweepId)
    {
        AnonymizationEngineLog.TierCReachedRuntime(
            r_logger,
            sweepId,
            entityType.ClrType.Name,
            classification.TierCBlocker?.PropertyName ?? "<unknown>");

        return D2Result<EntityResult>.UnhandledException();
    }

    private sealed class SetterCallResult
    {
        public Expression? Call { get; init; }

        public bool IsFailClosed { get; init; }

        public string? FailReason { get; init; }

        public static SetterCallResult Ok(Expression call) => new() { Call = call };

        public static SetterCallResult Skip() => new();

        public static SetterCallResult Fail(string reason) =>
            new() { IsFailClosed = true, FailReason = reason };
    }

    private sealed class EntityResult
    {
        public int Anonymized { get; init; }

        public int AlreadyDone { get; init; }
    }

    private sealed class TierAPlan
    {
        public static readonly TierAPlan Invalid = new() { IsValid = false };

        public bool IsValid { get; init; }

        /// <summary>
        /// Gets a value indicating whether this plan represents a misconfiguration that must
        /// propagate as an engine failure (e.g. SetNull on a non-nullable value-type column).
        /// </summary>
        public bool IsFailClosedMisconfiguration { get; init; }

        public string? FailReason { get; init; }

        public object? SetterAction { get; init; }

        public static TierAPlan FailClosed(string? reason) =>
            new() { IsValid = false, IsFailClosedMisconfiguration = true, FailReason = reason };
    }

    private sealed class TierBPlan
    {
        public IReadOnlyDictionary<string, AnonymizationTemplatePlan> TemplatePlans { get; init; } =
            new Dictionary<string, AnonymizationTemplatePlan>();
    }
}
