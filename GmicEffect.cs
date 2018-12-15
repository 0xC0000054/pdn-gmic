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

using GmicEffectPlugin.Properties;
using PaintDotNet;
using PaintDotNet.Effects;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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
                return new Bitmap(typeof(GmicEffect), "wand.png");
            }
        }

        public GmicEffect() : base(StaticName, StaticImage, "Advanced", EffectFlags.Configurable)
        {
            repeatEffect = true;
        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            repeatEffect = false;

            return new GmicConfigDialog();
        }

        private void ShowErrorMessage(string message)
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
                        using (TempDirectory tempDir = new TempDirectory())
                        {
                            string firstLayerPath = tempDir.GetRandomFileNameWithExtension(".png");

                            using (Bitmap source = srcArgs.Surface.CreateAliasedBitmap())
                            {
                                source.Save(firstLayerPath, System.Drawing.Imaging.ImageFormat.Png);
                            }

                            string secondLayerPath = string.Empty;

                            using (Bitmap clipboardImage = ClipboardUtil.GetImage())
                            {
                                // Some G'MIC filters require the image to have more than one layer.
                                // Because use Paint.NET does not currently support Effect plug-ins accessing
                                // other layers in the document, allowing the user to place the second layer on
                                // the clipboard is supported as a workaround.

                                if (clipboardImage != null && clipboardImage.Width == srcArgs.Width && clipboardImage.Height == srcArgs.Height)
                                {
                                    secondLayerPath = tempDir.GetRandomFileNameWithExtension(".png");

                                    clipboardImage.Save(secondLayerPath, System.Drawing.Imaging.ImageFormat.Png);
                                }
                            }

                            string outputPath = tempDir.GetRandomFileNameWithExtension(".png");

                            string arguments = string.Format(CultureInfo.InvariantCulture,
                                                             "\"{0}\" \"{1}\" \"{2}\" reapply",
                                                             firstLayerPath, secondLayerPath, outputPath);

                            using (Process process = new Process())
                            {
                                process.StartInfo = new ProcessStartInfo(GmicConfigDialog.GmicPath, arguments);

                                process.Start();
                                process.WaitForExit();

                                if (process.ExitCode == 0)
                                {
                                    try
                                    {
                                        using (Bitmap image = new Bitmap(outputPath))
                                        {
                                            token.Surface = Surface.CopyFromBitmap(image);
                                        }
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
