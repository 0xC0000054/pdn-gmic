/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
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
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;

namespace GmicEffectPlugin
{
    internal sealed class GmicLayer : Disposable
    {
        private IBitmapSource<ColorBgra32>? bitmapSource;

        public GmicLayer(IEffectLayerInfo layerInfo)
        {
            ArgumentNullException.ThrowIfNull(layerInfo);

            bitmapSource = layerInfo.GetBitmapBgra32();

            SizeInt32 size = bitmapSource.Size;

            Width = size.Width;
            Height = size.Height;
            Visible = layerInfo.Visible;
        }

        public GmicLayer(IBitmap<ColorBgra32> bitmapSource, bool takeOwnership = false)
        {
            ArgumentNullException.ThrowIfNull(bitmapSource);

            if (takeOwnership)
            {
                this.bitmapSource = bitmapSource;
            }
            else
            {
                this.bitmapSource = bitmapSource.CreateRefT();
            }

            SizeInt32 size = bitmapSource.Size;

            Width = size.Width;
            Height = size.Height;
            Visible = true;
        }

        public RectInt32 Bounds => new(0, 0, Width, Height);

        public IBitmapSource<ColorBgra32> BitmapSource
        {
            get
            {
                VerifyNotDisposed();

                return bitmapSource!;
            }
        }

        public int Width { get; }

        public int Height { get; }

        public bool Visible { get; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref bitmapSource);
            }

            base.Dispose(disposing);
        }

        private void VerifyNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(GmicLayer));
            }
        }
    }
}
