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

namespace GmicEffectPlugin.Interop
{
    internal static class NativeConstants
    {
        internal const int S_OK = 0;
        internal const int S_FALSE = 1;

        internal const string CLSID_FileOpenDialog = "DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7";
        internal const string CLSID_FileSaveDialog = "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B";

        internal const string IID_IModalWindow = "b4db1657-70d7-485e-8e3e-6fcb5a5c1802";
        internal const string IID_IFileDialog = "42f85136-db7e-439c-85f1-e4075d135fc8";
        internal const string IID_IFileOpenDialog = "d57c7288-d4ad-4768-be02-9d969532d960";
        internal const string IID_IFileSaveDialog = "84bccd23-5fde-4cdb-aea4-af64b83d78ab";
        internal const string IID_IFileDialogEvents = "973510DB-7D7F-452B-8975-74A85828D354";
        internal const string IID_IFileDialogControlEvents = "36116642-D713-4b97-9B83-7484A9D00433";
        internal const string IID_IFileDialogCustomize = "8016b7b3-3d49-4504-a0aa-2a37494e606f";
        internal const string IID_IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        internal const string IID_IShellItemArray = "B63EA76D-1F85-456F-A19C-48159EFA858B";
    }
}
