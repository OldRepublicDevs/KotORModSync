



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

