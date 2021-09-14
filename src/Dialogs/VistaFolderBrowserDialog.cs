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
    [DefaultProperty("SelectedPath")]
    [Description("Prompts the user to select a folder using a Vista-style dialog.")]
    internal sealed class VistaFolderBrowserDialog : CommonDialog
    {
        private string title;
        private string defaultFolder;
        private string selectedPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="VistaFolderBrowserDialog"/> class.
        /// </summary>
        public VistaFolderBrowserDialog()
        {
            Reset();
        }

        public override void Reset()
        {
            title = null;
            defaultFolder = null;
            selectedPath = null;
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

        /// <summary>
        /// Gets or sets the folder that browsing starts from if there is not a recently used folder value available.
        /// </summary>
        /// <value>
        /// The default folder used when there is not a recently used folder value available.
        /// </value>
        [Category("Folder Browsing")]
        [Description("The folder that browsing starts from if there is not a recently used folder value available.")]
        [DefaultValue("")]
        [Localizable(false)]
        public string DefaultFolder
        {
            get => defaultFolder ?? string.Empty;
            set => defaultFolder = value;
        }

        /// <summary>
        /// Gets or sets the path selected by the user.
        /// </summary>
        /// <value>
        /// The path selected by the user.
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

        private static bool CreateShellItemFromPath(string path, out NativeInterfaces.IShellItem item)
        {
            Guid riid = new Guid(NativeConstants.IID_IShellItem);
            if (SafeNativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out item) != NativeConstants.S_OK)
            {
                item = null;
                return false;
            }

            return true;
        }

        private static NativeInterfaces.IFileOpenDialog CreateDialog()
        {
            NativeInterfaces.IFileOpenDialog dialog = new NativeInterfaces.NativeFileOpenDialog();

            // Set a client GUID to allow this dialog to persist its state independently
            // of the standard OpenFileDialog when the AutoUpgradeEnabled property is true.
            Guid folderBrowserGuid = new Guid("A6FDAC55-B3B6-43EB-A94E-BA44593686E6");
            dialog.SetClientGuid(ref folderBrowserGuid);

            return dialog;
        }

        private void SetDialogOptions(NativeInterfaces.IFileOpenDialog dialog)
        {
            NativeEnums.FOS options;
            dialog.GetOptions(out options);

            // The FOS_FORCEFILESYSTEM flag restricts the dialog to selecting folders that are located on the file system.
            // This matches the behavior of the classic folder browser dialog which does not allow virtual folders to be selected.
            options |= NativeEnums.FOS.FOS_PICKFOLDERS | NativeEnums.FOS.FOS_FORCEFILESYSTEM;

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

        private void OnBeforeShow(NativeInterfaces.IFileOpenDialog dialog)
        {
            SetDialogOptions(dialog);

            if (!string.IsNullOrEmpty(title))
            {
                dialog.SetTitle(title);
            }

            if (!string.IsNullOrEmpty(defaultFolder))
            {
                NativeInterfaces.IShellItem defaultFolderShellItem = null;

                try
                {
                    if (CreateShellItemFromPath(defaultFolder, out defaultFolderShellItem))
                    {
                        dialog.SetDefaultFolder(defaultFolderShellItem);
                    }
                }
                finally
                {
                    if (defaultFolderShellItem != null)
                    {
                        Marshal.ReleaseComObject(defaultFolderShellItem);
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedPath))
            {
                dialog.SetFileName(selectedPath);
            }
        }

        protected override bool RunDialog(IntPtr hwndOwner)
        {
            if (Application.OleRequired() != ApartmentState.STA)
            {
                throw new ThreadStateException("The calling thread must be STA.");
            }

            bool result = false;

            NativeInterfaces.IFileOpenDialog dialog = null;

            try
            {
                dialog = CreateDialog();

                OnBeforeShow(dialog);

                FolderBrowserDialogEvents dialogEvents = new FolderBrowserDialogEvents(this);
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

                selectedPath = path;
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

        private sealed class FolderBrowserDialogEvents : NativeInterfaces.IFileDialogEvents
        {
            private readonly VistaFolderBrowserDialog dialog;

            public FolderBrowserDialogEvents(VistaFolderBrowserDialog folderDialog)
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
