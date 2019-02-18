﻿/*
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private GmicPipeServer server;
        private bool haveOutputImage;

        internal static readonly string GmicPath = Path.Combine(Path.GetDirectoryName(typeof(GmicEffect).Assembly.Location), "gmic\\gmic_paintdotnet_qt.exe");

        public GmicConfigDialog()
        {
            surface = null;
            workerThread = null;
            server = new GmicPipeServer();
            server.OutputImageChanged += UpdateOutputImage;
            haveOutputImage = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && server != null)
            {
                server.Dispose();
                server = null;
            }

            base.Dispose(disposing);
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
            try
            {
                List<GmicLayer> layers = new List<GmicLayer>
                {
                    new GmicLayer(EffectSourceSurface, false)
                };

                using (Bitmap clipboardImage = ClipboardUtil.GetImage())
                {
                    // Some G'MIC filters require the image to have more than one layer.
                    // Because use Paint.NET does not currently support Effect plug-ins accessing
                    // other layers in the document, allowing the user to place the second layer on
                    // the clipboard is supported as a workaround.

                    if (clipboardImage != null)
                    {
                        layers.Add(new GmicLayer(Surface.CopyFromBitmap(clipboardImage), true));
                    }
                }

                server.AddLayers(layers);

                server.Start();

                string arguments = string.Format(CultureInfo.InvariantCulture, ".PDN {0}", server.FullPipeName);

                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(GmicPath, arguments);

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 3)
                    {
                        ShowErrorMessage(Resources.ImageTooLargeForX86);
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

            BeginInvoke(new Action(GmicThreadFinished));
        }

        private void GmicThreadFinished()
        {
            workerThread.Join();
            workerThread = null;

            DialogResult = haveOutputImage ? DialogResult.OK : DialogResult.Cancel;
            if (DialogResult == DialogResult.OK)
            {
                FinishTokenUpdate();
            }
            Close();
        }

        private void UpdateOutputImage(object sender, EventArgs e)
        {
            Surface output = server.Output;

            if (output != null)
            {
                if (surface == null)
                {
                    surface = new Surface(EffectSourceSurface.Width, EffectSourceSurface.Height);
                }
                else
                {
                    if (output.Width < surface.Width || output.Height < surface.Height)
                    {
                        surface.Clear(ColorBgra.TransparentBlack);
                    }
                }

                surface.CopySurface(output);
                haveOutputImage = true;

                // The DialogResult property is not set here because it would close the dialog
                // and there is no way to tell if the user clicked "Apply" or "Ok".
                // The "Apply" button will show the image on the canvas without closing the G'MIC-Qt dialog.
                if (InvokeRequired)
                {
                    Invoke(new Action(FinishTokenUpdate));
                }
                else
                {
                    FinishTokenUpdate();
                }
            }
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
