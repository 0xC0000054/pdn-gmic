/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021 Nicholas Hayes
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
using System;

namespace GmicEffectPlugin
{
    internal sealed class GmicLayer : IDisposable
    {
        private Surface surface;
        private readonly bool ownsSurface;

        public GmicLayer(Surface surface, bool ownsSurface)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.ownsSurface = ownsSurface;
            Width = surface.Width;
            Height = surface.Height;
        }

        public Surface Surface
        {
            get
            {
                VerifyNotDisposed();

                return surface;
            }
        }

        public int Width { get; }

        public int Height { get; }

        public void Dispose()
        {
            if (surface != null && ownsSurface)
            {
                surface.Dispose();
                surface = null;
            }
        }

        private void VerifyNotDisposed()
        {
            if (surface == null)
            {
                throw new ObjectDisposedException(nameof(GmicLayer));
            }
        }
    }
}
