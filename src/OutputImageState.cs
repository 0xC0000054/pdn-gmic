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

using PaintDotNet;
using PaintDotNet.Imaging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GmicEffectPlugin
{
    internal sealed class OutputImageState : Disposable
    {
        private IReadOnlyList<IBitmap<ColorBgra32>>? outputImages;

        public OutputImageState(Exception? error, IReadOnlyList<IBitmap<ColorBgra32>>? outputImages)
        {
            Error = error;
            this.outputImages = outputImages;
        }

        public Exception? Error { get; }

        public IReadOnlyList<IBitmap<ColorBgra32>>? OutputImages
        {
            get
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);

                return outputImages;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IReadOnlyList<IBitmap<ColorBgra32>>? output = Interlocked.Exchange(ref outputImages, null);

                if (output != null)
                {
                    for (int i = 0; i < output.Count; i++)
                    {
                        output[i]?.Dispose();
                    }
                }
            }

            base.Dispose(disposing);
        }
    }
}
