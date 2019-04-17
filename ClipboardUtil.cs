/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
*
*  Copyright (C) 2018, 2019 Nicholas Hayes
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

using GmicEffectPlugin.Interop;
using PaintDotNet.IO;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    internal static class ClipboardUtil
    {
        private static readonly DataFormats.Format DibV5Format = DataFormats.GetFormat(NativeConstants.CF_DIBV5);

        /// <summary>
        /// Gets an image from the clipboard.
        /// </summary>
        /// <returns>The clipboard image, if present; otherwise, null.</returns>
        public static Bitmap GetImage()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return GetImageFromClipboard();
            }
            else
            {
                ClipboardThreadingHelper helper = new ClipboardThreadingHelper();

                Thread thread = new Thread(new ThreadStart(helper.DoWork));
                thread.SetApartmentState(ApartmentState.STA);

                thread.Start();
                thread.Join();

                return helper.Image;
            }
        }

        private static Bitmap GetImageFromClipboard()
        {
            Bitmap image = null;

            try
            {
                IDataObject dataObject = Clipboard.GetDataObject();

                if (dataObject != null)
                {
                    if (dataObject.GetDataPresent("PNG", false))
                    {
                        Stream stream = dataObject.GetData("PNG", false) as Stream;

                        if (stream != null)
                        {
                            image = new Bitmap(stream);
                        }
                    }
                    else if (!TryReadDibV5(dataObject, out image))
                    {
                        if (dataObject.GetDataPresent(DataFormats.Bitmap))
                        {
                            // Some applications do not place a "PNG" format on the clipboard.
                            image = (Bitmap)dataObject.GetData(typeof(Bitmap));
                        }
                    }
                }
            }
            catch
            {
                // Ignore any exceptions thrown when reading the clipboard.
            }

            return image;
        }

        private static bool TryReadDibV5(IDataObject dataObject, out Bitmap image)
        {
            if (dataObject.GetDataPresent(DibV5Format.Name, false))
            {
                Stream stream = dataObject.GetData(DibV5Format.Name, false) as Stream;

                if (stream != null)
                {
                    return TryGetBitmapFromDibV5(stream, out image);
                }
            }

            image = null;
            return false;
        }

        private static unsafe bool TryGetBitmapFromDibV5(Stream stream, out Bitmap image)
        {
            if (stream.Length < NativeStructs.BITMAPV5HEADER.SizeOf)
            {
                image = null;
                return false;
            }

            byte[] headerBytes = new byte[NativeStructs.BITMAPV5HEADER.SizeOf];
            stream.ProperRead(headerBytes, 0, headerBytes.Length);

            NativeStructs.BITMAPV5HEADER header;
            fixed (byte* ptr = headerBytes)
            {
                header = *(NativeStructs.BITMAPV5HEADER*)ptr;
            }

            // Some applications use BI_RGB instead of BI_BITFIELDS when setting the color and alpha masks.
            if (header.bV5BitCount != 32 ||
                (header.bV5Compression != NativeConstants.BI_BITFIELDS && header.bV5Compression != NativeConstants.BI_RGB))
            {
                image = null;
                return false;
            }

            int width = header.bV5Width;
            int height = Math.Abs(header.bV5Height);
            bool topDown = header.bV5Height < 0;
            bool hasAlpha = header.bV5AlphaMask == 0xff000000;

            long srcImageDataSize = (long)width * height * 4;

            const int ExtraDataLength = 12;

            // Some applications (e.g. Chrome) include some extra data before the start of the image data that must be skipped,
            // others (e.g. Firefox) do not include any extra data before the start of the image data.
            if (stream.Length == (header.bV5Size + ExtraDataLength + srcImageDataSize))
            {
                stream.Position += ExtraDataLength;
            }
            else if (stream.Length < (header.bV5Size + srcImageDataSize))
            {
                image = null;
                return false;
            }

            byte[] imageDataBytes = new byte[srcImageDataSize];
            stream.ProperRead(imageDataBytes, 0, imageDataBytes.Length);

            image = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            fixed (byte* pSrcBits = imageDataBytes)
            {
                long srcStride = (long)width * header.bV5BitCount / 8;

                BitmapData dstBitmapData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    byte* dstScan0 = (byte*)dstBitmapData.Scan0;
                    int dstStride = dstBitmapData.Stride;

                    for (int y = 0; y < height; y++)
                    {
                        int srcY = topDown ? y : (height - y - 1);
                        byte* pSrc = pSrcBits + (srcY * srcStride);
                        byte* pDst = dstScan0 + (y * dstStride);

                        for (int x = 0; x < width; x++)
                        {
                            pDst[0] = pSrc[0];
                            pDst[1] = pSrc[1];
                            pDst[2] = pSrc[2];
                            pDst[3] = hasAlpha ? pSrc[3] : (byte)255;

                            pSrc += 4;
                            pDst += 4;
                        }
                    }
                }
                finally
                {
                    image.UnlockBits(dstBitmapData);
                }
            }

            return true;
        }

        private sealed class ClipboardThreadingHelper
        {
            public Bitmap Image
            {
                get;
                private set;
            }

            public void DoWork()
            {
                Image = GetImageFromClipboard();
            }
        }
    }
}
