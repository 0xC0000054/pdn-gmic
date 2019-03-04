/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019 Nicholas Hayes
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
using PaintDotNet.IO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;

namespace GmicEffectPlugin
{
    internal sealed class GmicPipeServer : IDisposable
    {
        private List<GmicLayer> layers;
        private List<MemoryMappedFile> memoryMappedFiles;
        private Surface output;
        private NamedPipeServerStream server;
        private bool disposed;

        private readonly string pipeName;
        private readonly string fullPipeName;

        private static readonly RectangleF WholeImageCropRect = new RectangleF(0.0f, 0.0f, 1.0f, 1.0f);

        /// <summary>
        /// Initializes a new instance of the <see cref="GmicPipeServer"/> class.
        /// </summary>
        public GmicPipeServer()
        {
            pipeName = "PDN_GMIC" + Guid.NewGuid().ToString();
            fullPipeName = @"\\.\pipe\" + pipeName;
            layers = new List<GmicLayer>();
            memoryMappedFiles = new List<MemoryMappedFile>();
            output = null;
            disposed = false;
        }

        public string FullPipeName => fullPipeName;

        public Surface Output => output;

        public event EventHandler OutputImageChanged;

        private enum InputMode
        {
            NoInput = 0,
            ActiveLayer,
            AllLayers,
            ActiveAndBelow,
            ActiveAndAbove,
            AllVisibleLayers,
            AllHiddenLayers,
            AllVisibleLayersDescending,
            AllHiddenLayersDescending
        }

        private enum OutputMode
        {
            InPlace = 0,
            NewLayers,
            NewActiveLayers,
            NewImage
        }

        /// <summary>
        /// Adds the layers.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
        public void AddLayers(IEnumerable<GmicLayer> collection)
        {
            VerifyNotDisposed();

            layers.AddRange(collection);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                for (int i = 0; i < layers.Count; i++)
                {
                    layers[i].Dispose();
                }

                for (int i = 0; i < memoryMappedFiles.Count; i++)
                {
                    memoryMappedFiles[i].Dispose();
                }

                if (output != null)
                {
                    output.Dispose();
                    output = null;
                }

                if (server != null)
                {
                    server.Dispose();
                    server = null;
                }
            }
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <exception cref="InvalidOperationException">Must call AddLayers with at least one layer before calling Start.</exception>
        /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
        public void Start()
        {
            VerifyNotDisposed();
            if (layers.Count == 0)
            {
                throw new InvalidOperationException("Must call AddLayers with at least one layer before calling Start.");
            }

            server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            server.BeginWaitForConnection(WaitForConnectionCallback, null);
        }

        private void WaitForConnectionCallback(IAsyncResult result)
        {
            if (server == null)
            {
                return;
            }

            try
            {
                server.EndWaitForConnection(result);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            byte[] replySizeBuffer = new byte[sizeof(int)];
            server.ProperRead(replySizeBuffer, 0, replySizeBuffer.Length);

            int messageLength = BitConverter.ToInt32(replySizeBuffer, 0);

            byte[] messageBytes = new byte[messageLength];

            server.ProperRead(messageBytes, 0, messageLength);

            List<string> parameters = DecodeMessageBuffer(messageBytes);

            if (!TryGetValue(parameters[0], "command=", out string command))
            {
                throw new InvalidOperationException("The first item must be a command.");
            }

            if (command.Equals("gmic_qt_get_max_layer_size", StringComparison.Ordinal))
            {
                if (!TryGetValue(parameters[1], "mode=", out string mode))
                {
                    throw new InvalidOperationException("The second item must be the input mode.");
                }

                InputMode inputMode = ParseInputMode(mode);

#if DEBUG
                System.Diagnostics.Debug.WriteLine("'gmic_qt_get_max_layer_size' received. mode=" + inputMode.ToString());
#endif
                string reply = GetMaxLayerSize(inputMode);

                SendMessage(server, reply);
            }
            else if (command.Equals("gmic_qt_get_cropped_images", StringComparison.Ordinal))
            {
                if (!TryGetValue(parameters[1], "mode=", out string mode))
                {
                    throw new InvalidOperationException("The second item must be the input mode.");
                }

                if (!TryGetValue(parameters[2], "croprect=", out string packedCropRect))
                {
                    throw new InvalidOperationException("The third item must be the crop rectangle.");
                }

                InputMode inputMode = ParseInputMode(mode);
                RectangleF cropRect = GetCropRectangle(packedCropRect);

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "'gmic_qt_get_cropped_images' received. mode={0}, cropRect={1}",
                                                                 inputMode.ToString(), cropRect.ToString()));
#endif
                string reply = PrepareCroppedLayers(inputMode, cropRect);

                SendMessage(server, reply);
            }
            else if (command.Equals("gmic_qt_output_images", StringComparison.Ordinal))
            {
                if (!TryGetValue(parameters[1], "mode=", out string mode))
                {
                    throw new InvalidOperationException("The second item must be the output mode.");
                }

                OutputMode outputMode = ParseOutputMode(mode);

#if DEBUG
                System.Diagnostics.Debug.WriteLine("'gmic_qt_output_images' received. mode=" + outputMode.ToString());
#endif

                List<string> outputLayers = parameters.GetRange(2, parameters.Count - 2);

                ProcessOutputImage(outputLayers, outputMode);
                SendMessage(server, "done");
            }
            else if (command.Equals("gmic_qt_release_shared_memory", StringComparison.Ordinal))
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("'gmic_qt_release_shared_memory' received.");
#endif

