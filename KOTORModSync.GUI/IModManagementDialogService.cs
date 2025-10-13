



using System.Collections.Generic;
using System.Threading.Tasks;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public interface IModManagementDialogService
	{
		
		
		
		Task<string[]> ShowFileDialog(bool isFolderDialog, string windowName, bool allowMultiple = false);

		
		
		
		Task<string> ShowSaveFileDialog(string suggestedFileName);

		
		
		
		Task ShowInformationDialog(string message);

		
		
		
		Task<bool?> ShowConfirmationDialog(string message, string yesButtonText = "Yes", string noButtonText = "No");

		
		
		
		IReadOnlyList<ModComponent> GetComponents();

		
		
		
		void UpdateComponents(List<ModComponent> components);

		
		
		
		void RefreshStatistics();
	}
}
