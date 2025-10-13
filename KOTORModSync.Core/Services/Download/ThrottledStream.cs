// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;

namespace KOTORModSync.Core.Services.Download
{
	
	
	
	
	
	public sealed class ThrottledStream : Stream
	{
		private readonly Stream _baseStream;
		private readonly long _maximumBytesPerSecond;
		private long _byteCount;
		private long _start;

		
		
		
		
		
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
			
			Throttle(count);

			
			int bytesRead = _baseStream.Read(buffer, offset, count);

			
			_byteCount += bytesRead;

			return bytesRead;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			
			Throttle(count);

			
			_baseStream.Write(buffer, offset, count);

			
			_byteCount += count;
		}

		
		
		
		
		private void Throttle(int bufferSizeInBytes)
		{
			if (_maximumBytesPerSecond <= 0)
				return;

			
			long elapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _start;

			if (elapsedMilliseconds > 0)
			{
				
				long expectedByteCount = (elapsedMilliseconds * _maximumBytesPerSecond) / 1000;

				
				if (_byteCount > expectedByteCount)
				{
					
					long millisToWait = (_byteCount - expectedByteCount) * 1000 / _maximumBytesPerSecond;

					if (millisToWait > 1)
					{
						try
						{
							Thread.Sleep((int)millisToWait);
						}
						catch (ThreadInterruptedException)
						{
							
						}
					}
				}
			}

			
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

