using System;
using System.IO;
using System.Threading;

namespace KOTORModSync.Core.Services.Download
{
	/// <summary>
	/// Wraps a stream and throttles bandwidth to a specified bytes-per-second rate.
	/// Based on the industry-standard approach from CodeProject: https://www.codeproject.com/Articles/18243/Bandwidth-throttling
	/// This provides TRUE bandwidth throttling without exposing Task.Delay to consumers.
	/// </summary>
	public sealed class ThrottledStream : Stream
	{
		private readonly Stream _baseStream;
		private readonly long _maximumBytesPerSecond;
		private long _byteCount;
		private long _start;

		/// <summary>
		/// Creates a new ThrottledStream with the specified maximum bytes per second.
		/// </summary>
		/// <param name="baseStream">The underlying stream to throttle</param>
		/// <param name="maximumBytesPerSecond">Maximum bytes per second (e.g., 700 * 1024 for 700 KB/s)</param>
		public ThrottledStream(Stream baseStream, long maximumBytesPerSecond)
		{
			_baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
			_maximumBytesPerSecond = maximumBytesPerSecond;
			_start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			_byteCount = 0;
		}

		public override bool CanRead => _baseStream.CanRead;
		public override bool CanSeek => _baseStream.CanSeek;
		public override bool CanWrite => _baseStream.CanWrite;
		public override long Length => _baseStream.Length;
		public override long Position
		{
			get => _baseStream.Position;
			set => _baseStream.Position = value;
		}

		public override void Flush()
		{
			_baseStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return _baseStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			_baseStream.SetLength(value);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			// Throttle the read operation
			Throttle(count);

			// Perform the actual read
			int bytesRead = _baseStream.Read(buffer, offset, count);

			// Update byte counter with actual bytes read
			_byteCount += bytesRead;

			return bytesRead;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			// Throttle the write operation
			Throttle(count);

			// Perform the actual write
			_baseStream.Write(buffer, offset, count);

			// Update byte counter
			_byteCount += count;
		}

		/// <summary>
		/// Throttles the stream to maintain the specified bytes-per-second rate.
		/// This is the ONLY place where timing/waiting occurs - hidden from consumers.
		/// </summary>
		private void Throttle(int bufferSizeInBytes)
		{
			if (_maximumBytesPerSecond <= 0)
				return;

			// Calculate how much time should have elapsed for the bytes we've transferred
			long elapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _start;

			if (elapsedMilliseconds > 0)
			{
				// Calculate expected bytes transferred based on time elapsed
				long expectedByteCount = (elapsedMilliseconds * _maximumBytesPerSecond) / 1000;

				// If we're going too fast, sleep to slow down
				if (_byteCount > expectedByteCount)
				{
					// Calculate how long to wait
					long millisToWait = (_byteCount - expectedByteCount) * 1000 / _maximumBytesPerSecond;

					if (millisToWait > 1)
					{
						try
						{
							Thread.Sleep((int)millisToWait);
						}
						catch (ThreadInterruptedException)
						{
							// Ignore interruptions
						}
					}
				}
			}

			// Reset counters every second to prevent overflow
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			if (currentTime - _start > 1000)
			{
				_byteCount = 0;
				_start = currentTime;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_baseStream?.Dispose();
			}
			base.Dispose(disposing);
		}

		public override string ToString()
		{
			return $"ThrottledStream (Max: {_maximumBytesPerSecond / 1024} KB/s)";
		}
	}
}

