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

        private static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, StaticName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        using (GmicPipeServer server = new GmicPipeServer())
                        {
                            server.OutputImageChanged += (s, e) =>
                            {
                                if (e.Error != null)
                                {
                                    ShowErrorMessage(e.Error.Message);
                                }
                                else
                                {
                                    IReadOnlyList<Surface> outputImages = e.OutputImages;

                                    try
                                    {
                                        if (outputImages.Count > 1)
                                        {
                                            try
                                            {
                                                OutputImageUtil.SaveAllToFolder(outputImages, token.OutputFolder);
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
                                        else
                                        {
                                            Surface output = outputImages[0];

                                            int sourceSurfaceWidth = srcArgs.Surface.Width;
                                            int sourceSurfaceHeight = srcArgs.Surface.Height;

                                            token.Surface = new Surface(sourceSurfaceWidth, sourceSurfaceHeight);

                                            if (output.Width < sourceSurfaceWidth || output.Height < sourceSurfaceHeight)
                                            {
                                                token.Surface.Clear(ColorBgra.TransparentBlack);
                                            }

                                            if (output.Width > sourceSurfaceWidth || output.Height > sourceSurfaceHeight)
                                            {
                                                // Place the full image on the clipboard if it is larger than the Paint.NET layer.
                                                // A cropped version will be copied to the canvas.
                                                Services.GetService<PaintDotNet.AppModel.IClipboardService>().SetImage(output);
                                            }

                                            token.Surface.CopySurface(output);
                                        }
                                    }
                                    finally
                                    {
                                        for (int i = 0; i < outputImages.Count; i++)
                                        {
                                            outputImages[i].Dispose();
                                        }
                                    }
                                }
                            };

                            List<GmicLayer> layers = new List<GmicLayer>
                            {
                                new GmicLayer(srcArgs.Surface, false)
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

                            string arguments = string.Format(CultureInfo.InvariantCulture, ".PDN {0} reapply", server.FullPipeName);

                            using (Process process = new Process())
                            {
                                process.StartInfo = new ProcessStartInfo(GmicConfigDialog.GmicPath, arguments);

                                process.Start();
                                process.WaitForExit();

                                if (process.ExitCode == 3)
                                {
                                    ShowErrorMessage(Resources.ImageTooLargeForX86);
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
