/*
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

using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    /// <summary>
    /// Prompts the user to select a folder using a dialog appropriate for the current platform.
    /// </summary>
    /// <seealso cref="PlatformFileDialog"/>
    [DefaultProperty("SelectedPath")]
    [Description("Prompts the user to select a folder using a dialog appropriate for the current platform.")]
    internal sealed class PlatformFolderBrowserDialog : PlatformFileDialog
    {
        private VistaFolderBrowserDialog vistaFolderBrowserDialog;
        private FolderBrowserDialog classicFolderBrowserDialog;
        private string classicFolderBrowserDescription;
        private string vistaFolderBrowserTitle;
        private Environment.SpecialFolder rootFolder;
        private string vistaFolderBrowserDefaultFolder;
        private string selectedPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformFolderBrowserDialog"/> class.
        /// </summary>
        public PlatformFolderBrowserDialog()
        {
            vistaFolderBrowserDialog = null;
            classicFolderBrowserDialog = null;
            classicFolderBrowserDescription = null;
            vistaFolderBrowserTitle = null;
            rootFolder = Environment.SpecialFolder.Desktop;
            vistaFolderBrowserDefaultFolder = GetSpecialFolderPath(Environment.SpecialFolder.Desktop);
            selectedPath = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (vistaFolderBrowserDialog != null)
                {
                    vistaFolderBrowserDialog.Dispose();
                    vistaFolderBrowserDialog = null;
                }
                if (classicFolderBrowserDialog != null)
                {
                    classicFolderBrowserDialog.Dispose();
                    classicFolderBrowserDialog = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets or sets the description displayed above the tree view control in the classic folder browser dialog.
        /// </summary>
        /// <value>
        /// The description shown to the user in the classic folder browser dialog. The default is an empty string ("").
        /// </value>
        [Category("Folder Browsing")]
        [Description("The description displayed above the tree view control in the classic folder browser dialog.")]
        [DefaultValue("")]
        [Localizable(true)]
        public string ClassicFolderBrowserDescription
        {
            get => classicFolderBrowserDescription ?? string.Empty;
            set => classicFolderBrowserDescription = value;
        }

        /// <summary>
        /// Gets or sets the title of the Vista-style folder browser dialog.
        /// </summary>
        /// <remarks>
        /// The string is placed in the title bar of the Vista-style folder browser dialog, this can be used to provide instructions to the user.
        /// If the title is an empty string, the system uses a default title.
        /// </remarks>
        /// <value>
        /// The title of the Vista-style folder browser dialog. The default value is an empty string ("")
        /// </value>
        [Category("Folder Browsing")]
        [Description("The title of the Vista-style folder browser dialog.")]
        [DefaultValue("")]
        [Localizable(true)]
        public string VistaFolderBrowserTitle
        {
            get => vistaFolderBrowserTitle ?? string.Empty;
            set => vistaFolderBrowserTitle = value;
        }

        /// <summary>
        /// Gets or sets the root folder that browsing starts from.
        /// The Vista-style dialog uses this property as the default folder when there is not a recently used folder value available.
        /// </summary>
        /// <value>
        /// The root folder that browsing starts from.
        /// </value>
        /// <exception cref="InvalidEnumArgumentException">
        /// The value assigned is not one of the <see cref="Environment.SpecialFolder"/> values.
        /// </exception>
        [Category("Folder Browsing")]
        [Description("The root folder that browsing starts from. The Vista-style dialog defaults to this when there is not a recently used folder.")]
        [DefaultValue(Environment.SpecialFolder.Desktop)]
        [Localizable(false)]
        public Environment.SpecialFolder RootFolder
        {
            get => rootFolder;
            set
            {
                if (!Enum.IsDefined(typeof(Environment.SpecialFolder), value))
                {
                    throw new InvalidEnumArgumentException("value", (int)value, typeof(Environment.SpecialFolder));
                }

                if (rootFolder != value)
                {
                    rootFolder = value;
                    vistaFolderBrowserDefaultFolder = GetSpecialFolderPath(rootFolder);
                }
            }
        }

        /// <summary>
        /// Gets or sets the path selected by the user.
        /// </summary>
        /// <value>
        /// The selected path.
        /// </value>
        [Category("Folder Browsing")]
        [Description("The path selected by the user.")]
        [DefaultValue("")]
        [Localizable(false)]
        public string SelectedPath
        {
            get => selectedPath ?? string.Empty;
            set => selectedPath = value;
        }

        protected override DialogResult RunDialog(IWin32Window owner)
        {
            DialogResult result;

            if (VistaDialogSupported())
            {
                if (vistaFolderBrowserDialog == null)
                {
                    vistaFolderBrowserDialog = new VistaFolderBrowserDialog();
                }
                vistaFolderBrowserDialog.Title = vistaFolderBrowserTitle;
                vistaFolderBrowserDialog.DefaultFolder = vistaFolderBrowserDefaultFolder;
                vistaFolderBrowserDialog.SelectedPath = selectedPath;

                result = vistaFolderBrowserDialog.ShowDialog(owner);

                selectedPath = vistaFolderBrowserDialog.SelectedPath;
            }
            else
            {
                if (classicFolderBrowserDialog == null)
                {
                    classicFolderBrowserDialog = new FolderBrowserDialog();
                }
                classicFolderBrowserDialog.Description = classicFolderBrowserDescription;
                classicFolderBrowserDialog.RootFolder = rootFolder;
                classicFolderBrowserDialog.SelectedPath = selectedPath;

                result = classicFolderBrowserDialog.ShowDialog(owner);

                selectedPath = classicFolderBrowserDialog.SelectedPath;
            }

            return result;
        }

        private static string GetSpecialFolderPath(Environment.SpecialFolder folder)
        {
            string folderPath = string.Empty;

            try
            {
                folderPath = Environment.GetFolderPath(folder);
            }
            catch (ArgumentException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }

            return folderPath;
        }
    }
}