                for (int i = 0; i < memoryMappedFiles.Count; i++)
                {
                    memoryMappedFiles[i].Dispose();
                }
                memoryMappedFiles.Clear();

                SendMessage(server, "done");
            }
            else if (command.Equals("gmic_qt_get_max_layer_data_length", StringComparison.Ordinal))
            {
                // This command is used to prevent images larger than 4GB from being used on a 32-bit version of G'MIC.
                // Attempting to map an image that size into memory would cause an integer overflow when casting a 64-bit
                // integer to the unsigned 32-bit size_t type.
                long maxDataLength = 0;

                foreach (GmicLayer layer in layers)
                {
                    maxDataLength = Math.Max(maxDataLength, layer.Surface.Scan0.Length);
                }

                server.Write(BitConverter.GetBytes(sizeof(long)), 0, 4);
                server.Write(BitConverter.GetBytes(maxDataLength), 0, 8);
            }

            // Wait for the acknowledgment that the client is done reading.
            if (server.IsConnected)
            {
                byte[] doneMessageBuffer = new byte[4];
                int bytesRead = 0;
                int bytesToRead = doneMessageBuffer.Length;

                do
                {
                    int n = server.Read(doneMessageBuffer, bytesRead, bytesToRead);

                    bytesRead += n;
                    bytesToRead -= n;

                } while (bytesToRead > 0 && server.IsConnected);
            }

