using System;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Interface for displaying registry-related dialogs to the user.
    /// Allows UI layers to provide custom dialog implementations while maintaining
    /// library independence from specific UI frameworks.
    /// </summary>
    public interface IRegistryDialogHandler
    {
        /// <summary>
        /// Shows a dialog with a message and a "don't show again" checkbox.
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        /// <param name="dontShowAgain">Output parameter indicating whether the user checked "don't show again"</param>
        /// <returns>True if the dialog was shown successfully, false otherwise</returns>
        bool ShowDialogWithDontShowAgain(string title, string message, out bool dontShowAgain);
    }
}

