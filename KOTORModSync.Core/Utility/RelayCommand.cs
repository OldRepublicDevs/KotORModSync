



using System;
using System.Windows.Input;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	
	
	
	public class RelayCommand : ICommand
	{
		[CanBeNull] private readonly Func<object, bool> _canExecute;
		[NotNull] private readonly Action<object> _execute;

		public RelayCommand([NotNull] Action<object> execute, [CanBeNull] Func<object, bool> canExecute = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

#pragma warning disable CS0067
		[UsedImplicitly][CanBeNull] public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

		public bool CanExecute([CanBeNull] object parameter) => _canExecute?.Invoke(parameter) == true;
		public void Execute([CanBeNull] object parameter) => _execute(parameter);
	}
}
