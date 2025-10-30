// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class CanonicalBencodingTests
	{
		[Test]
		public void BencodeCanonical_WithString_ProducesCorrectFormat()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["name"] = "test",
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			// Format: d 4:name 4:test e
			Assert.That(decoded, Is.EqualTo("d4:name4:teste"));
		}

		[Test]
		public void BencodeCanonical_WithInteger_ProducesCorrectFormat()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["number"] = 42L,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			// Format: d 6:number i42e e
			Assert.That(decoded, Is.EqualTo("d6:numberi42ee"));
		}

		[Test]
		public void BencodeCanonical_WithMultipleKeys_SortsAlphabetically()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["z"] = "last",
				["a"] = "first",
				["m"] = "middle",
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			// Keys must appear in alphabetical order
			int aIndex = decoded.IndexOf("1:a", StringComparison.Ordinal);
			int mIndex = decoded.IndexOf("1:m", StringComparison.Ordinal);
			int zIndex = decoded.IndexOf("1:z", StringComparison.Ordinal);

			Assert.That(aIndex, Is.LessThan(mIndex));
			Assert.That(mIndex, Is.LessThan(zIndex));
		}

		[Test]
		public void BencodeCanonical_WithByteArray_EncodesAsString()
		{
			var data = new byte[] { 0x01, 0x02, 0x03 };
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["data"] = data,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);

			// Format: d 4:data 3:<raw bytes> e
			Assert.That(bencode[0], Is.EqualTo((byte)'d'));
			Assert.That(bencode[bencode.Length - 1], Is.EqualTo((byte)'e'));
		}

		[Test]
		public void BencodeCanonical_WithEmptyDict_ProducesValidEncoding()
		{
			var dict = new SortedDictionary<string, object>(StringComparer.Ordinal);

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			Assert.That(decoded, Is.EqualTo("de"));
		}

		[Test]
		public void BencodeCanonical_WithNestedDict_HandlesRecursively()
		{
			var nested = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["inner"] = "value",
			};

			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["outer"] = nested,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			// Format: d 5:outer d 5:inner 5:value e e
			Assert.That(decoded, Does.Contain("5:outer"));
			Assert.That(decoded, Does.Contain("5:inner"));
		}

		[Test]
		public void BencodeCanonical_WithZeroInteger_EncodesCorrectly()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["zero"] = 0L,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			Assert.That(decoded, Does.Contain("i0e"));
		}

		[Test]
		public void BencodeCanonical_WithNegativeInteger_EncodesCorrectly()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["negative"] = -42L,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			Assert.That(decoded, Does.Contain("i-42e"));
		}

		[Test]
		public void BencodeCanonical_ProducesDeterministicOutput()
		{
			var dict1 = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["b"] = "second",
				["a"] = "first",
			};

			var dict2 = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["a"] = "first",
				["b"] = "second",
			};

			byte[] bencode1 = CanonicalBencoding.BencodeCanonical(dict1);
			byte[] bencode2 = CanonicalBencoding.BencodeCanonical(dict2);

			Assert.That(bencode1, Is.EqualTo(bencode2));
		}

		[Test]
		public void BencodeCanonical_WithLargeInteger_HandlesCorrectly()
		{
			var dict = new SortedDictionary<string, object>
(StringComparer.Ordinal)
			{
				["large"] = 9876543210L,
			};

			byte[] bencode = CanonicalBencoding.BencodeCanonical(dict);
			string decoded = Encoding.UTF8.GetString(bencode);

			Assert.That(decoded, Does.Contain("i9876543210e"));
		}
	}
}