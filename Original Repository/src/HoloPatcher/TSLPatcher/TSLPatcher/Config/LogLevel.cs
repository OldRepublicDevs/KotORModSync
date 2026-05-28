namespace KOTORModSync.Config
{

    /// <summary>
    /// Log level configuration for the patcher
    /// Docstrings taken from ChangeEdit docs
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// No feedback at all. The text from "info.rtf" will continue to be displayed during installation
        /// </summary>
        Nothing = 0,

        /// <summary>
        /// Only general progress information will be displayed. Not recommended.
        /// </summary>
        General = 1,

        /// <summary>
        /// General progress information is displayed, along with any serious errors encountered.
        /// </summary>
        Errors = 2,

        /// <summary>
        /// General progress information, serious errors and warnings are displayed. 
        /// This is recommended for the release version of your mod.
        /// </summary>
        Warnings = 3,

        /// <summary>
        /// Full feedback. On top of what is displayed at level 3, it also shows verbose progress
        /// information that may be useful for a Modder to see what is happening. Intended for Debugging.
        /// </summary>
        Full = 4
    }
}

