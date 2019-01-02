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

using GmicEffectPlugin.Properties;
using PaintDotNet;
using PaintDotNet.Effects;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    public sealed class GmicConfigDialog : EffectConfigDialog
    {
        private Surface surface;
        private Thread workerThread;

        internal static readonly string GmicPath = Path.Combine(Path.GetDirectoryName(typeof(GmicEffect).Assembly.Location), "gmic\\gmic_paintdotnet_qt.exe");

        public GmicConfigDialog()
        {
            surface = null;
            workerThread = null;
        }

        /// <summary>
        /// Creates a Paint.NET Surface from the G'MIC image.
        /// </summary>
        /// <param name="image">The G'MIC image.</param>
        /// <param name="surfaceSize">The size of the resulting Paint.NET surface.</param>
        /// <returns>The created surface.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
        internal static unsafe Surface CopyFromGmicImage(Bitmap image, Size surfaceSize)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            Surface surface = new Surface(surfaceSize.Width, surfaceSize.Height);

            int imageWidth = Math.Min(image.Width, surfaceSize.Width);
            int imageHeight = Math.Min(image.Height, surfaceSize.Height);

            BitmapData bitmapData = image.LockBits(new Rectangle(0, 0, imageWidth, imageHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte* scan0 = (byte*)bitmapData.Scan0;
                int stride = bitmapData.Stride;
                ulong rowLength = (ulong)imageWidth * ColorBgra.SizeOf;

                for (int y = 0; y < imageHeight; y++)
                {
                    Buffer.MemoryCopy(scan0 + (y * stride), surface.GetRowAddressUnchecked(y), rowLength, rowLength);
                }
            }
            finally
            {
                image.UnlockBits(bitmapData);
            }

            return surface;
        }

        protected override void InitialInitToken()
        {
            theEffectToken = new GmicConfigToken();
        }

        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
            // Not required as the token is only used to send the finished image to Paint.NET.
        }

        protected override void InitTokenFromDialog()
        {
            GmicConfigToken token = (GmicConfigToken)theEffectToken;

            token.Surface = surface;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Opacity = 0;

            if (File.Exists(GmicPath))
            {
                workerThread = new Thread(new ThreadStart(GmicThread)) { IsBackground = true };

                // The thread must use a single-threaded apartment to access the clipboard.
                workerThread.SetApartmentState(ApartmentState.STA);
                workerThread.Start();
            }
            else
            {
                if (ShowErrorMessage(Resources.GmicNotFound) == DialogResult.OK)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            }
        }

        private void GmicThread()
        {
            DialogResult result = DialogResult.Cancel;

            try
            {
                using (TempDirectory tempDir = new TempDirectory())
                {
                    string firstLayerPath = tempDir.GetRandomFileNameWithExtension(".png");

                    using (Bitmap source = EffectSourceSurface.CreateAliasedBitmap())
                    {
                        source.Save(firstLayerPath, ImageFormat.Png);
                    }

                    string secondLayerPath = string.Empty;

                    using (Bitmap clipboardImage = ClipboardUtil.GetImage())
                    {
                        // Some G'MIC filters require the image to have more than one layer.
                        // Because use Paint.NET does not currently support Effect plug-ins accessing
                        // other layers in the document, allowing the user to place the second layer on
                        // the clipboard is supported as a workaround.

                        if (clipboardImage != null && clipboardImage.Width == EffectSourceSurface.Width && clipboardImage.Height == EffectSourceSurface.Height)
                        {
                            secondLayerPath = tempDir.GetRandomFileNameWithExtension(".png");

                            clipboardImage.Save(secondLayerPath, ImageFormat.Png);
                        }
                    }
                    string outputPath = tempDir.GetRandomFileNameWithExtension(".png");

                    string arguments = string.Format(CultureInfo.InvariantCulture, "\"{0}\" \"{1}\" \"{2}\"", firstLayerPath, secondLayerPath, outputPath);

                    using (Process process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo(GmicPath, arguments);

                        process.Start();
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            try
                            {
                                using (Bitmap image = new Bitmap(outputPath))
                                {
                                    surface = CopyFromGmicImage(image, EffectSourceSurface.Size);
                                }
                                result = DialogResult.OK;
                            }
                            catch (ArgumentException)
                            {
                            }
                            catch (FileNotFoundException)
                            {
                            }
                        }
                    }
                }
            }
            catch (ArgumentException ex)
            {
                ShowErrorMessage(ex.Message);
            }
            catch (ExternalException ex)
            {
                ShowErrorMessage(ex.Message);
            }
            catch (IOException ex)
            {
                ShowErrorMessage(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowErrorMessage(ex.Message);
            }

            BeginInvoke(new Action<DialogResult>(GmicThreadFinished), result);
        }

        private void GmicThreadFinished(DialogResult result)
        {
            workerThread.Join();
            workerThread = null;

            DialogResult = result;
            if (result == DialogResult.OK)
            {
                FinishTokenUpdate();
            }
            Close();
        }

        private DialogResult ShowErrorMessage(string message)
        {
            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Action<string>((string error) => MessageBox.Show(error, Text, MessageBoxButtons.OK, MessageBoxIcon.Error)),
                                            message);
            }
            else
            {
                return MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
