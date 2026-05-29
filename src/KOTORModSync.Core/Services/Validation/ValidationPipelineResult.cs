// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

using JetBrains.Annotations;

using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
    public enum ValidationPipelineStage
    {
        Environment,
        Conflicts,
        InstallOrder,
        ComponentValidation,
        DryRun,
    }

    public sealed class ValidationPipelineStageResult
    {
        public ValidationPipelineStage Stage { get; set; }

        public bool Passed { get; set; }

        public bool HasWarnings { get; set; }

        [CanBeNull]
        public string Summary { get; set; }

        [NotNull]
        public List<string> Messages { get; } = new List<string>();
    }

    /// <summary>
    /// Aggregate result from <see cref="InstallationValidationPipeline"/>.
    /// </summary>
    public sealed class ValidationPipelineResult
    {
        public bool IsSuccess { get; set; }

        public bool HasCriticalErrors { get; set; }

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public int PassedCount { get; set; }

        [CanBeNull]
        public DryRunValidationResult DryRunResult { get; set; }

        [NotNull]
        public List<ValidationPipelineStageResult> Stages { get; } = new List<ValidationPipelineStageResult>();

        public int ExitCode => IsSuccess ? 0 : 1;
    }
}
