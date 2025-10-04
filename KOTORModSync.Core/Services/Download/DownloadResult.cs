namespace KOTORModSync.Core.Services.Download
{
	public sealed class DownloadResult
	{
		public bool Success { get; private set; }
		public string Message { get; private set; }
		public string FilePath { get; private set; }
		public bool WasSkipped { get; private set; }

		private DownloadResult(bool success, string message, string filePath, bool wasSkipped = false)
		{
			Success = success;
			Message = message;
			FilePath = filePath;
			WasSkipped = wasSkipped;
		}

		public static DownloadResult Succeeded(string filePath, string message) => new DownloadResult(true, message ?? string.Empty, filePath ?? string.Empty);

		public static DownloadResult Succeeded(string filePath) => new DownloadResult(true, string.Empty, filePath ?? string.Empty);

		public static DownloadResult Failed(string message) => new DownloadResult(false, message ?? string.Empty, string.Empty);

		public static DownloadResult Skipped(string filePath, string message) => new DownloadResult(true, message ?? string.Empty, filePath ?? string.Empty, true);
	}
}
