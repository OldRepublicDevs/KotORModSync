// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
	/// <summary>
	/// Wraps an Instruction with additional metadata for UI display and filtering
	/// </summary>
	public class InstructionViewModel : INotifyPropertyChanged
	{
		private bool _willExecute;
		private double _opacity;

		public Instruction Instruction { get; }
		public ModComponent ParentComponent { get; }

		/// <summary>
		/// Whether this instruction will actually execute based on current selections
		/// </summary>
		public bool WillExecute
		{
			get => _willExecute;
			set
			{
				if ( _willExecute == value ) return;
				_willExecute = value;
				UpdateVisualState();
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Visual opacity for the instruction (used in editor mode to de-emphasize skipped instructions)
		/// </summary>
		public double Opacity
		{
			get => _opacity;
			set
			{
				if ( Math.Abs(_opacity - value) < 0.01 ) return;
				_opacity = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Font weight for the instruction text
		/// </summary>
		public string FontWeight => WillExecute ? "SemiBold" : "Normal";

		/// <summary>
		/// Names of components/options this instruction depends on
		/// </summary>
		public List<string> DependencyNames { get; }

		/// <summary>
		/// Names of components/options this instruction is restricted by
		/// </summary>
		public List<string> RestrictionNames { get; }

		/// <summary>
		/// Whether to show dependency/restriction information
		/// </summary>
		public bool ShowDependencyInfo { get; set; }

		public InstructionViewModel([NotNull] Instruction instruction, [NotNull] ModComponent parentComponent, bool willExecute, bool showDependencyInfo = false)
		{
			Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
			ParentComponent = parentComponent ?? throw new ArgumentNullException(nameof(parentComponent));
			_willExecute = willExecute;
			ShowDependencyInfo = showDependencyInfo;

			// Resolve dependency and restriction names
			DependencyNames = InstructionViewModel.ResolveGuidNames(instruction.Dependencies);
			RestrictionNames = InstructionViewModel.ResolveGuidNames(instruction.Restrictions);

			UpdateVisualState();
		}

		private void UpdateVisualState()
		{
			// In editor mode with ShowDependencyInfo, use opacity to de-emphasize non-executing instructions
			Opacity = WillExecute ? 1.0 : 0.5;
			OnPropertyChanged(nameof(FontWeight));
		}

		private static List<string> ResolveGuidNames(List<Guid> guids)
		{
			var names = new List<string>();
			if ( guids == null || guids.Count == 0 )
				return names;

			foreach ( Guid guid in guids )
			{
				// Try to find component
				ModComponent component = MainConfig.AllComponents.FirstOrDefault(c => c.Guid == guid);
				if ( component != null )
				{
					names.Add($"[ModComponent] {component.Name}");
					continue;
				}

				// Try to find option within any component
				foreach ( ModComponent comp in MainConfig.AllComponents )
				{
					Option option = comp.Options.FirstOrDefault(o => o.Guid == guid);
					if ( option == null )
					    continue;
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

