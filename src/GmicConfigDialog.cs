﻿/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
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

using GmicEffectPlugin.Properties;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
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
    internal sealed class GmicConfigDialog : EffectConfigForm<GmicEffect, GmicConfigToken>
    {
        private IBitmap<ColorBgra32>? outputBitmap;
        private Thread? workerThread;
        private GmicPipeServer? server;
        private string outputFolder;
        private PlatformFolderBrowserDialog folderBrowserDialog;
        private PlatformFileSaveDialog resizedImageSaveDialog;
        private Label infoLabel;
        private readonly IImagingFactory imagingFactory;

        private readonly GmicDialogSynchronizationContext dialogSynchronizationContext;

        internal static readonly string GmicPath = Path.Combine(Path.GetDirectoryName(typeof(GmicEffect).Assembly.Location)!, "gmic\\gmic_paintdotnet_qt.exe");

        // The fields the compiler complains about are initialized in InitializeComponent.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public GmicConfigDialog(IServiceProvider effectServices, IBitmapEffectEnvironment effectEnvironment)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            InitializeComponent();
            Text = GmicEffect.StaticName + " v" + typeof(GmicConfigDialog).Assembly.GetName().Version!.ToString(3);
            outputBitmap = null;
            workerThread = null;
            dialogSynchronizationContext = new GmicDialogSynchronizationContext(this);
            server = new GmicPipeServer(dialogSynchronizationContext, effectServices, effectEnvironment);
            server.OutputImageChanged += UpdateOutputImage;
            outputFolder = string.Empty;
            imagingFactory = effectEnvironment.ImagingFactory;
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref server);
            }

            base.OnDispose(disposing);
        }

        protected override EffectConfigToken OnCreateInitialToken()
        {
            return new GmicConfigToken();
        }

        protected override void OnUpdateDialogFromToken(GmicConfigToken token)
        {
            outputFolder = token.OutputFolder;
        }

        protected override void OnUpdateTokenFromDialog(GmicConfigToken token)
        {
            token.OutputFolder = outputFolder;
            token.OutputBitmap = outputBitmap;
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            if (File.Exists(GmicPath))
            {
                if (GmicLayerUtil.IsTooLargeForX86<ColorBgra32>(Environment.Document.Size))
                {
                    ShowErrorMessage(Resources.ImageTooLargeForX86);
                    return;
                }

                workerThread = new Thread(new ThreadStart(GmicThread)) { IsBackground = true };

                // The thread must use a single-threaded apartment to access the clipboard.
                workerThread.SetApartmentState(ApartmentState.STA);
                workerThread.Start();
            }
            else
            {
                ShowErrorMessage(Resources.GmicNotFound);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void GmicThread()
        {
            DialogResult result = DialogResult.Cancel;

            try
            {
                server!.Start();

                string arguments = string.Format(CultureInfo.InvariantCulture, ".PDN {0}", server.FullPipeName);

                using (Process process = new())
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
                        DisposableUtil.Free(ref outputBitmap);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                ShowErrorMessage(ex);
            }
            catch (ExternalException ex)
            {
                ShowErrorMessage(ex);
            }
            catch (IOException ex)
            {
                ShowErrorMessage(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowErrorMessage(ex);
            }

            BeginInvoke(new Action<DialogResult>(GmicThreadFinished), result);
        }

        private void GmicThreadFinished(DialogResult result)
        {
            workerThread!.Join();
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

            OutputImageState state = server!.OutputImageState!;

            if (state.Error != null)
            {
                ShowErrorMessage(state.Error);
            }
            else
            {
                IReadOnlyList<IBitmap<ColorBgra32>> outputImages = state.OutputImages!;
                string gmicCommandName = server.GmicCommandName;

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
                            OutputImageUtil.SaveAllToFolder(imagingFactory,
                                                            outputImages,
                                                            outputFolder,
                                                            gmicCommandName);

                            DisposableUtil.Free(ref outputBitmap);
                            result = DialogResult.OK;
                        }
                        catch (ArgumentException ex)
                        {
                            ShowErrorMessage(ex);
                        }
                        catch (ExternalException ex)
                        {
                            ShowErrorMessage(ex);
                        }
                        catch (IOException ex)
                        {
                            ShowErrorMessage(ex);
                        }
                        catch (SecurityException ex)
                        {
                            ShowErrorMessage(ex);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            ShowErrorMessage(ex);
                        }
                    }
                }
                else
                {
                    IBitmap<ColorBgra32> output = outputImages[0];

                    if (output.Size == Environment.Document.Size)
                    {
                        outputBitmap ??= imagingFactory.CreateBitmap<ColorBgra32>(Environment.Document.Size);

                        using (IBitmapLock<ColorBgra32> bitmapLock = outputBitmap.Lock(BitmapLockOptions.Write))
                        {
                            output.CopyPixels(bitmapLock);
                        }
                        result = DialogResult.OK;
                    }
                    else
                    {
                        DisposableUtil.Free(ref outputBitmap);

                        // Place the full image on the clipboard when the size does not match the Paint.NET layer
                        // and prompt the user to save it.
                        // The resized image will not be copied to the Paint.NET canvas.
                        Services.GetService<IClipboardService>()!.SetImage(output);

                        resizedImageSaveDialog.FileName = gmicCommandName + "_" + DateTime.Now.ToString("yyyyMMdd-THHmmss") + ".png";
                        if (resizedImageSaveDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            string resizedImagePath = resizedImageSaveDialog.FileName;
                            try
                            {
                                OutputImageUtil.SaveResizedImage(imagingFactory, output, resizedImagePath);

                                result = DialogResult.OK;
                            }
                            catch (ArgumentException ex)
                            {
                                ShowErrorMessage(ex);
                            }
                            catch (ExternalException ex)
                            {
                                ShowErrorMessage(ex);
                            }
                            catch (IOException ex)
                            {
                                ShowErrorMessage(ex);
                            }
                            catch (SecurityException ex)
                            {
                                ShowErrorMessage(ex);
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                ShowErrorMessage(ex);
                            }
                        }
                    }
                }

                UpdateTokenFromDialog();
            }

            return result;
        }

        private void UpdateOutputImage(object? sender, EventArgs e)
        {
            GmicPipeServer server = (GmicPipeServer)sender!;

            DisposableUtil.Free(ref outputBitmap);

            OutputImageState state = server.OutputImageState!;

            if (state.Error == null)
            {
                IReadOnlyList<IBitmap<ColorBgra32>> outputImages = state.OutputImages!;

                if (outputImages.Count == 1)
                {
                    IBitmap<ColorBgra32> output = outputImages[0];

                    if (output.Size == Environment.Document.Size)
                    {
                        outputBitmap = imagingFactory.CreateBitmap<ColorBgra32>(Environment.Document.Size);

                        using (IBitmapLock<ColorBgra32> bitmapLock = outputBitmap.Lock(BitmapLockOptions.Write))
                        {
                            output.CopyPixels(bitmapLock);
                        }
                    }
                }
            }

            // The DialogResult property is not set here because it would close the dialog
            // and there is no way to tell if the user clicked "Apply" or "Ok".
            // The "Apply" button will show the image on the canvas without closing the G'MIC-Qt dialog.
            UpdateTokenFromDialog();
        }

        private void ShowErrorMessage(Exception exception)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Exception>((Exception ex) => Services.GetService<IExceptionDialogService>()!.ShowErrorDialog(this, ex.Message, ex)),
                       exception);
            }
            else
            {
                Services.GetService<IExceptionDialogService>()!.ShowErrorDialog(this, exception.Message, exception);
            }
        }

        private void ShowErrorMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>((string error) => Services.GetService<IExceptionDialogService>()!.ShowErrorDialog(this, error, string.Empty)),
                       message);
            }
            else
            {
                Services.GetService<IExceptionDialogService>()!.ShowErrorDialog(this, message, string.Empty);
            }
        }

        private void InitializeComponent()
        {
            folderBrowserDialog = new GmicEffectPlugin.PlatformFolderBrowserDialog();
            resizedImageSaveDialog = new GmicEffectPlugin.PlatformFileSaveDialog();
            infoLabel = new Label();
            SuspendLayout();
            //
            // folderBrowserDialog
            //
            folderBrowserDialog.ClassicFolderBrowserDescription = Resources.ClassicFolderBrowserDescription;
            folderBrowserDialog.VistaFolderBrowserTitle = Resources.VistaFolderBrowserTitle;
            //
            // resizedImageSaveDialog
            //
            resizedImageSaveDialog.Filter = Resources.ResizedImageSaveDialogFilter;
            resizedImageSaveDialog.Title = Resources.ResizedImageSaveDialogTitle;
            //
            // infoLabel
            //
            infoLabel.Name = "infoLabel";
            infoLabel.Text = Resources.ConfigDialogInfoText;
            //
            // GmicConfigDialog
            //
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(250, 50);
            ControlBox = false;
            Location = new System.Drawing.Point(0, 0);
            Name = "GmicConfigDialog";
            Controls.Add(infoLabel);
            ResumeLayout(false);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            int hMargin = LogicalToDeviceUnits(8);
            int vMargin = LogicalToDeviceUnits(8);

            infoLabel.Location = new System.Drawing.Point(hMargin, vMargin);
            infoLabel.Size = TextRenderer.MeasureText(infoLabel.Text,
                                                      infoLabel.Font,
                                                      new System.Drawing.Size(ClientSize.Width - infoLabel.Left - hMargin, int.MaxValue),
                                                      TextFormatFlags.WordBreak);
            infoLabel.PerformLayout();

            int clientWidth = infoLabel.Right + hMargin;
            int clientHeight = infoLabel.Bottom + vMargin;

            ClientSize = new System.Drawing.Size(clientWidth, clientHeight);

            base.OnLayout(levent);
        }

        private sealed class GmicDialogSynchronizationContext : SynchronizationContext
        {
            private readonly GmicConfigDialog dialog;

            public GmicDialogSynchronizationContext(GmicConfigDialog dialog)
            {
                this.dialog = dialog;
            }

            public override void Post(SendOrPostCallback d, object? state)
            {
                dialog?.BeginInvoke(d, state);
            }

            public override void Send(SendOrPostCallback d, object? state)
            {
                dialog?.Invoke(d, state);
            }
        }
    }
}
