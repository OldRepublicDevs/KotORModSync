// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Core.Services.Validation
{
    /// <summary>
    /// Options for <see cref="InstallationValidationPipeline"/>. Mirrors CLI validate flags and wizard presets.
    /// </summary>
    public sealed class ValidationPipelineOptions
    {
        /// <summary>Run environment / HoloPatcher checks (CLI <c>--full</c>).</summary>
        public bool FullValidation { get; set; }

        /// <summary>Run VFS instruction dry-run after component checks (CLI <c>--dry-run</c>).</summary>
        public bool DryRun { get; set; }

        /// <summary>Skip per-component archive validation; VFS dry-run only (CLI <c>--dry-run-only</c>).</summary>
        public bool DryRunOnly { get; set; }

        /// <summary>Suppress non-error log output (CLI <c>--errors-only</c>).</summary>
        public bool ErrorsOnly { get; set; }

        /// <summary>Only validate components with <see cref="ModComponent.IsSelected"/> true.</summary>
        public bool UseFileSelection { get; set; } = true;

        /// <summary>Skip HoloPatcher environment probe (tests and headless fixtures).</summary>
        public bool SkipEnvironmentValidation { get; set; }

        /// <summary>Skip per-component archive validation (tests that only need graph checks).</summary>
        public bool SkipComponentArchiveValidation { get; set; }

        [CanBeNull]
        public MainConfig MainConfig { get; set; }

        [CanBeNull]
        public Func<string, Task<bool?>> ConfirmationCallback { get; set; }

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>Wizard / install-wizard preset: env, conflicts, order, archives, dry-run on selected mods.</summary>
        public static ValidationPipelineOptions WizardFull => new ValidationPipelineOptions
        {
            FullValidation = true,
            DryRun = true,
            UseFileSelection = true,
        };

        /// <summary>Legacy Getting Started validate: dry-run on selected mods only.</summary>
        public static ValidationPipelineOptions LegacyDryRunOnly => new ValidationPipelineOptions
        {
            DryRun = true,
            UseFileSelection = true,
        };

        /// <summary>CLI <c>validate --full --dry-run --use-file-selection</c>.</summary>
        public static ValidationPipelineOptions CliFullWithDryRun => new ValidationPipelineOptions
        {
            FullValidation = true,
            DryRun = true,
            UseFileSelection = true,
        };

        /// <summary>CLI <c>validate --dry-run-only --use-file-selection</c>.</summary>
        public static ValidationPipelineOptions CliDryRunOnly => new ValidationPipelineOptions
        {
            DryRunOnly = true,
            UseFileSelection = true,
        };
    }
}
