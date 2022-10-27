/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022 Nicholas Hayes
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
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class GmicEffect : BitmapEffect<GmicConfigToken>
    {
        private bool repeatEffect;
        private IBitmap<ColorBgra32> outputBitmap;

        internal static string StaticName
        {
            get
            {
                return "G'MIC-Qt";
            }
        }

        internal static Bitmap StaticImage
        {
            get
            {
                return new Bitmap(typeof(GmicEffect), PluginIconUtil.GetIconResourceNameForDpi(UIScaleFactor.Current.Dpi));
            }
        }

        public GmicEffect() : base(StaticName, StaticImage, "Advanced", new BitmapEffectOptions { IsConfigurable = true })
        {
            repeatEffect = true;
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref outputBitmap);
            }

            base.OnDispose(disposing);
        }

        protected override EffectConfigForm OnCreateConfigForm()
        {
            repeatEffect = false;

            // The services are passed to the constructor as a parameter because the EffectConfigDialog class
            // may not have its Services property initialized when the constructor runs.
            return new GmicConfigDialog(Services, Environment);
        }

        private void ShowErrorMessage(Exception exception)
        {
            Services.GetService<IExceptionDialogService>().ShowErrorDialog(null, exception.Message, exception);
        }

        private void ShowErrorMessage(string message)
        {
            Services.GetService<IExceptionDialogService>().ShowErrorDialog(null, message, string.Empty);
        }

        private void RunGmicRepeatEffect(string lastOutputFolder)
        {
            if (File.Exists(GmicConfigDialog.GmicPath))
            {
                if (GmicLayerUtil.IsTooLargeForX86<ColorBgra32>(Environment.Document.Size))
                {
                    ShowErrorMessage(Resources.ImageTooLargeForX86);
                    return;
                }

                try
                {
                    using (GmicPipeServer server = new(Services, Environment))
                    {
                        server.Start();

                        string arguments = string.Format(CultureInfo.InvariantCulture, ".PDN {0} reapply", server.FullPipeName);

                        using (Process process = new())
                        {
                            process.StartInfo = new ProcessStartInfo(GmicConfigDialog.GmicPath, arguments);

                            process.Start();
                            process.WaitForExit();

                            if (process.ExitCode == GmicExitCode.Ok)
                            {
                                OutputImageState state = server.OutputImageState;

                                if (state.Error != null)
                                {
                                    ShowErrorMessage(state.Error);
                                }
                                else
                                {
                                    IReadOnlyList<IBitmap<ColorBgra32>> outputImages = state.OutputImages;
                                    string gmicCommandName = server.GmicCommandName;

                                    if (outputImages.Count > 1)
                                    {
                                        using (PlatformFolderBrowserDialog folderBrowserDialog = new())
                                        {
                                            folderBrowserDialog.ClassicFolderBrowserDescription = Resources.ClassicFolderBrowserDescription;
                                            folderBrowserDialog.VistaFolderBrowserTitle = Resources.VistaFolderBrowserTitle;

                                            if (!string.IsNullOrWhiteSpace(lastOutputFolder))
                                            {
                                                folderBrowserDialog.SelectedPath = lastOutputFolder;
                                            }

                                            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                                            {
                                                string outputFolder = folderBrowserDialog.SelectedPath;

                                                OutputImageUtil.SaveAllToFolder(Environment.ImagingFactory,
                                                                                outputImages,
                                                                                outputFolder,
                                                                                gmicCommandName);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        IBitmap<ColorBgra32> output = outputImages[0];

                                        if (output.Size == Environment.Document.Size)
                                        {
                                            outputBitmap ??= Environment.ImagingFactory.CreateBitmap<ColorBgra32>(output.Size);
                                            using (IBitmapLock<ColorBgra32> bitmapLock = outputBitmap.Lock(BitmapLockOptions.Write))
                                            {
                                                output.CopyPixels(bitmapLock);
                                            }
                                        }
                                        else
                                        {
                                            // Place the full image on the clipboard when the size does not match the Paint.NET layer
                                            // and prompt the user to save it.
                                            // The resized image will not be copied to the Paint.NET canvas.
                                            Services.GetService<IClipboardService>().SetImage(output);

                                            using (PlatformFileSaveDialog resizedImageSaveDialog = new())
                                            {
                                                resizedImageSaveDialog.Filter = Resources.ResizedImageSaveDialogFilter;
                                                resizedImageSaveDialog.Title = Resources.ResizedImageSaveDialogTitle;
                                                resizedImageSaveDialog.FileName = gmicCommandName + "_" + DateTime.Now.ToString("yyyyMMdd-THHmmss") + ".png";
                                                if (resizedImageSaveDialog.ShowDialog() == DialogResult.OK)
                                                {
                                                    string resizedImagePath = resizedImageSaveDialog.FileName;

                                                    OutputImageUtil.SaveResizedImage(Environment.ImagingFactory, output, resizedImagePath);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
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
                catch (SecurityException ex)
                {
                    ShowErrorMessage(ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ShowErrorMessage(ex);
                }
            }
            else
            {
                ShowErrorMessage(Resources.GmicNotFound);
            }
        }

        protected override void OnSetToken(GmicConfigToken token)
        {
            if (repeatEffect)
            {
                RunGmicRepeatEffect(token.OutputFolder);
            }
            else
            {
                if (token.OutputBitmap != null)
                {
                    outputBitmap ??= Environment.ImagingFactory.CreateBitmap<ColorBgra32>(token.OutputBitmap.Size);
                    using (IBitmapLock<ColorBgra32> bitmapLock = outputBitmap.Lock(BitmapLockOptions.Write))
                    {
                        token.OutputBitmap.CopyPixels(bitmapLock);
                    }
                }
            }

            base.OnSetToken(token);
        }

        protected override void OnRender(IBitmapEffectOutput bitmapEffectOutput)
        {
            if (outputBitmap != null)
            {
                using (IBitmapLock<ColorBgra32> src = outputBitmap.Lock(bitmapEffectOutput.Bounds, BitmapLockOptions.Read))
                using (IBitmapLock<ColorBgra32> dst = bitmapEffectOutput.LockBgra32())
                {
                    src.AsRegionPtr().CopyTo(dst.AsRegionPtr());
                }
            }
        }
    }
}
