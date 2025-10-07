// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KOTORModSync.Models
{
	public class TierFilterItem : INotifyPropertyChanged
	{
		private bool _isSelected;
		private bool _isIncluded; // Included because of higher tier selection
		private string _name;
		private int _count;
		private int _priority;

		public string Name
		{
			get => _name;
			set
			{
				if ( _name != value )
				{
					_name = value;
					OnPropertyChanged();
				}
			}
		}

		public int Count
		{
			get => _count;
			set
			{
				if ( _count != value )
				{
					_count = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(DisplayText));
				}
			}
		}

		public int Priority
		{
			get => _priority;
			set
			{
				if ( _priority != value )
				{
					_priority = value;
					OnPropertyChanged();
				}
			}
		}

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if ( _isSelected != value )
				{
					_isSelected = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(EffectiveSelection));
				}
			}
		}

		public bool IsIncluded
		{
			get => _isIncluded;
			set
			{
				if ( _isIncluded != value )
				{
					_isIncluded = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(EffectiveSelection));
				}
			}
		}

		// Either directly selected or included by hierarchy
		public bool EffectiveSelection => IsSelected || IsIncluded;

		public string DisplayText => $"{Name} ({Count})";

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}

