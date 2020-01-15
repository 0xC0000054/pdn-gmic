/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
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

using System;

namespace GmicEffectPlugin
{
    internal static class PluginIconUtil
    {
        private static readonly Tuple<int, string>[] availableIcons = new Tuple<int, string>[]
        {
            new Tuple<int, string>(96, "icons.wand-96.png"),
            new Tuple<int, string>(144, "icons.wand-144.png"),
            new Tuple<int, string>(192, "icons.wand-192.png"),
            new Tuple<int, string>(384, "icons.wand-384.png")
        };

        internal static string GetIconResourceNameForDpi(int dpi)
        {
            for (int i = 0; i < availableIcons.Length; i++)
            {
                Tuple<int, string> icon = availableIcons[i];
                if (icon.Item1 >= dpi)
                {
                    return icon.Item2;
                }
            }

            return "icons.wand-384.png";
        }
    }
}
