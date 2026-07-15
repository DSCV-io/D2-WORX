// -----------------------------------------------------------------------
// <copyright file="RedactedTestObject.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// Type-level <c>[RedactData]</c> fixture used by the composed-pipeline
/// redaction-enforcement test in <c>LoggingPipelineE2ETests</c>. Distinct
/// from <c>IntegrationRedactionFixtures</c> (which is logging-test-scoped)
/// so a logging-test refactor can't break the ServiceDefaults E2E
/// assertion.
/// </summary>
/// <param name="Email">A synthetic email value.</param>
/// <param name="Phone">A synthetic phone value.</param>
/// <param name="Address">A synthetic address value.</param>
[RedactData(Reason = RedactReason.PersonalInformation)]
internal sealed record RedactedTestObject(string Email, string Phone, string Address);
