/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022, 2023 Nicholas Hayes
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
using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace GmicEffectPlugin
{
    internal abstract class PlatformFileDialog : Component
    {
        /// <summary>
        /// Shows the folder dialog.
        /// </summary>
        /// <returns>One of the <see cref="DialogResult"/> values.</returns>
        public DialogResult ShowDialog()
        {
            return ShowDialog(null);
        }

        /// <summary>
        /// Shows the folder dialog with the specified owner.
        /// </summary>
        /// <param name="owner">
        /// Any object that implements <see cref="IWin32Window"/> that represents the top-level window that will own the modal dialog box.
        /// </param>
        /// <returns>One of the <see cref="DialogResult"/> values.</returns>
        public DialogResult ShowDialog(IWin32Window owner)
        {
            DialogResult result = DialogResult.Cancel;

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                result = RunDialog(owner);
            }
            else
            {
                Thread staThread = new(delegate (object state)
                {
                    result = RunDialog((IWin32Window)state);
                });
                staThread.SetApartmentState(ApartmentState.STA);

                staThread.Start(owner);
                staThread.Join();
            }

            return result;
        }

        protected abstract DialogResult RunDialog(IWin32Window owner);

        protected static bool VistaDialogSupported()
        {
            try
            {
                // Check that visual styles are enabled and the OS is not in safe mode.
                VisualStyleState state = Application.VisualStyleState;

                if (state == VisualStyleState.ClientAndNonClientAreasEnabled ||
                    state == VisualStyleState.ClientAreaEnabled)
                {
                    return SystemInformation.BootMode == BootMode.Normal;
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (SecurityException)
            {
            }

            return false;
        }
    }
}
