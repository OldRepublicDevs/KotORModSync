



using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KOTORModSync.Models
{
	public class TierFilterItem : INotifyPropertyChanged
	{
		private bool _isSelected;
		private bool _isIncluded; 
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

		
		public bool EffectiveSelection => IsSelected || IsIncluded;

		public string DisplayText => $"{Name} ({Count})";

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}

