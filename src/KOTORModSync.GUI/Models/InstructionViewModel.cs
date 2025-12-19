// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using JetBrains.Annotations;

using KOTORModSync.Core;

using ModComponent = KOTORModSync.Core.ModComponent;

namespace KOTORModSync.Models
{

    public class InstructionViewModel : INotifyPropertyChanged
    {
        private bool _willExecute;
        private double _opacity;

        public Instruction Instruction { get; }
        public ModComponent ParentComponent { get; }

        public bool WillExecute
        {
            get => _willExecute;
            set
            {
                if (_willExecute == value)
                {
                    return;
                }

                _willExecute = value;
                UpdateVisualState();
                OnPropertyChanged();
            }
        }

        public double Opacity
        {
            get => _opacity;
            set
            {
                if (Math.Abs(_opacity - value) < 0.01)
                {
                    return;
                }

                _opacity = value;
                OnPropertyChanged();
            }
        }

        public string FontWeight => WillExecute ? "SemiBold" : "Normal";

        public List<string> DependencyNames { get; }

        public List<string> RestrictionNames { get; }

        public bool ShowDependencyInfo { get; set; }

        public InstructionViewModel([NotNull] Instruction instruction, [NotNull] ModComponent parentComponent, bool willExecute, bool showDependencyInfo = false)
        {
            Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            ParentComponent = parentComponent ?? throw new ArgumentNullException(nameof(parentComponent));
            _willExecute = willExecute;
            ShowDependencyInfo = showDependencyInfo;

            DependencyNames = InstructionViewModel.ResolveGuidNames(instruction.Dependencies);
            RestrictionNames = InstructionViewModel.ResolveGuidNames(instruction.Restrictions);

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {

            Opacity = WillExecute ? 1.0 : 0.5;
            OnPropertyChanged(nameof(FontWeight));
        }

        private static List<string> ResolveGuidNames(List<Guid> guids)
        {
            var names = new List<string>();
            if (guids is null || guids.Count == 0)
            {
                return names;
            }

            foreach (Guid guid in guids)
            {

                ModComponent component = MainConfig.AllComponents.FirstOrDefault(c => c.Guid == guid);
                if (component != null)
                {
                    names.Add($"[ModComponent] {component.Name}");
                    continue;
                }

                foreach (ModComponent comp in MainConfig.AllComponents)
                {
                    Option option = comp.Options.FirstOrDefault(o => o.Guid == guid);
                    if (option is null)
                    {
                        continue;
                    }

                    names.Add($"[Option] {comp.Name} → {option.Name}");
                    break;
                }
            }

            return names;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
