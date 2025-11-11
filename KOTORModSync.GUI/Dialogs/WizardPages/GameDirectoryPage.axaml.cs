// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class GameDirectoryPage : WizardPageBase
    {
        private readonly MainConfig _mainConfig;
        private DirectoryPickerControl _destinationPathPicker;
        private Border _validationFeedback;
        private TextBlock _validationTitle;
        private TextBlock _validationMessage;
        private TextBlock _gameDetailsText;

        private PathUtilities.DetailedGameDetectionSummary _lastDetectionSummary =
            PathUtilities.DetailedGameDetectionSummary.Empty;

        public GameDirectoryPage()
            : this(new MainConfig())
        {
        }

        public GameDirectoryPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            CacheControls();
            HookEvents();
            UpdateValidation();
        }

        public override string Title => "KOTOR Game Directory";
        public override string Subtitle => "Point to your KOTOR installation folder";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_mainConfig.destinationPathFullName))
            {
                _destinationPathPicker?.SetCurrentPath(_mainConfig.destinationPathFullName);
            }

            UpdateValidation();
            return Task.CompletedTask;
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            string destPath = _destinationPathPicker?.GetCurrentPath();
            if (!string.IsNullOrWhiteSpace(destPath) && Directory.Exists(destPath))
            {
                _mainConfig.destinationPath = new DirectoryInfo(destPath);
            }

            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            string destPath = _destinationPathPicker?.GetCurrentPath();

            if (string.IsNullOrWhiteSpace(destPath))
            {
                return Task.FromResult((false, "Please select your Knights of the Old Republic installation folder."));
            }

            if (!Directory.Exists(destPath))
            {
                return Task.FromResult((false, "The selected directory does not exist. Please choose an existing installation folder."));
            }

            PathUtilities.DetailedGameDetectionSummary summary =
                _lastDetectionSummary.Identity == PathUtilities.GameInstallVariant.Unknown
                    ? PathUtilities.AnalyzeGameDirectoryDetailed(destPath)
                    : _lastDetectionSummary;

            _lastDetectionSummary = summary;

            if (summary.BestResult is null
                || summary.Identity == PathUtilities.GameInstallVariant.Unknown
                || summary.BestResult.Matches == 0)
            {
                return Task.FromResult((false, "We could not identify a KOTOR installation in that folder. Make sure it contains swkotor.exe or swkotor2.exe."));
            }

            if (!IsSupportedPlatform(summary.Identity))
            {
                return Task.FromResult((false, "We detected a non-PC build (console/mobile). Please select a Windows PC installation of KOTOR."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _destinationPathPicker = this.FindControl<DirectoryPickerControl>("DestinationPathPicker");
            _validationFeedback = this.FindControl<Border>("ValidationFeedback");
            _validationTitle = this.FindControl<TextBlock>("ValidationTitle");
            _validationMessage = this.FindControl<TextBlock>("ValidationMessage");
            _gameDetailsText = this.FindControl<TextBlock>("GameDetailsText");
        }

        private void HookEvents()
        {
            if (_destinationPathPicker != null)
            {
                _destinationPathPicker.DirectoryChanged += OnDirectoryChanged;
                ToolTip.SetTip(_destinationPathPicker, "Choose the folder that contains swkotor.exe or swkotor2.exe.");
            }
        }

        private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Path))
            {
                UpdateValidation();
                return;
            }

            try
            {
                if (e.PickerType == DirectoryPickerType.KotorDirectory)
                {
                    _mainConfig.destinationPath = new DirectoryInfo(e.Path);
                }
            }
            catch (Exception)
            {
                // DirectoryInfo can throw for invalid paths; ignore and fall back to validation.
            }

            UpdateValidation();
        }

        private void UpdateValidation()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(UpdateValidation);
                return;
            }

            if (_destinationPathPicker is null || _validationFeedback is null)
            {
                return;
            }

            string destPath = _destinationPathPicker.GetCurrentPath();

            if (string.IsNullOrWhiteSpace(destPath))
            {
                _validationFeedback.IsVisible = false;
                if (_gameDetailsText != null)
                {
                    _gameDetailsText.Text = string.Empty;
                }

                _lastDetectionSummary = PathUtilities.DetailedGameDetectionSummary.Empty;
                return;
            }

            _validationFeedback.IsVisible = true;

            if (!Directory.Exists(destPath))
            {
                _validationTitle.Text = "❌ Directory Not Found";
                _validationMessage.Text = "The selected path does not exist. Please choose an existing installation folder.";
                if (_gameDetailsText != null)
                {
                    _gameDetailsText.Text = string.Empty;
                }

                _lastDetectionSummary = PathUtilities.DetailedGameDetectionSummary.Empty;
                return;
            }

            PathUtilities.DetailedGameDetectionSummary summary = PathUtilities.AnalyzeGameDirectoryDetailed(destPath);
            _lastDetectionSummary = summary;

            if (summary.BestResult is null || summary.Identity == PathUtilities.GameInstallVariant.Unknown)
            {
                _validationTitle.Text = "⚠️ Could Not Identify Game";
                _validationMessage.Text = "We found the folder, but none of the known KOTOR signatures matched strongly enough.";
                if (_gameDetailsText != null)
                {
                    _gameDetailsText.Text = BuildDetailsText(summary);
                }

                return;
            }

            if (!IsSupportedPlatform(summary.Identity))
            {
                _validationTitle.Text = "⚠️ Unsupported Game Variant";
                _validationMessage.Text = $"{GetFriendlyName(summary.Identity)} was detected. Please provide a Windows PC installation that includes swkotor.exe or swkotor2.exe.";
                if (_gameDetailsText != null)
                {
                    _gameDetailsText.Text = BuildDetailsText(summary);
                }

                return;
            }

            _validationTitle.Text = "✅ Valid Game Directory";
            _validationMessage.Text = $"Detected {GetFriendlyName(summary.Identity)}. We will configure the installer automatically.";
            if (_gameDetailsText != null)
            {
                _gameDetailsText.Text = BuildDetailsText(summary);
            }

            ApplyDetectedTargetGame(summary.Identity);
        }

        private static string BuildDetailsText(PathUtilities.DetailedGameDetectionSummary summary)
        {
            if (summary.AllResults is null || summary.AllResults.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            if (summary.BestResult != null && summary.Identity != PathUtilities.GameInstallVariant.Unknown)
            {
                sb.Append(GetFriendlyName(summary.Identity)).Append(" — ").Append(summary.BestResult.Matches).Append('/').Append(summary.BestResult.TotalChecks).Append(" signature files (").Append(summary.BestResult.Confidence.ToString("P0", CultureInfo.CurrentCulture)).AppendLine(").");

                if (summary.BestResult.MatchedEvidence.Count > 0)
                {
                    sb.AppendLine("Key matches:");
                    foreach (string evidence in summary.BestResult.MatchedEvidence)
                    {
                        sb.Append(" • ").AppendLine(NormalizeEvidence(evidence));
                    }
                }

                if (summary.BestResult.MissingEvidence.Count > 0)
                {
                    sb.AppendLine("Still missing:");
                    foreach (string missing in summary.BestResult.MissingEvidence.Take(3))
                    {
                        sb.Append(" • ").AppendLine(NormalizeEvidence(missing));
                    }
                }
            }
            else
            {
                sb.AppendLine("No definitive match found. Top candidates:");
            }

            var scoreboard = summary.AllResults
                .Where(r => r.Variant != summary.Identity && r.TotalChecks > 0)
                .OrderByDescending(r => r.Matches)
                .ThenBy(r => r.TotalChecks)
                .Take(3)
                .ToList();

            if (scoreboard.Count > 0)
            {
                sb.AppendLine("Confidence ranking:");
                foreach (PathUtilities.DetailedGameDetectionResult result in scoreboard)
                {
                    sb.Append(" • ")
                        .Append(GetFriendlyName(result.Variant))
                        .Append(" — ")
                        .Append(result.Matches)
                        .Append('/')
                        .Append(result.TotalChecks)
                        .Append(" matches (")
                        .Append(result.Confidence.ToString("P0", CultureInfo.CurrentCulture))
                        .AppendLine(")");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string NormalizeEvidence(string value) =>
            value.Replace('\\', '/');

        private static string GetFriendlyName(PathUtilities.GameInstallVariant identity)
        {
            switch (identity)
            {
                case PathUtilities.GameInstallVariant.PcKotor1:
                    return "Knights of the Old Republic (PC)";
                case PathUtilities.GameInstallVariant.PcKotor2:
                    return "Knights of the Old Republic II (PC)";
                case PathUtilities.GameInstallVariant.PcKotor2Legacy:
                    return "Knights of the Old Republic II (PC, Legacy)";
                case PathUtilities.GameInstallVariant.PcKotor2Aspyr:
                    return "Knights of the Old Republic II (PC, Aspyr)";
                case PathUtilities.GameInstallVariant.XboxKotor1:
                    return "Knights of the Old Republic (Xbox)";
                case PathUtilities.GameInstallVariant.XboxKotor2:
                    return "Knights of the Old Republic II (Xbox)";
                case PathUtilities.GameInstallVariant.IosKotor1:
                    return "Knights of the Old Republic (iOS)";
                case PathUtilities.GameInstallVariant.IosKotor2:
                    return "Knights of the Old Republic II (iOS)";
                case PathUtilities.GameInstallVariant.AndroidKotor1:
                    return "Knights of the Old Republic (Android)";
                case PathUtilities.GameInstallVariant.AndroidKotor2:
                    return "Knights of the Old Republic II (Android)";
                default:
                    return "Unknown Installation";
            }
        }

        private static bool IsSupportedPlatform(PathUtilities.GameInstallVariant identity) =>
            identity == PathUtilities.GameInstallVariant.PcKotor1
            || identity == PathUtilities.GameInstallVariant.PcKotor2
            || identity == PathUtilities.GameInstallVariant.PcKotor2Legacy
            || identity == PathUtilities.GameInstallVariant.PcKotor2Aspyr;

        private void ApplyDetectedTargetGame(PathUtilities.GameInstallVariant identity)
        {
            if (!IsSupportedPlatform(identity))
            {
                return;
            }

            string target = identity == PathUtilities.GameInstallVariant.PcKotor2
                            || identity == PathUtilities.GameInstallVariant.PcKotor2Legacy
                            || identity == PathUtilities.GameInstallVariant.PcKotor2Aspyr
                ? MainConfig.ValidTargetGames.TSL
                : MainConfig.ValidTargetGames.K1;

            if (!string.Equals(_mainConfig.targetGame, target, StringComparison.OrdinalIgnoreCase))
            {
                _mainConfig.targetGame = target;
            }
        }
    }
}
