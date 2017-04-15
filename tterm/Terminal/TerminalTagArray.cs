﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace tterm.Terminal
{
    public struct TerminalTagArray : IEquatable<TerminalTagArray>, IEnumerable<TerminalTag>
    {
        private readonly ImmutableArray<TerminalTag> _tags;
        private readonly int _hash;

        public int Length => _tags.Length;

        public TerminalTagArray(ImmutableArray<TerminalTag> tags)
        {
            _tags = tags;
            _hash = GetHashCode(tags);
        }

        private static int GetHashCode(ImmutableArray<TerminalTag> tags)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < tags.Length; i++)
                {
                    hash = hash * 23 + tags[i].GetHashCode();
                }
                return hash;
            }
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public override bool Equals(object obj)
        {
            return Equals((ImmutableArray<TerminalTag>)obj);
        }

        public bool Equals(TerminalTagArray other)
        {
            if (_hash != other._hash)
            {
                return false;
            }

            var tagsA = _tags;
            var tagsB = other._tags;
            if (!ReferenceEquals(tagsA, tagsB))
            {
                if (tagsA.Length != tagsB.Length)
                {
                    return false;
                }
                for (int i = 0; i < tagsA.Length; i++)
                {
                    if (tagsA[i] != tagsB[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public IEnumerator<TerminalTag> GetEnumerator()
        {
            return ((IEnumerable<TerminalTag>)_tags).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TerminalTag>)_tags).GetEnumerator();
        }

        public static bool operator ==(TerminalTagArray a, TerminalTagArray b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TerminalTagArray a, TerminalTagArray b)
        {
            return !a.Equals(b);
        }
    }
}
