/*
*  This file is part of pdn-gmic, an Effect plug-in that
*  integrates G'MIC-Qt into Paint.NET.
*
*  Copyright (C) 2018 Nicholas Hayes
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
using System.IO;

namespace GmicEffectPlugin
{
    internal sealed class TempDirectory : IDisposable
    {
        private readonly string path;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectory"/> class.
        /// </summary>
        public TempDirectory()
        {
            path = CreateTempDirectory();
        }

        private static string CreateTempDirectory()
        {
            string basePath = Path.GetTempPath();

            while (true)
            {
                string tempDirectoryPath = Path.Combine(basePath, Path.GetRandomFileName());

                try
                {
                    Directory.CreateDirectory(tempDirectoryPath);

                    return tempDirectoryPath;
                }
                catch (IOException)
                {
                    // Try again if the directory already exists.
                }
            }
        }

        /// <summary>
        /// Gets a random file name in the directory, with the specified extension.
        /// </summary>
        /// <returns>A random file name in the directory.</returns>
        public string GetRandomFileNameWithExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return Path.Combine(path, Guid.NewGuid().ToString());
            }

            return Path.Combine(path, Guid.NewGuid().ToString() + extension);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                try
                {
                    Directory.Delete(path, true);
                }
                catch (ArgumentException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
