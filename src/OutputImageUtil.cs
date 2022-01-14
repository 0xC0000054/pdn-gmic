/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
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

using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace GmicEffectPlugin
{
    internal static class OutputImageUtil
    {
        /// <summary>
        /// Saves all of the G'MIC output images to a folder.
        /// </summary>
        /// <param name="outputImages">The output images.</param>
        /// <param name="outputFolder">The output folder.</param>
        /// <param name="gmicCommandName">The G'MIC command name.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="outputImages"/> is null
        /// or
        /// <paramref name="outputFolder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">The file path is not valid.</exception>
        /// <exception cref="ExternalException">An error occurred when saving the image.</exception>
        /// <exception cref="IOException">An I/O error occurred.</exception>
        /// <exception cref="SecurityException">The caller does not have the required permission.</exception>
        /// <exception cref="UnauthorizedAccessException">The access requested is not permitted by the operating system for the specified path.</exception>
        public static void SaveAllToFolder(IReadOnlyList<Surface> outputImages, string outputFolder, string gmicCommandName)
        {
            if (outputImages is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(outputImages));
            }

            if (outputFolder is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(outputFolder));
            }

            DirectoryInfo directoryInfo = new(outputFolder);

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            string currentTime = DateTime.Now.ToString("yyyyMMdd-THHmmss");

            for (int i = 0; i < outputImages.Count; i++)
            {
                string imageName = string.Format(CultureInfo.InvariantCulture, "{0}_{1}-{2}.png", gmicCommandName, currentTime, i);

                string path = Path.Combine(outputFolder, imageName);

                using (FileStream stream = new(path, FileMode.Create, FileAccess.Write))
                {
                    using (System.Drawing.Bitmap image = outputImages[i].CreateAliasedBitmap())
                    {
                        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
        }
    }
}
