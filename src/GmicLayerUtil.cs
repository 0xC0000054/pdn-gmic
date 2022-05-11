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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GmicEffectPlugin
{
    internal class GmicLayerUtil
    {
        private static readonly Lazy<bool> Is32BitGmic = new(GetIs32BitGmic);

        /// <summary>
        /// Determines whether the specified layers are too large for the 32-bit version of G'MIC-Qt.
        /// </summary>
        /// <param name="layers">The layers.</param>
        /// <returns>
        ///   <see langword="true"/> if specified layers are too large for the 32-bit version of G'MIC-Qt; otherwise, <see langword="false"/>.
        /// </returns>
        internal static bool IsTooLargeForX86(List<GmicLayer> layers)
        {
            if (Is32BitGmic.Value)
            {
                // Check that the layer data length is within the limit for the size_t type on 32-bit builds.
                // This prevents an integer overflow when calculating the total image size if the image is larger than 4GB.

                foreach (GmicLayer layer in layers)
                {
                    if (layer.Surface.Scan0.Length > uint.MaxValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool GetIs32BitGmic()
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(GmicConfigDialog.GmicPath);

            return info.FileDescription.StartsWith("32-bit", StringComparison.OrdinalIgnoreCase);
        }
    }
}
