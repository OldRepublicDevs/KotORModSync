using System;
using System.Collections.Generic;

namespace KOTORModSync.Memory
{

    /// <summary>
    /// Base class for token usage in TSLPatcher.
    /// Tokens are placeholders that get replaced with actual values from memory.
    /// </summary>
    public abstract class TokenUsage
    {
        public abstract string Value(PatcherMemory memory);
    }

    /// <summary>
    /// Represents a token that doesn't use memory - just stores a constant value.
    /// </summary>
    public class NoTokenUsage : TokenUsage
    {
        private readonly string _stored;

        public string Stored => _stored;

        public NoTokenUsage(string stored)
        {
            _stored = stored;
        }

        public NoTokenUsage(int stored)
        {
            _stored = stored.ToString();
        }

        public override string Value(PatcherMemory memory)
        {
            return _stored;
        }
    }

    /// <summary>
    /// Represents a token that references a 2DA memory location (2DAMEMORY#).
    /// </summary>
    public class TokenUsage2DA : TokenUsage
    {
        public int TokenId { get; }

        public TokenUsage2DA(int tokenId)
        {
            TokenId = tokenId;
        }

        public override string Value(PatcherMemory memory)
        {
            if (!memory.Memory2DA.TryGetValue(TokenId, out string value))
            {
                throw new KeyNotFoundException($"2DAMEMORY{TokenId} was not defined before use");
            }

            return value;
        }
    }

    /// <summary>
    /// Represents a token that references a TLK memory location (StrRef#).
    /// </summary>
    public class TokenUsageTLK : TokenUsage
    {
        public int TokenId { get; }

        public TokenUsageTLK(int tokenId)
        {
            TokenId = tokenId;
        }

        public override string Value(PatcherMemory memory)
        {
            if (!memory.MemoryStr.TryGetValue(TokenId, out int value))
            {
                throw new KeyNotFoundException($"StrRef{TokenId} was not defined before use");
            }

            return value.ToString();
        }
    }
}

