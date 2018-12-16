/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
*
*  Copyright (C) 2018 Nicholas Hayes
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

using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    internal static class ClipboardUtil
    {
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
                    else if (dataObject.GetDataPresent(DataFormats.Bitmap))
                    {
                        // Paint.NET 3.5.X does not place a "PNG" format on the clipboard.
                        image = (Bitmap)dataObject.GetData(typeof(Bitmap));
                    }
                }
            }
            catch
            {
                // Ignore any exceptions thrown when reading the clipboard.
            }

            return image;
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
