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

using GmicEffectPlugin.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GmicEffectPlugin
{
    /// <summary>
    /// Prompts the user to select a folder using a Vista-style dialog.
    /// </summary>
    /// <seealso cref="CommonDialog"/>
    [DefaultProperty("FileName")]
    [Description("Prompts the user to save a file using a Vista-style dialog.")]
    internal sealed class VistaFileSaveDialog : CommonDialog
    {
        private string fileName;
        private string filter;
        private string title;

        /// <summary>
        /// Initializes a new instance of the <see cref="VistaFolderBrowserDialog"/> class.
        /// </summary>
        public VistaFileSaveDialog()
        {
            Reset();
        }

        public override void Reset()
        {
            fileName = null;
            filter = null;
            FilterIndex = 1;
            title = null;
            AddToRecentDocuments = false;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler HelpRequest
        {
            add
            {
                base.HelpRequest += value;
            }
            remove
            {
                base.HelpRequest -= value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the selected folder is added to the recent documents list.
        /// </summary>
        /// <value>
        /// <c>true</c> if the folder is added to the recent documents; otherwise, <c>false</c>.
        /// </value>
        [Category("Behavior")]
        [Description("Indicates whether the selected folder is added to the recent documents list.")]
        [DefaultValue(false)]
        [Localizable(false)]
        public bool AddToRecentDocuments { get; set; }

        /// <summary>
        /// Gets or sets the file path selected by the user.
        /// </summary>
        /// <value>
        /// The file path selected by the user.
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
        /// Gets or sets the folder browser dialog box title.
        /// </summary>
        /// <remarks>
        /// The string is placed in the title bar of the dialog box, this can be used to provide instructions to the user.
        /// If the title is an empty string, the system uses a default title.
        /// </remarks>
        /// <value>
        /// The folder browser dialog box title. The default value is an empty string ("").
        /// </value>
        [Category("Appearance")]
        [Description("The string to display in the title bar of the dialog box.")]
        [DefaultValue("")]
        [Localizable(true)]
        public string Title
        {
            get => title ?? string.Empty;
            set => title = value;
        }

        private static NativeInterfaces.IFileSaveDialog CreateDialog()
        {
            NativeInterfaces.IFileSaveDialog dialog = new NativeInterfaces.NativeFileSaveDialog();

            // Set a client GUID to allow this dialog to persist its state independently
            // of the standard SaveFileDialog when the AutoUpgradeEnabled property is true.
            Guid folderBrowserGuid = new Guid("C5778DA9-6108-45FA-AA98-1087689B93FC");
            dialog.SetClientGuid(ref folderBrowserGuid);

            return dialog;
        }

        private void SetDialogOptions(NativeInterfaces.IFileSaveDialog dialog)
        {
            NativeEnums.FOS options;
            dialog.GetOptions(out options);

            // The FOS_FORCEFILESYSTEM flag restricts the dialog to selecting files that are located on the file system.
            // This matches the behavior of the classic file save dialog which does not allow virtual items to be selected.
            options |= NativeEnums.FOS.FOS_FORCEFILESYSTEM | NativeEnums.FOS.FOS_DEFAULTNOMINIMODE;

            if (!AddToRecentDocuments)
            {
                options |= NativeEnums.FOS.FOS_DONTADDTORECENT;
            }
            else
            {
                options &= ~NativeEnums.FOS.FOS_DONTADDTORECENT;
            }

            dialog.SetOptions(options);
        }

        private NativeStructs.COMDLG_FILTERSPEC[] GetFilterItems()
        {
            List<NativeStructs.COMDLG_FILTERSPEC> filterItems = new List<NativeStructs.COMDLG_FILTERSPEC>();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                string[] splitItems = filter.Split('|');

                // The split filter string array must contain an even number of items.
                if ((splitItems.Length & 1) == 0)
                {
                    for (int i = 0; i < splitItems.Length; i += 2)
                    {
                        NativeStructs.COMDLG_FILTERSPEC filterSpec = new NativeStructs.COMDLG_FILTERSPEC
                        {
                            pszName = splitItems[i],
                            pszSpec = splitItems[i + 1]
                        };

                        filterItems.Add(filterSpec);
                    }
                }
            }

            return filterItems.ToArray();
        }

        private void OnBeforeShow(NativeInterfaces.IFileSaveDialog dialog)
        {
            SetDialogOptions(dialog);

            if (!string.IsNullOrEmpty(title))
            {
                dialog.SetTitle(title);
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                dialog.SetFileName(fileName);
            }

            NativeStructs.COMDLG_FILTERSPEC[] filterItems = GetFilterItems();

            if (filterItems.Length > 0)
            {
                dialog.SetFileTypes((uint)filterItems.Length, filterItems);
                dialog.SetFileTypeIndex((uint)FilterIndex);
            }
        }

        protected override bool RunDialog(IntPtr hwndOwner)
        {
            if (Application.OleRequired() != ApartmentState.STA)
            {
                throw new ThreadStateException("The calling thread must be STA.");
            }

            bool result = false;

            NativeInterfaces.IFileSaveDialog dialog = null;

            try
            {
                dialog = CreateDialog();

                OnBeforeShow(dialog);

                FileSaveDialogEvents dialogEvents = new FileSaveDialogEvents(this);
                uint eventCookie;
                dialog.Advise(dialogEvents, out eventCookie);
                try
                {
                    result = dialog.Show(hwndOwner) == NativeConstants.S_OK;
                }
                finally
                {
                    dialog.Unadvise(eventCookie);
                    // Prevent the IFileDialogEvents interface from being collected while the dialog is running.
                    GC.KeepAlive(dialogEvents);
                }
            }
            finally
            {
                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }

            return result;
        }

        private bool HandleFileOk(NativeInterfaces.IFileDialog pfd)
        {
            bool result = false;

            NativeInterfaces.IShellItem resultShellItem = null;
            try
            {
                pfd.GetResult(out resultShellItem);

                string path;
                resultShellItem.GetDisplayName(NativeEnums.SIGDN.SIGDN_FILESYSPATH, out path);

                fileName = path;
                result = true;
            }
            finally
            {
                if (resultShellItem != null)
                {
                    Marshal.ReleaseComObject(resultShellItem);
                }
            }

            return result;
        }

        private sealed class FileSaveDialogEvents : NativeInterfaces.IFileDialogEvents
        {
            private readonly VistaFileSaveDialog dialog;

            public FileSaveDialogEvents(VistaFileSaveDialog folderDialog)
            {
                dialog = folderDialog;
            }

            int NativeInterfaces.IFileDialogEvents.OnFileOk(NativeInterfaces.IFileDialog pfd)
            {
                return dialog.HandleFileOk(pfd) ? NativeConstants.S_OK : NativeConstants.S_FALSE;
            }

            int NativeInterfaces.IFileDialogEvents.OnFolderChanging(NativeInterfaces.IFileDialog pfd, NativeInterfaces.IShellItem psiFolder)
            {
                return NativeConstants.S_OK;
            }

            void NativeInterfaces.IFileDialogEvents.OnFolderChange(NativeInterfaces.IFileDialog pfd)
            {
            }

            void NativeInterfaces.IFileDialogEvents.OnSelectionChange(NativeInterfaces.IFileDialog pfd)
            {
            }

            void NativeInterfaces.IFileDialogEvents.OnShareViolation(
                NativeInterfaces.IFileDialog pfd,
                NativeInterfaces.IShellItem psi,
                out NativeEnums.FDE_SHAREVIOLATION_RESPONSE pResponse)
            {
                pResponse = NativeEnums.FDE_SHAREVIOLATION_RESPONSE.FDESVR_DEFAULT;
            }

            void NativeInterfaces.IFileDialogEvents.OnTypeChange(NativeInterfaces.IFileDialog pfd)
            {
            }

            void NativeInterfaces.IFileDialogEvents.OnOverwrite(
                NativeInterfaces.IFileDialog pfd,
                NativeInterfaces.IShellItem psi,
                out NativeEnums.FDE_OVERWRITE_RESPONSE pResponse)
            {
                pResponse = NativeEnums.FDE_OVERWRITE_RESPONSE.FDEOR_DEFAULT;
            }
        }
    }
}