            // Start a new server and wait for the next connection.
            server.Dispose();
            server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            server.BeginWaitForConnection(WaitForConnectionCallback, null);
        }

        private static List<string> DecodeMessageBuffer(byte[] bytes)
        {
            const byte Separator = (byte)'\n';

            int startOffset = 0;
            int count = 0;

            List<string> messageParameters = new List<string>();

            if (bytes[bytes.Length - 1] == Separator)
            {
                // A message with multiple values uses \n as the separator and terminator.
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == Separator)
                    {
                        // Empty strings are skipped.
                        if (count > 0)
                        {
                            messageParameters.Add(Encoding.UTF8.GetString(bytes, startOffset, count));
                        }
                        startOffset = i + 1;
                        count = 0;
                    }
                    else
                    {
                        count++;
                    }
                }
            }
            else
            {
                messageParameters.Add(Encoding.UTF8.GetString(bytes));
            }

            return messageParameters;
        }

        private static InputMode ParseInputMode(string item)
        {
            if (Enum.TryParse(item, out InputMode temp))
            {
                if (temp >= InputMode.NoInput && temp <= InputMode.AllHiddenLayersDescending)
                {
                   return temp;
                }
            }

            return InputMode.ActiveLayer;
        }

        private static OutputMode ParseOutputMode(string item)
        {
            if (Enum.TryParse(item, out OutputMode temp))
            {
                if (temp >= OutputMode.InPlace && temp <= OutputMode.NewImage)
                {
                    return temp;
                }
            }

            return OutputMode.InPlace;
        }

        private static RectangleF GetCropRectangle(string packedCropRect)
        {
            string[] cropCoords = packedCropRect.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (cropCoords.Length != 4)
            {
                throw new InvalidOperationException("A crop rectangle message argument must have 4 values.");
            }

            float x = float.Parse(cropCoords[0], CultureInfo.InvariantCulture);
            float y = float.Parse(cropCoords[1], CultureInfo.InvariantCulture);
            float width = float.Parse(cropCoords[2], CultureInfo.InvariantCulture);
            float height = float.Parse(cropCoords[3], CultureInfo.InvariantCulture);

            return new RectangleF(x, y, width, height);
        }

        private string GetMaxLayerSize(InputMode inputMode)
        {
            int width = 0;
            int height = 0;

            switch (inputMode)
            {
                case InputMode.NoInput:
                    break;
                case InputMode.ActiveLayer:
                case InputMode.ActiveAndAbove:
                    // The first layer in the list is always the layer the user has selected in Paint.NET,
                    // so it will be treated as the active layer.
                    // The clipboard layer (if present) will be placed below the active layer.
                    GmicLayer activeLayer = layers[0];

                    width = activeLayer.Width;
                    height = activeLayer.Height;
                    break;

                case InputMode.AllLayers:
                case InputMode.ActiveAndBelow:
                case InputMode.AllVisibleLayers:
                case InputMode.AllHiddenLayers:
                case InputMode.AllVisibleLayersDescending:
                case InputMode.AllHiddenLayersDescending:
                    foreach (GmicLayer layer in layers)
                    {
                        width = Math.Max(width, layer.Width);
                        height = Math.Max(height, layer.Height);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported InputMode: " + inputMode.ToString());
            }

            return width.ToString(CultureInfo.InvariantCulture) + "," + height.ToString(CultureInfo.InvariantCulture);
        }

        private IReadOnlyList<GmicLayer> GetRequestedLayers(InputMode mode)
        {
            if (mode == InputMode.ActiveLayer ||
                mode == InputMode.ActiveAndAbove)
            {
                // The first layer in the list is always the layer the user has selected in Paint.NET,
                // so it will be treated as the active layer.
                // The clipboard layer (if present) will be placed below the active layer.

                return new GmicLayer[] { layers[0] };
            }
            else if (mode == InputMode.AllHiddenLayersDescending ||
                     mode == InputMode.AllVisibleLayersDescending)
            {
                List<GmicLayer> reversed = new List<GmicLayer>(layers.Count);

                for (int i = layers.Count - 1; i >= 0; i--)
                {
                    reversed.Add(layers[i]);
                }

                return reversed;
            }
            else
            {
                switch (mode)
                {
                    case InputMode.AllLayers:
                    case InputMode.ActiveAndBelow:
                    case InputMode.AllVisibleLayers:
                    case InputMode.AllHiddenLayers:
                        return layers;
                    default:
                        throw new ArgumentException("The mode was not handled: " + mode.ToString());
                }
            }
        }

        private unsafe string PrepareCroppedLayers(InputMode inputMode, RectangleF cropRect)
        {
            if (inputMode == InputMode.NoInput)
            {
                return string.Empty;
            }

            IReadOnlyList<GmicLayer> layers = GetRequestedLayers(inputMode);

            if (memoryMappedFiles.Capacity < layers.Count)
            {
                memoryMappedFiles.Capacity = layers.Count;
            }

            StringBuilder reply = new StringBuilder();

            foreach (GmicLayer layer in layers)
            {
                Surface surface = layer.Surface;
                bool disposeSurface = false;
                int destinationImageStride = surface.Stride;

                if (cropRect != WholeImageCropRect)
                {
                    int cropX = (int)Math.Floor(cropRect.X * layer.Width);
                    int cropY = (int)Math.Floor(cropRect.Y * layer.Height);
                    int cropWidth = (int)Math.Min(layer.Width - cropX, 1 + Math.Ceiling(cropRect.Width * layer.Width));
                    int cropHeight = (int)Math.Min(layer.Height - cropY, 1 + Math.Ceiling(cropRect.Height * layer.Height));

                    try
                    {
                        surface = layer.Surface.CreateWindow(cropX, cropY, cropWidth, cropHeight);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        throw new InvalidOperationException(string.Format("Surface.CreateWindow bounds invalid, cropRect={0}", cropRect.ToString()), ex);
                    }
                    disposeSurface = true;
                    destinationImageStride = cropWidth * ColorBgra.SizeOf;
                }

                string mapName = "pdn_" + Guid.NewGuid().ToString();

                try
                {
                    MemoryMappedFile file = MemoryMappedFile.CreateNew(mapName, surface.Scan0.Length);
                    memoryMappedFiles.Add(file);

                    using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor())
                    {
                        byte* destination = null;
                        RuntimeHelpers.PrepareConstrainedRegions();
                        try
                        {
                            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);

                            for (int y = 0; y < surface.Height; y++)
                            {
                                ColorBgra* src = surface.GetRowAddressUnchecked(y);
                                byte* dst = destination + (y * destinationImageStride);

                                Buffer.MemoryCopy(src, dst, destinationImageStride, destinationImageStride);
                            }
                        }
                        finally
                        {
                            if (destination != null)
                            {
                                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                            }
                        }
                    }
                }
                finally
                {
                    if (disposeSurface)
                    {
                        surface.Dispose();
                    }
                }

                reply.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}\n",
                    mapName,
                    surface.Width.ToString(CultureInfo.InvariantCulture),
                    surface.Height.ToString(CultureInfo.InvariantCulture),
                    destinationImageStride.ToString(CultureInfo.InvariantCulture));
            }

            return reply.ToString();
        }

        private unsafe void ProcessOutputImage(List<string> outputLayers, OutputMode outputMode)
        {
            if (outputLayers.Count != 1)
            {
                throw new InvalidOperationException("The output layer count must be 1.");
            }

            if (outputMode != OutputMode.InPlace)
            {
                outputMode = OutputMode.InPlace;
            }

            if (!TryGetValue(outputLayers[0], "layer=", out string packedLayerArgs))
            {
                throw new InvalidOperationException("Expected a layer message argument.");
            }

            string[] layerArgs = packedLayerArgs.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (layerArgs.Length != 4)
            {
                throw new InvalidOperationException("A layer message argument must have 4 values.");
            }

            string sharedMemoryName = layerArgs[0];
            int width = int.Parse(layerArgs[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
            int height = int.Parse(layerArgs[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
            int stride = int.Parse(layerArgs[3], NumberStyles.Integer, CultureInfo.InvariantCulture);

            if (output == null || output.Width != width || output.Height != height)
            {
                output?.Dispose();
                output = new Surface(width, height);
            }

            using (MemoryMappedFile file = MemoryMappedFile.OpenExisting(sharedMemoryName))
            {
                using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor())
                {
                    byte* sourceScan0 = null;
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref sourceScan0);

                        for (int y = 0; y < output.Height; y++)
                        {
                            byte* src = sourceScan0 + (y * stride);
                            ColorBgra* dst = output.GetRowAddressUnchecked(y);

                            Buffer.MemoryCopy(src, dst, stride, stride);
                        }
                    }
                    finally
                    {
                        if (sourceScan0 != null)
                        {
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
                }
            }

            OnOutputImageChanged();
        }

        private void OnOutputImageChanged()
        {
            OutputImageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void VerifyNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GmicPipeServer));
            }
        }

        private static bool TryGetValue(string item, string prefix, out string value)
        {
            if (item != null && item.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = item.Substring(prefix.Length);

                return !string.IsNullOrWhiteSpace(value);
            }

            value = null;
            return false;
        }

        private static void SendMessage(NamedPipeServerStream stream, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            int messageLength = Encoding.UTF8.GetByteCount(message);

            byte[] messageBytes = new byte[sizeof(int) + messageLength];

            messageBytes[0] = (byte)(messageLength & 0xff);
            messageBytes[1] = (byte)((messageLength >> 8) & 0xff);
            messageBytes[2] = (byte)((messageLength >> 16) & 0xff);
            messageBytes[3] = (byte)((messageLength >> 24) & 0xff);
            Encoding.UTF8.GetBytes(message, 0, message.Length, messageBytes, 4);

            stream.Write(messageBytes, 0, messageBytes.Length);
        }
    }
}
