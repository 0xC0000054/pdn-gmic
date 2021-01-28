﻿/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
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

using PaintDotNet;
using PaintDotNet.Effects;

namespace GmicEffectPlugin
{
    public sealed class GmicConfigToken : EffectConfigToken
    {
        public GmicConfigToken()
        {
            OutputFolder = null;
            Surface = null;
        }

        public string OutputFolder
        {
            get;
            set;
        }

        public Surface Surface
        {
            get;
            set;
        }

        private GmicConfigToken(GmicConfigToken cloneMe)
        {
            OutputFolder = cloneMe.OutputFolder;
            Surface = cloneMe.Surface;
        }

        public override object Clone()
        {
            return new GmicConfigToken(this);
        }
    }
}
