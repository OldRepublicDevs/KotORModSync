// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KOTORModSync.Models
{
	public class SelectionFilterItem : INotifyPropertyChanged
	{
		private bool _isSelected;
		private string _name;
		private int _count;

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

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if ( _isSelected != value )
				{
					_isSelected = value;
					OnPropertyChanged();
				}
			}
		}

		public string DisplayText => $"{Name} ({Count})";

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}

