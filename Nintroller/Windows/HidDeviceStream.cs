using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace NintrollerLib
{
    using static NintrollerLib.NativeImports;

    public class HidDeviceStream : Stream
    {
        private string m_path;
        private FileShare m_sharingMode;
        private SafeFileHandle m_handle;

        private byte[] m_readBuffer;
        private byte[] m_writeBuffer;

        // One event per direction, reused for every report instead of creating a kernel event each time
        private readonly object m_readLock = new object();
        private readonly object m_writeLock = new object();
        private readonly ManualResetEvent m_readEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent m_writeEvent = new ManualResetEvent(false);

        /// <summary>
        /// Returned by <see cref="Read(byte[], int, int, int, CancellationToken)"/> when no report arrived in time.
        /// </summary>
        public const int ReadTimedOut = -1;

        private enum TransferStatus
        {
            Completed,
            TimedOut,
            Cancelled
        }

        public override bool CanRead => InputLength > 0;
        public override bool CanWrite => OutputLength > 0;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public int InputLength { get; private set; }
        public int OutputLength { get; private set; }

        public bool UseHidD { get; set; }

        private HidDeviceStream() { }

        public HidDeviceStream(string path, FileShare sharingMode = FileShare.ReadWrite)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            int result = Initialize(path, sharingMode);
            if (result != (int)Win32Error.Success)
            {
                throw new Win32Exception(result);
            }
        }

        public static bool TryCreate(string path, out HidDeviceStream stream, FileShare sharingMode = FileShare.ReadWrite)
        {
            stream = new HidDeviceStream();
            int result = stream.Initialize(path, sharingMode);
            if (result != (int)Win32Error.Success)
            {
                stream = null;
                return false;
            }

            return true;
        }

        private int Initialize(string path, FileShare sharingMode)
        {
            m_path = path;
            m_sharingMode = sharingMode;
            if (!OpenDevice(path, out m_handle, sharingMode))
            {
                return Marshal.GetLastWin32Error();
            }

            int result = ReadCapabilities(path);
            if (result != (int)Win32Error.Success)
            {
                // Don't leave the device open; in exclusive mode that would block every later attempt
                m_handle.Close();
                m_handle = null;
            }

            return result;
        }

        private int ReadCapabilities(string path)
        {
            int result;
            if (!HidD_GetPreparsedData(m_handle, out var hidData))
            {
                result = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Could not get preparsed data for {path}: {new Win32Exception(result).Message} ({result})");
                return result;
            }

            using (hidData)
            {
                if (hidData == null || hidData.IsInvalid)
                {
                    Debug.WriteLine($"Preparsed data handle is invalid for {path}");
                    return (int)Win32Error.InvalidHandle;
                }

                result = HidP_GetCaps(hidData, out var caps);
                if (result < 0) // HRESULT, not Win32 error
                {
                    Debug.WriteLine($"Could not get HID capabilities for {path}: {new Win32Exception(result).Message} ({result})");
                    return result;
                }

                InputLength = caps.InputReportByteLength;
                OutputLength = caps.OutputReportByteLength;
            }

            m_readBuffer = new byte[InputLength];
            m_writeBuffer = new byte[OutputLength];

            return (int)Win32Error.Success;
        }

        private static bool OpenDevice(string path, out SafeFileHandle handle, FileShare sharingMode = FileShare.ReadWrite)
        {
            handle = CreateFile(path, FileAccess.ReadWrite, sharingMode, IntPtr.Zero, FileMode.Open, EFileAttributes.Overlapped, IntPtr.Zero);
            bool result = handle != null && !handle.IsInvalid;
            Debug.WriteLineIf(!result, $"Could not create file for path {path}: {new Win32Exception(Marshal.GetLastWin32Error()).Message} ({Marshal.GetLastWin32Error()})");
            return result;
        }

        public bool Open()
        {
            if (m_handle != null && !m_handle.IsInvalid)
                return true;

            return OpenDevice(m_path, out m_handle, m_sharingMode);
        }

        private void VerifyArguments(byte[] buffer, int offset, int count)
        {
            if (m_handle == null)
                throw new ObjectDisposedException(nameof(m_handle));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException($"Offset must not be negative! Got {offset}", nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException($"Count must not be negative! Got {count}", nameof(count));

            if ((buffer.Length - offset) < count)
                throw new ArgumentException($"Buffer is too small for the given count! Must be at least {count}, got {buffer.Length}", $"{nameof(buffer)}, {nameof(count)}");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadInternal(buffer, offset, count, Timeout.Infinite, CancellationToken.None);
        }

        /// <summary>
        /// Reads a report on the calling thread. Returns <see cref="ReadTimedOut"/> if nothing arrived
        /// within <paramref name="timeout"/> milliseconds, or 0 if <paramref name="token"/> was cancelled.
        /// </summary>
        public int Read(byte[] buffer, int offset, int count, int timeout, CancellationToken token)
        {
            return ReadInternal(buffer, offset, count, timeout, token);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return Task.Run(() => {
                return ReadInternal(buffer, offset, count, Timeout.Infinite, token);
            });
        }

        private int ReadInternal(byte[] buffer, int offset, int count, int timeout, CancellationToken token)
        {
            VerifyArguments(buffer, offset, count);
            if (buffer.Length < 1 || count < 1)
                return 0;

            lock (m_readLock)
            {
                uint bytesRead;
                if (UseHidD)
                {
                    if (!HidD_GetInputReport(m_handle, m_readBuffer, (uint)m_readBuffer.Length))
                    {
                        ThrowLastError();
                    }

                    bytesRead = (uint)InputLength;
                }
                else
                {
                    var status = TransferOverlapped(true, m_readBuffer, m_readEvent, timeout, token, out bytesRead);
                    if (status == TransferStatus.TimedOut)
                        return ReadTimedOut;
                    if (status == TransferStatus.Cancelled)
                        return 0;
                }

                Debug.WriteLine($"Read: {BitConverter.ToString(m_readBuffer)}");

                Array.Copy(m_readBuffer, 0, buffer, offset, Math.Min(m_readBuffer.Length, buffer.Length - offset));
                return (int)bytesRead;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteInternal(buffer, offset, count, CancellationToken.None);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return Task.Run(() => {
                WriteInternal(buffer, offset, count, token);
            });
        }

        private void WriteInternal(byte[] buffer, int offset, int count, CancellationToken token)
        {
            VerifyArguments(buffer, offset, count);
            if (buffer.Length < 1 || count < 1)
                return;

            lock (m_writeLock)
            {
                Array.Copy(buffer, offset, m_writeBuffer, 0, Math.Min(m_writeBuffer.Length, buffer.Length - offset));
                for (int i = buffer.Length - offset; i < m_writeBuffer.Length; i++)
                {
                    m_writeBuffer[i] = 0;
                }

                Debug.WriteLine($"Writing: {BitConverter.ToString(m_writeBuffer)}");

                if (UseHidD)
                {
                    if (!HidD_SetOutputReport(m_handle, m_writeBuffer, (uint)m_writeBuffer.Length))
                    {
                        ThrowLastError();
                    }
                }
                else
                {
                    TransferOverlapped(false, m_writeBuffer, m_writeEvent, Timeout.Infinite, token, out _);
                }
            }
        }

        private static void ThrowLastError()
        {
            int result = Marshal.GetLastWin32Error();
            if (result != (int)Win32Error.Success)
            {
                throw new Win32Exception(result);
            }

            Debug.WriteLine("Result was failure but GetLastWin32Error returned success");
        }

        // Runs one overlapped ReadFile/WriteFile. The kernel keeps using the buffer and the OVERLAPPED until
        // the operation is finished, so both live in unmanaged memory (a managed array can be moved by the GC),
        // and this never returns before the operation has completed or its cancellation has gone through.
        private TransferStatus TransferOverlapped(bool read, byte[] data, ManualResetEvent ioEvent, int timeout, CancellationToken token, out uint transferred)
        {
            int overlappedSize = Marshal.SizeOf(typeof(NativeOverlapped));
            IntPtr memory = Marshal.AllocHGlobal(overlappedSize + data.Length);
            try
            {
                IntPtr overlapped = memory;
                IntPtr buffer = memory + overlappedSize;

                ioEvent.Reset();
                var header = new NativeOverlapped { EventHandle = ioEvent.SafeWaitHandle.DangerousGetHandle() };
                Marshal.StructureToPtr(header, overlapped, false);

                if (!read)
                    Marshal.Copy(data, 0, buffer, data.Length);

                bool success = read
                    ? ReadFile(m_handle, buffer, (uint)data.Length, IntPtr.Zero, overlapped)
                    : WriteFile(m_handle, buffer, (uint)data.Length, IntPtr.Zero, overlapped);
                int result = Marshal.GetLastWin32Error();
                if (!success && result != (int)Win32Error.IoPending)
                    throw new Win32Exception(result);

                var status = TransferStatus.Completed;
                if (!success)
                {
                    int signaled = token.CanBeCanceled
                        ? WaitHandle.WaitAny(new WaitHandle[] { ioEvent, token.WaitHandle }, timeout, false)
                        : (ioEvent.WaitOne(timeout, false) ? 0 : WaitHandle.WaitTimeout);

                    if (signaled != 0)
                    {
                        status = signaled == WaitHandle.WaitTimeout ? TransferStatus.TimedOut : TransferStatus.Cancelled;
                        CancelIoEx(m_handle, overlapped);
                    }
                }

                // Also waits for a cancellation requested above to go through
                success = GetOverlappedResult(m_handle, overlapped, out transferred, true);
                result = Marshal.GetLastWin32Error();
                if (!success)
                {
                    if (status != TransferStatus.Completed && result == (int)Win32Error.OperationAborted)
                        return status;

                    throw new Win32Exception(result);
                }

                // A report that arrived just before the cancellation still counts
                if (read)
                    Marshal.Copy(buffer, data, 0, (int)Math.Min(transferred, (uint)data.Length));

                return TransferStatus.Completed;
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                m_handle?.Close();
            }

            m_handle = null;
        }
    }
}