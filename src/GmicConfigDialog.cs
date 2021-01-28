/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
*
*  Copyright (C) 2018, 2019, 2020 Nicholas Hayes
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
using PaintDotNet.Clipboard;
using PaintDotNet.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    internal sealed class GmicConfigDialog : EffectConfigDialog
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Code Quality",
            "IDE0069:Disposable fields should be disposed",
            Justification = "InitTokenFromDialog transfers ownership to the effect token.")]
        private Surface surface;
        private Thread workerThread;
        private GmicPipeServer server;
        private string outputFolder;
        private PlatformFolderBrowserDialog folderBrowserDialog;

        private readonly GmicDialogSynchronizationContext dialogSynchronizationContext;

        internal static readonly string GmicPath = Path.Combine(Path.GetDirectoryName(typeof(GmicEffect).Assembly.Location), "gmic\\gmic_paintdotnet_qt.exe");

        public GmicConfigDialog()
        {
            InitializeComponent();
            Text = GmicEffect.StaticName;
            surface = null;
            workerThread = null;
            dialogSynchronizationContext = new GmicDialogSynchronizationContext(this);
            server = new GmicPipeServer(dialogSynchronizationContext);
            server.OutputImageChanged += UpdateOutputImage;
            outputFolder = string.Empty;
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
            GmicConfigToken token = (GmicConfigToken)effectTokenCopy;

            outputFolder = token.OutputFolder;
        }

        protected override void InitTokenFromDialog()
        {
            GmicConfigToken token = (GmicConfigToken)theEffectToken;

            token.OutputFolder = outputFolder;
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
                List<GmicLayer> layers = new List<GmicLayer>
                {
                    new GmicLayer(EnvironmentParameters.SourceSurface, false)
                };

                Surface clipboardSurface = null;
                try
                {
                    // Some G'MIC filters require the image to have more than one layer.
                    // Because use Paint.NET does not currently support Effect plug-ins accessing
                    // other layers in the document, allowing the user to place the second layer on
                    // the clipboard is supported as a workaround.

                    clipboardSurface = Services.GetService<PaintDotNet.AppModel.IClipboardService>().TryGetSurface();

                    if (clipboardSurface != null)
                    {
                        layers.Add(new GmicLayer(clipboardSurface, true));
                        clipboardSurface = null;
                    }
                }
                finally
                {
                    if (clipboardSurface != null)
                    {
                        clipboardSurface.Dispose();
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

                    if (process.ExitCode == GmicExitCode.Ok)
                    {
                        result = DialogResult.OK;
                    }
                    else
                    {
                        surface?.Dispose();
                        surface = null;

                        switch (process.ExitCode)
                        {
                            case GmicExitCode.ImageTooLargeForX86:
                                ShowErrorMessage(Resources.ImageTooLargeForX86);
                                break;
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

            if (result == DialogResult.OK)
            {
                DialogResult = ProcessOutputImages();
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
            Close();
        }

        private DialogResult ProcessOutputImages()
        {
            DialogResult result = DialogResult.Cancel;

            OutputImageState state = server.OutputImageState;

            if (state.Error != null)
            {
                ShowErrorMessage(state.Error.Message);
            }
            else
            {
                IReadOnlyList<Surface> outputImages = state.OutputImages;

                if (outputImages.Count > 1)
                {
                    if (!string.IsNullOrWhiteSpace(outputFolder))
                    {
                        folderBrowserDialog.SelectedPath = outputFolder;
                    }

                    if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        outputFolder = folderBrowserDialog.SelectedPath;

                        try
                        {
                            OutputImageUtil.SaveAllToFolder(outputImages, outputFolder);

                            surface?.Dispose();
                            surface = null;
                            result = DialogResult.OK;
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
                        catch (SecurityException ex)
                        {
                            ShowErrorMessage(ex.Message);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            ShowErrorMessage(ex.Message);
                        }
                    }
                }
                else
                {
                    Surface output = outputImages[0];

                    if (surface == null)
                    {
                        surface = new Surface(EnvironmentParameters.SourceSurface.Width, EnvironmentParameters.SourceSurface.Height);
                    }
                    else
                    {
                        if (output.Width < surface.Width || output.Height < surface.Height)
                        {
                            surface.Clear(ColorBgra.TransparentBlack);
                        }
                    }

                    if (output.Width > surface.Width || output.Height > surface.Height)
                    {
                        // Place the full image on the clipboard if it is larger than the Paint.NET layer.
                        // A cropped version will be copied to the canvas.
                        Services.GetService<PaintDotNet.AppModel.IClipboardService>().SetImage(output);
                    }

                    surface.CopySurface(output);
                    result = DialogResult.OK;
                }

                FinishTokenUpdate();
            }

            return result;
        }

        private void UpdateOutputImage(object sender, EventArgs e)
        {
            GmicPipeServer server = (GmicPipeServer)sender;

            if (surface != null)
            {
                surface.Dispose();
                surface = null;
            }

            OutputImageState state = server.OutputImageState;

            if (state.Error == null)
            {
                IReadOnlyList<Surface> outputImages = state.OutputImages;

                if (outputImages.Count == 1)
                {
                    Surface output = outputImages[0];

                    if (output.Size == EnvironmentParameters.SourceSurface.Size)
                    {
                        surface = output.Clone();

                        // The DialogResult property is not set here because it would close the dialog
                        // and there is no way to tell if the user clicked "Apply" or "Ok".
                        // The "Apply" button will show the image on the canvas without closing the G'MIC-Qt dialog.
                        FinishTokenUpdate();
                    }
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

        private void InitializeComponent()
        {
            this.folderBrowserDialog = new GmicEffectPlugin.PlatformFolderBrowserDialog();
            this.SuspendLayout();
            //
            // folderBrowserDialog
            //
            this.folderBrowserDialog.ClassicFolderBrowserDescription = "Select the output folder.";
            this.folderBrowserDialog.VistaFolderBrowserTitle = "Select Output Folder";
            //
            // GmicConfigDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Location = new System.Drawing.Point(0, 0);
            this.Name = "GmicConfigDialog";
            this.ResumeLayout(false);

        }

        private sealed class GmicDialogSynchronizationContext : SynchronizationContext
        {
            private readonly GmicConfigDialog dialog;

            public GmicDialogSynchronizationContext(GmicConfigDialog dialog)
            {
                this.dialog = dialog;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                dialog?.BeginInvoke(d, state);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                dialog?.Invoke(d, state);
            }
        }
    }
}
