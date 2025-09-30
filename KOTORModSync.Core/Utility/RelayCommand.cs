// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Windows.Input;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// A generic command implementation that can be used to bind UI actions to methods.
	/// </summary>
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
