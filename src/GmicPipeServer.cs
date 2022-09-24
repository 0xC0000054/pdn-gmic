/*
*  This file is part of pdn-gmic, a Paint.NET Effect that
*  that provides integration with G'MIC-Qt.
*
*  Copyright (C) 2018, 2019, 2020, 2021, 2022 Nicholas Hayes
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
using PaintDotNet.AppModel;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.IO;
using PaintDotNet.Rendering;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace GmicEffectPlugin
{
    internal sealed class GmicPipeServer : IDisposable
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly List<GmicLayer> layers;
        private readonly List<MemoryMappedFile> memoryMappedFiles;
        private int activeLayerIndex;
        private NamedPipeServerStream server;
        private bool disposed;

        private readonly string pipeName;
        private readonly string fullPipeName;
        private readonly SynchronizationContext synchronizationContext;
        private readonly SendOrPostCallback outputImageCallback;
        private readonly IArrayPoolService arrayPoolService;
        private readonly IBitmapEffectEnvironment effectEnvironment;
        private readonly SizeInt32 canvasSize;
#pragma warning restore IDE0032 // Use auto property

        private static readonly RectangleF WholeImageCropRect = new(0.0f, 0.0f, 1.0f, 1.0f);

        /// <summary>
        /// Initializes a new instance of the <see cref="GmicPipeServer"/> class.
        /// </summary>
        /// <param name="services">The Paint.NET effect service provider.</param>
        /// <param name="effectEnvironment">The effect environment.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> is null.
        /// -or-
        /// <paramref name="effectEnvironment"/> is null.
        /// </exception>
        public GmicPipeServer(IServiceProvider services, IBitmapEffectEnvironment effectEnvironment)
            : this(null, services, effectEnvironment)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GmicPipeServer"/> class.
        /// </summary>
        /// <param name="synchronizationContext">The synchronization context.</param>
        /// <param name="services">The Paint.NET effect service provider.</param>
        /// <param name="effectEnvironment">The effect environment.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> is null.
        /// -or-
        /// <paramref name="effectEnvironment"/> is null.
        /// </exception>
        public GmicPipeServer(SynchronizationContext synchronizationContext,
                              IServiceProvider services,
                              IBitmapEffectEnvironment effectEnvironment)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(effectEnvironment);

            pipeName = "PDN_GMIC" + Guid.NewGuid().ToString();
            fullPipeName = @"\\.\pipe\" + pipeName;
            this.synchronizationContext = synchronizationContext;
            this.effectEnvironment = effectEnvironment;
            canvasSize = effectEnvironment.CanvasSize;
            outputImageCallback = new SendOrPostCallback(OutputImageChangedCallback);
            arrayPoolService = services.GetService<IArrayPoolService>();
            layers = new List<GmicLayer>();
            memoryMappedFiles = new List<MemoryMappedFile>();
            disposed = false;
        }

        public string FullPipeName => fullPipeName;

        public string GmicCommandName { get; private set; }

        public OutputImageState OutputImageState { get; private set; }

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
            [Obsolete("Removed in G'MIC-Qt version 2.8.2", true)]
            AllVisibleLayersDescending,
            [Obsolete("Removed in G'MIC-Qt version 2.8.2", true)]
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

                if (OutputImageState != null)
                {
                    OutputImageState.Dispose();
                    OutputImageState = null;
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
        /// <exception cref="InvalidOperationException">This instance is already running.</exception>
        /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
        public void Start()
        {
            VerifyNotDisposed();

            if (server != null)
            {
                throw new InvalidOperationException("This instance is already running.");
            }

            IReadOnlyList<IEffectLayerInfo> effectLayerInfos = effectEnvironment.Document.Layers;
            int layerCount = effectLayerInfos.Count;

            layers.Capacity = layerCount;

            // Paint.NET stores layers in bottom-to-top order and G'MIC expects the layers to be in
            // top-to-bottom order (which is what GIMP uses).
            for (int i = layerCount - 1; i >= 0; i--)
            {
                IEffectLayerInfo layerInfo = effectLayerInfos[i];

                layers.Add(new GmicLayer(layerInfo));
            }
            activeLayerIndex = layerCount - (1 + effectEnvironment.SourceLayerIndex);

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

            List<string> parameters = GetMessageParameters();

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

                SendReplyToClient(reply);
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

                SendReplyToClient(reply);
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

                string reply = ProcessOutputImage(outputLayers, outputMode);
                SendReplyToClient(reply);
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

                SendReplyToClient("done");
            }
            else if (command.Equals("gmic_qt_set_gmic_command_name", StringComparison.Ordinal))
            {
                GmicCommandName = parameters[1];

                SendReplyToClient("done");
            }

            // Wait for the acknowledgment that the client is done reading.
            if (server.IsConnected)
            {
                Span<byte> doneMessageBuffer = stackalloc byte[4];
                int bytesRead = 0;
                int bytesToRead = doneMessageBuffer.Length;

                do
                {
                    int n = server.Read(doneMessageBuffer.Slice(bytesRead, bytesToRead));

                    bytesRead += n;
                    bytesToRead -= n;

                } while (bytesToRead > 0 && server.IsConnected);
            }

            // Start a new server and wait for the next connection.
            server.Dispose();
            server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            server.BeginWaitForConnection(WaitForConnectionCallback, null);
        }

        private static InputMode ParseInputMode(string item)
        {
            if (Enum.TryParse(item, out InputMode temp))
            {
                if (temp >= InputMode.NoInput && temp <= InputMode.AllHiddenLayers)
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
                case InputMode.ActiveAndBelow:
                case InputMode.AllLayers:
                case InputMode.ActiveAndAbove:
                case InputMode.AllVisibleLayers:
                case InputMode.AllHiddenLayers:
                    // Paint.NET layers are always the same size as the parent document.
                    width = canvasSize.Width;
                    height = canvasSize.Height;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported InputMode: " + inputMode.ToString());
            }

            return width.ToString(CultureInfo.InvariantCulture) + "," + height.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Disable Inlining due to the use of stackalloc.
        [SkipLocalsInit]
        private List<string> GetMessageParameters()
        {
            const int MaxStackAllocBufferSize = 128;

            Span<byte> replySizeBuffer = stackalloc byte[sizeof(int)];
            server.ProperRead(replySizeBuffer);

            int messageLength = BinaryPrimitives.ReadInt32LittleEndian(replySizeBuffer);
            IArrayPoolBuffer<byte> bufferFromPool = null;

            try
            {
                Span<byte> buffer = stackalloc byte[MaxStackAllocBufferSize];

                if (messageLength > MaxStackAllocBufferSize)
                {
                    bufferFromPool = arrayPoolService.Rent<byte>(messageLength);
                    buffer = bufferFromPool.AsSpan();
                }

                Span<byte> messageBytes = buffer.Slice(0, messageLength);

                server.ProperRead(messageBytes);

                return DecodeMessageBuffer(messageBytes);
            }
            finally
            {
                bufferFromPool?.Dispose();
            }

            static List<string> DecodeMessageBuffer(ReadOnlySpan<byte> bytes)
            {
                const byte Separator = (byte)'\n';

                List<string> messageParameters = new();

                if (bytes[bytes.Length - 1] == Separator)
                {
                    // A message with multiple values uses \n as the separator and terminator.
                    foreach (ReadOnlySpan<byte> parameter in bytes.Split(Separator))
                    {
                        // Empty strings are skipped.
                        if (parameter.Length > 0)
                        {
                            messageParameters.Add(Encoding.UTF8.GetString(parameter));
                        }
                    }
                }
                else
                {
                    messageParameters.Add(Encoding.UTF8.GetString(bytes));
                }

                return messageParameters;
            }
        }

        private IReadOnlyList<GmicLayer> GetRequestedLayers(InputMode mode)
        {
            if (mode == InputMode.ActiveLayer)
            {
                return new GmicLayer[] { layers[activeLayerIndex] };
            }
            else if (mode == InputMode.ActiveAndAbove)
            {
                // The layers are stored in top-to-bottom order, which is what G'MIC uses.
                List<GmicLayer> requestedLayers = new()
                {
                    layers[activeLayerIndex]
                };

                if (activeLayerIndex < (layers.Count - 1))
                {
                    requestedLayers.Add(layers[activeLayerIndex + 1]);
                }

                return requestedLayers;
            }
            else if (mode == InputMode.ActiveAndBelow)
            {
                List<GmicLayer> requestedLayers = new();

                // The layers are stored in top-to-bottom order, which is what G'MIC uses.
                if (activeLayerIndex > 0)
                {
                    requestedLayers.Add(layers[activeLayerIndex - 1]);
                }
                requestedLayers.Add(layers[activeLayerIndex]);

                return requestedLayers;
            }
            else if (mode == InputMode.AllVisibleLayers)
            {
                List<GmicLayer> requestedLayers = new();

                // The layers are stored in top-to-bottom order, which is what G'MIC uses.
                for (int i = 0; i < layers.Count; i++)
                {
                    GmicLayer layer = layers[i];

                    if (layer.Visible)
                    {
                        requestedLayers.Add(layer);
                    }
                }

                return requestedLayers;
            }
            else if (mode == InputMode.AllHiddenLayers)
            {
                List<GmicLayer> requestedLayers = new();

                // The layers are stored in top-to-bottom order, which is what G'MIC uses.
                for (int i = 0; i < layers.Count; i++)
                {
                    GmicLayer layer = layers[i];

                    if (!layer.Visible)
                    {
                        requestedLayers.Add(layer);
                    }
                }

                return requestedLayers;
            }
            else
            {
                switch (mode)
                {
                    case InputMode.AllLayers:
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

            if (layers.Count == 0)
            {
                return string.Empty;
            }

            if (memoryMappedFiles.Capacity < layers.Count)
            {
                memoryMappedFiles.Capacity = layers.Count;
            }

            StringBuilder reply = new();

            foreach (GmicLayer layer in layers)
            {
                RectInt32 roi = layer.Bounds;

                if (cropRect != WholeImageCropRect)
                {
                    int cropX = (int)Math.Floor(cropRect.X * layer.Width);
                    int cropY = (int)Math.Floor(cropRect.Y * layer.Height);
                    int cropWidth = (int)Math.Min(layer.Width - cropX, 1 + Math.Ceiling(cropRect.Width * layer.Width));
                    int cropHeight = (int)Math.Min(layer.Height - cropY, 1 + Math.Ceiling(cropRect.Height * layer.Height));

                    roi = new RectInt32(cropX, cropY, cropWidth, cropHeight);
                }

                string mapName = "pdn_" + Guid.NewGuid().ToString();
                int destinationStride = roi.Width * sizeof(ColorBgra32);

                long destinationSizeInBytes = (long)destinationStride * roi.Height;

                MemoryMappedFile file = MemoryMappedFile.CreateNew(mapName, destinationSizeInBytes);
                memoryMappedFiles.Add(file);

                using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor())
                {
                    byte* destination = null;
                    try
                    {
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);

                        using (IBitmap<ColorBgra32> bitmap = layer.ToBitmap(roi))
                        using (IBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock(BitmapLockOptions.Read))
                        {
                            RegionPtr<ColorBgra32> dst = new((ColorBgra32*)destination, bitmap.Size, destinationStride);

                            bitmapLock.AsRegionPtr().CopyTo(dst);
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

                reply.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}\n",
                    mapName,
                    roi.Width.ToString(CultureInfo.InvariantCulture),
                    roi.Height.ToString(CultureInfo.InvariantCulture),
                    destinationStride.ToString(CultureInfo.InvariantCulture));
            }

            return reply.ToString();
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private unsafe string ProcessOutputImage(List<string> outputLayers, OutputMode outputMode)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            string reply = string.Empty;

            List<IBitmap<ColorBgra32>> outputImages = null;
            Exception error = null;

            try
            {
                outputImages = new List<IBitmap<ColorBgra32>>(outputLayers.Count);

                for (int i = 0; i < outputLayers.Count; i++)
                {
                    if (!TryGetValue(outputLayers[i], "layer=", out string packedLayerArgs))
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

                    IBitmap<ColorBgra32> output = null;
                    bool disposeOutput = true;

                    try
                    {
                        output = effectEnvironment.ImagingFactory.CreateBitmap<ColorBgra32>(width, height);

                        using (MemoryMappedFile file = MemoryMappedFile.OpenExisting(sharedMemoryName))
                        {
                            using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor())
                            {
                                byte* sourceScan0 = null;
                                try
                                {
                                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref sourceScan0);

                                    using (IBitmapLock<ColorBgra32> bitmapLock = output.Lock(BitmapLockOptions.Write))
                                    {
                                        RegionPtr<ColorBgra32> src = new((ColorBgra32*)sourceScan0, width, height, stride);

                                        src.CopyTo(bitmapLock.AsRegionPtr());
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

                        outputImages.Add(output);
                        disposeOutput = false;
                    }
                    finally
                    {
                        if (disposeOutput)
                        {
                            output?.Dispose();
                        }
                    }
                }

                // Set the first output layer as the active layer.
                // This allows multiple G'MIC effects to be "layered" using the Apply button.
                layers[activeLayerIndex].Dispose();
                layers[activeLayerIndex] = new GmicLayer(outputImages[0]);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            OutputImageState?.Dispose();
            OutputImageState = new OutputImageState(error, outputImages);

            RaiseOutputImageChanged();

            return reply;
        }

        private void RaiseOutputImageChanged()
        {
            if (synchronizationContext != null)
            {
                synchronizationContext.Send(outputImageCallback, null);
            }
            else
            {
                OnOutputImageChanged();
            }
        }

        private void OutputImageChangedCallback(object state)
        {
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

        [MethodImpl(MethodImplOptions.NoInlining)] // Disable Inlining due to the use of stackalloc.
        [SkipLocalsInit]
        private void SendReplyToClient(string message)
        {
            ArgumentNullException.ThrowIfNull(nameof(message));

            const int MaxStackAllocBufferSize = 256;

            IArrayPoolBuffer<byte> bufferFromPool = null;

            try
            {
                int messageLength = Encoding.UTF8.GetByteCount(message);
                int totalMessageLength = sizeof(int) + messageLength;

                Span<byte> buffer = stackalloc byte[MaxStackAllocBufferSize];

                if (totalMessageLength > MaxStackAllocBufferSize)
                {
                    bufferFromPool = arrayPoolService.Rent<byte>(totalMessageLength);
                    buffer = bufferFromPool.AsSpan();
                }

                BinaryPrimitives.WriteInt32LittleEndian(buffer, messageLength);
                Encoding.UTF8.GetBytes(message, buffer.Slice(sizeof(int), messageLength));

                Span<byte> messageBuffer = buffer.Slice(0, totalMessageLength);

                server.Write(messageBuffer);
            }
            finally
            {
                bufferFromPool?.Dispose();
            }
        }
    }
}
