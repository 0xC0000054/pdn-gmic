/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
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
using System.Runtime.InteropServices;

namespace GmicEffectPlugin.Interop
{
    internal static class NativeInterfaces
    {
        [ComImport(), Guid(NativeConstants.IID_IModalWindow), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IModalWindow
        {
            [PreserveSig]
            int Show([In] IntPtr parent);
        }

        [ComImport(), Guid(NativeConstants.IID_IFileDialog), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialog : IModalWindow
        {
            // Defined on IModalWindow - repeated here due to requirements of COM interop layer
            // --------------------------------------------------------------------------------
            [PreserveSig]
            new int Show([In] IntPtr parent);

            // IFileDialog-Specific interface members
            // --------------------------------------------------------------------------------

            void SetFileTypes(
                [In] uint cFileTypes,
                [In, MarshalAs(UnmanagedType.LPArray)] NativeStructs.COMDLG_FILTERSPEC[] rgFilterSpec
                );

            void SetFileTypeIndex([In] uint iFileType);

            void GetFileTypeIndex(out uint piFileType);

            void Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);

            void Unadvise([In] uint dwCookie);

            void SetOptions([In] NativeEnums.FOS fos);

            void GetOptions(out NativeEnums.FOS pfos);

            void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, NativeEnums.FDAP fdap);

            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

            void Close([MarshalAs(UnmanagedType.Error)] int hr);

            void SetClientGuid([In] ref Guid guid);

            void ClearClientData();

            // Not supported:  IShellItemFilter is not defined, converting to IntPtr

            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
        }

        [ComImport(), Guid(NativeConstants.IID_IFileOpenDialog), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileOpenDialog : IFileDialog
        {
            // Defined on IModalWindow - repeated here due to requirements of COM interop layer
            // --------------------------------------------------------------------------------
            [PreserveSig]
            new int Show([In] IntPtr parent);

            // Defined on IFileDialog - repeated here due to requirements of COM interop layer

            new void SetFileTypes(
                [In] uint cFileTypes,
                [In, MarshalAs(UnmanagedType.LPArray)] NativeStructs.COMDLG_FILTERSPEC[] rgFilterSpec
                );

            new void SetFileTypeIndex([In] uint iFileType);

            new void GetFileTypeIndex(out uint piFileType);

            new void Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);

            new void Unadvise([In] uint dwCookie);

            new void SetOptions([In] NativeEnums.FOS fos);

            new void GetOptions(out NativeEnums.FOS pfos);

            new void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            new void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            new void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            new void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            new void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

            new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

            new void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

            new void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            new void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            new void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            new void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, NativeEnums.FDAP fdap);

            new void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

            new void Close([MarshalAs(UnmanagedType.Error)] int hr);

            new void SetClientGuid([In] ref Guid guid);

            new void ClearClientData();

            // Not supported:  IShellItemFilter is not defined, converting to IntPtr

            new void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);

            // Defined by IFileOpenDialog
            // ---------------------------------------------------------------------------------

