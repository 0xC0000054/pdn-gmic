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
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    /// <summary>
    /// Prompts the user to save a file using a dialog appropriate for the current platform.
    /// </summary>
    /// <seealso cref="PlatformFileDialog"/>
    [DefaultProperty("FileName")]
    [Description("Prompts the user to save a file using a dialog appropriate for the current platform.")]
    internal sealed class PlatformFileSaveDialog : PlatformFileDialog
    {
        private SaveFileDialog saveFileDialog;
        private string title;
        private string fileName;
        private string filter;

        private static readonly Guid ClientGuid = new("C5778DA9-6108-45FA-AA98-1087689B93FC");

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformFileSaveDialog"/> class.
        /// </summary>
        public PlatformFileSaveDialog()
        {
            saveFileDialog = new SaveFileDialog
            {
                AddToRecent = false,
                ClientGuid = ClientGuid
            };
            title = null;
            fileName = null;
            filter = null;
            FilterIndex = 1;
        }

        /// <summary>
        /// Gets or sets the file name selected by the user.
        /// </summary>
        /// <value>
        /// The file name path.
        /// </value>
        [Category("Behavior")]
        [Description("The file selected by the user.")]
        [DefaultValue("")]
        [Localizable(false)]
        public string FileName
        {
            get => fileName ?? string.Empty;
            set => fileName = value;
        }

        /// <summary>
        /// Gets or sets the file type filter.
        /// </summary>
        /// <value>
        /// The file type filter.
        /// </value>
        [Category("Behavior")]
        [Description("The file type filter.")]
        [DefaultValue("")]
        [Localizable(false)]
        public string Filter
        {
            get => filter ?? string.Empty;
            set => filter = value;
        }

        /// <summary>
        /// Gets or sets the file type filter index.
        /// </summary>
        /// <value>
        /// The filter index.
        /// </value>
        [Category("Behavior")]
        [Description("The file type filter index.")]
        [DefaultValue(1)]
        [Localizable(false)]
        public int FilterIndex
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the title of the dialog.
        /// </summary>
        /// <value>
        /// The title of the dialog. The default value is an empty string ("")
        /// </value>
        [Category("Appearance")]
        [Description("The title of the dialog.")]
        [DefaultValue("")]
        [Localizable(true)]
        public string Title
        {
            get => title ?? string.Empty;
            set => title = value;
        }

        protected override DialogResult RunDialog(IWin32Window owner)
        {
            saveFileDialog.Title = title;
            saveFileDialog.FileName = fileName;
            saveFileDialog.Filter = filter;
            saveFileDialog.FilterIndex = FilterIndex;

            DialogResult result = saveFileDialog.ShowDialog(owner);

            fileName = saveFileDialog.FileName;

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (saveFileDialog != null)
                {
                    saveFileDialog.Dispose();
                    saveFileDialog = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
