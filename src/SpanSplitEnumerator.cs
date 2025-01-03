/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022, 2023, 2024, 2025 Nicholas Hayes
*
*  pdn-gmic is free software: you can redistribute it and/or modify
*  it under the terms of the GNU General Public License as published by
*  the Free Software Foundation, either version 3 of the License, or
*  (at your option) any later version.
*
*  pdn-gmic is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*
*  You should have received a copy of the GNU General Public License
*  along with this program.  If not, see <http://www.gnu.org/licenses/>.
*
*/

using System;

namespace GmicEffectPlugin
{
    internal ref struct SpanSplitEnumerator<T> where T : IEquatable<T>
    {
        private ReadOnlySpan<T> span;
        private readonly T separator;

        public SpanSplitEnumerator(ReadOnlySpan<T> span, T separator) : this()
        {
            this.span = span;
            this.separator = separator;
        }

        public ReadOnlySpan<T> Current { get; private set; }

        public readonly SpanSplitEnumerator<T> GetEnumerator() => this;

        public bool MoveNext()
        {
            if (span.Length > 0)
            {
                int index = span.IndexOf(separator);

                if (index == -1)
                {
                    // The span does not contain a separator.
                    span = ReadOnlySpan<T>.Empty;
                    Current = span;

                    return true;
                }

                Current = span.Slice(0, index);

                int nextIndex = index + 1;

                span = nextIndex < span.Length ? span.Slice(nextIndex) : ReadOnlySpan<T>.Empty;

                return true;
            }

            return false;
        }
    }
}
