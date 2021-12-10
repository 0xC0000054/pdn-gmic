/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
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

using GmicEffectPlugin.Properties;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.Effects;
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
    public sealed class GmicEffect : Effect
    {
        private bool repeatEffect;

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

        public GmicEffect() : base(StaticName, StaticImage, "Advanced", new EffectOptions { Flags = EffectFlags.Configurable })
        {
            repeatEffect = true;
        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            repeatEffect = false;

            return new GmicConfigDialog();
        }

        private void ShowErrorMessage(Exception exception)
        {
            Services.GetService<IExceptionDialogService>().ShowErrorDialog(null, exception.Message, exception);
        }

        private void ShowErrorMessage(string message)
        {
            Services.GetService<IExceptionDialogService>().ShowErrorDialog(null, message, string.Empty);
        }

        protected override void OnSetRenderInfo(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            if (repeatEffect)
            {
                GmicConfigToken token = (GmicConfigToken)parameters;

                if (token.Surface != null)
                {
                    token.Surface.Dispose();
                    token.Surface = null;
                }

                if (File.Exists(GmicConfigDialog.GmicPath))
                {
                    try
                    {
                        using (GmicPipeServer server = new())
                        {
                            List<GmicLayer> layers = new();

                            Surface clipboardSurface = null;
                            try
                            {
                                // Some G'MIC filters require the image to have more than one layer.
                                // Because use Paint.NET does not currently support Effect plug-ins accessing
                                // other layers in the document, allowing the user to place the second layer on
                                // the clipboard is supported as a workaround.

                                clipboardSurface = Services.GetService<IClipboardService>().TryGetSurface();

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

                            layers.Add(new GmicLayer(EnvironmentParameters.SourceSurface, false));

                            server.AddLayers(layers);

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
                                        IReadOnlyList<Surface> outputImages = state.OutputImages;

                                        if (outputImages.Count > 1)
                                        {
                                            using (PlatformFolderBrowserDialog folderBrowserDialog = new())
                                            {
                                                folderBrowserDialog.ClassicFolderBrowserDescription = Resources.ClassicFolderBrowserDescription;
                                                folderBrowserDialog.VistaFolderBrowserTitle = Resources.VistaFolderBrowserTitle;

                                                if (!string.IsNullOrWhiteSpace(token.OutputFolder))
                                                {
                                                    folderBrowserDialog.SelectedPath = token.OutputFolder;
                                                }

                                                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                                                {
                                                    string outputFolder = folderBrowserDialog.SelectedPath;

                                                    try
                                                    {
                                                        OutputImageUtil.SaveAllToFolder(outputImages, outputFolder);
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
                                        else
                                        {
                                            Surface output = outputImages[0];

                                            if (output.Width == srcArgs.Surface.Width && output.Height == srcArgs.Surface.Height)
                                            {
                                                token.Surface = output.Clone();
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
                                                    resizedImageSaveDialog.FileName = DateTime.Now.ToString("yyyyMMdd-THHmmss") + ".png";
                                                    if (resizedImageSaveDialog.ShowDialog() == DialogResult.OK)
                                                    {
                                                        string resizedImagePath = resizedImageSaveDialog.FileName;
                                                        try
                                                        {
                                                            using (Bitmap bitmap = output.CreateAliasedBitmap())
                                                            {
                                                                bitmap.Save(resizedImagePath, System.Drawing.Imaging.ImageFormat.Png);
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
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    switch (process.ExitCode)
                                    {
                                        case GmicExitCode.ImageTooLargeForX86:
                                            ShowErrorMessage(Resources.ImageTooLargeForX86);
                                            break;
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

            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
            GmicConfigToken token = (GmicConfigToken)parameters;

            if (token.Surface != null)
            {
                dstArgs.Surface.CopySurface(token.Surface, rois, startIndex, length);
            }
        }
    }
}