            void GetResults([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppenum);

            void GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppsai);
        }

        [ComImport(), Guid(NativeConstants.IID_IFileDialogEvents), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialogEvents
        {
            // NOTE: some of these callbacks are cancelable - returning S_FALSE means that
            // the dialog should not proceed (e.g. with closing, changing folder); to
            // support this, we need to use the PreserveSig attribute to enable us to return
            // the proper HRESULT
            [PreserveSig]
            int OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            [PreserveSig]
            int OnFolderChanging(
                [In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
                [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder
                );

            void OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnShareViolation(
                [In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
                [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
                out NativeEnums.FDE_SHAREVIOLATION_RESPONSE pResponse
                );

            void OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnOverwrite(
                [In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
                [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
                out NativeEnums.FDE_OVERWRITE_RESPONSE pResponse
                );
        }

        [ComImport(), Guid(NativeConstants.IID_IFileDialogCustomize), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialogCustomize
        {
            void EnableOpenDropDown([In] uint dwIDCtl);

            void AddMenu([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void AddPushButton([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void AddComboBox([In] uint dwIDCtl);

            void AddRadioButtonList([In] uint dwIDCtl);

            void AddCheckButton([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel, [In] bool bChecked);

            void AddEditBox([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void AddSeparator([In] uint dwIDCtl);

            void AddText([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void SetControlLabel([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void GetControlState([In] uint dwIDCtl, out NativeEnums.CDCONTROLSTATE pdwState);

            void SetControlState([In] uint dwIDCtl, [In] NativeEnums.CDCONTROLSTATE dwState);

            void GetEditBoxText([In] uint dwIDCtl, out IntPtr ppszText);

            void SetEditBoxText([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void GetCheckButtonState([In] uint dwIDCtl, out bool pbChecked);

            void SetCheckButtonState([In] uint dwIDCtl, [In] bool bChecked);

            void AddControlItem([In] uint dwIDCtl, [In] uint dwIDItem, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void RemoveControlItem([In] uint dwIDCtl, [In] uint dwIDItem);

            void RemoveAllControlItems([In] uint dwIDCtl);

            void GetControlItemState([In] uint dwIDCtl, [In] uint dwIDItem, out NativeEnums.CDCONTROLSTATE pdwState);

            void SetControlItemState([In] uint dwIDCtl, [In] uint dwIDItem, [In] NativeEnums.CDCONTROLSTATE dwState);

            void GetSelectedControlItem([In] uint dwIDCtl, out int pdwIDItem);

            void SetSelectedControlItem([In] uint dwIDCtl, [In] uint dwIDItem); // Not valid for OpenDropDown

            void StartVisualGroup([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void EndVisualGroup();

            void MakeProminent([In] uint dwIDCtl);
        }

        [ComImport(), Guid(NativeConstants.IID_IFileDialogControlEvents), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialogControlEvents
        {
            void OnItemSelected([In, MarshalAs(UnmanagedType.Interface)] IFileDialogCustomize pfdc, [In] uint dwIDCtl, [In] uint dwIDItem);

            void OnButtonClicked([In, MarshalAs(UnmanagedType.Interface)] IFileDialogCustomize pfdc, [In] uint dwIDCtl);

            void OnCheckButtonToggled([In, MarshalAs(UnmanagedType.Interface)] IFileDialogCustomize pfdc, [In] uint dwIDCtl, [In] bool bChecked);

            void OnControlActivating([In, MarshalAs(UnmanagedType.Interface)] IFileDialogCustomize pfdc, [In] uint dwIDCtl);
        }

        [ComImport(), Guid(NativeConstants.IID_IShellItem), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItem
        {
            // Not supported: IBindCtx

            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void GetDisplayName([In] NativeEnums.SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszName);

            void GetAttributes([In] NativeEnums.SFGAO sfgaoMask, out NativeEnums.SFGAO psfgaoAttribs);

            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }

        [ComImport(), Guid(NativeConstants.IID_IShellItemArray), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemArray
        {
            // Not supported: IBindCtx

            void BindToHandler([In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc, [In] ref Guid rbhid, [In] ref Guid riid, out IntPtr ppvOut);

            void GetPropertyStore([In] int Flags, [In] ref Guid riid, out IntPtr ppv);

            void GetPropertyDescriptionList([In] ref NativeStructs.PROPERTYKEY keyType, [In] ref Guid riid, out IntPtr ppv);

            void GetAttributes([In] NativeEnums.SIATTRIBFLAGS dwAttribFlags, [In] NativeEnums.SFGAO sfgaoMask, out NativeEnums.SFGAO psfgaoAttribs);

            void GetCount(out uint pdwNumItems);

            void GetItemAt([In] uint dwIndex, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            // Not supported: IEnumShellItems (will use GetCount and GetItemAt instead)

            void EnumItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenumShellItems);
        }

        // ---------------------------------------------------------
        // Coclass interfaces - designed to "look like" the object
        // in the API, so that the 'new' operator can be used in a
        // straightforward way. Behind the scenes, the C# compiler
        // morphs all 'new CoClass()' calls to 'new CoClassWrapper()'

        [ComImport]
        [Guid(NativeConstants.IID_IFileOpenDialog)]
        [CoClass(typeof(FileOpenDialogRCW))]
#pragma warning disable IDE1006 // Naming Styles
        internal interface NativeFileOpenDialog : IFileOpenDialog
#pragma warning restore IDE1006 // Naming Styles
        {
        }

        // ---------------------------------------------------
        // .NET classes representing runtime callable wrappers

        [ComImport]
        [ClassInterface(ClassInterfaceType.None)]
        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [Guid(NativeConstants.CLSID_FileOpenDialog)]
        internal class FileOpenDialogRCW
        {
        }
    }
}
