using System.Runtime.InteropServices;

namespace Sigmap.Scanner.Agent.Capture.Wifi;

/// <summary>
/// Minimal Linux AF_PACKET raw socket for capturing 802.11 frames on
/// monitor-mode interfaces without libpcap/SharpPcap. Frames are read straight
/// from the kernel and parsed by <see cref="RadiotapParser"/> /
/// <see cref="Dot11FrameParser"/> — no kernel-side BPF filtering. Requires
/// CAP_NET_RAW.
/// </summary>
public sealed class RawPacketSocket : IDisposable
{
    private const int AfPacket = 17;
    private const int SockRaw = 3;
    private const ushort EthPAll = 0x0003;
    private const int ReadTimeoutMs = 500;

    // SOL_SOCKET / SO_RCVTIMEO / SO_RCVBUF
    private const int SolSocket = 1;
    private const int SoRcvtimeo = 20;
    private const int SoRcvbuf = 8;

    // SOL_PACKET / PACKET_ADD_MEMBERSHIP / PACKET_MR_PROMISC (linux/if_packet.h).
    // Registers the socket for promiscuous delivery so PACKET_OTHERHOST frames
    // (every 802.11 frame seen on a monitor interface) are not dropped.
    private const int SolPacket = 263;
    private const int PacketAddMembership = 1;
    private const ushort PacketMrPromisc = 1;

    // SIOCGIFFLAGS / SIOCSIFFLAGS / IFF_PROMISC: also raise the device-level
    // promiscuous flag (libpcap does both).
    private const ulong Siocgifflags = 0x8913;
    private const ulong Siocsifflags = 0x8914;
    private const short IffPromisc = 0x0100;

    private static readonly uint SockAddrLlSize = (uint)Marshal.SizeOf<SockAddrLl>();
    private static readonly uint PacketMreqSize = (uint)Marshal.SizeOf<PacketMreq>();

    [StructLayout(LayoutKind.Sequential)]
    private struct SockAddrLl
    {
        public ushort Family;
        public ushort Protocol;
        public int IfIndex;
        public ushort Hatype;
        public byte Pkttype;
        public byte Halen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Addr;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PacketMreq
    {
        public int IfIndex;
        public ushort MrType;
        public ushort MrAlen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Address;
    }

    // struct timeval (two longs on Linux/glibc).
    [StructLayout(LayoutKind.Sequential)]
    private struct TimeVal
    {
        public long TvSec;
        public long TvUsec;
    }

    // struct ifreq: ifr_name[IFNAMSIZ=16] + union (ifr_flags as short here).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct Ifreq
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string Name;
        public short Flags;
        public short Pad0;
        public int Pad1;
        public int Pad2;
        public int Pad3;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    private static extern int bind(int sockfd, ref SockAddrLl addr, uint addrlen);

    [DllImport("libc", SetLastError = true)]
    private static extern int setsockopt(int sockfd, int level, int optname, ref PacketMreq optval, uint optlen);

    [DllImport("libc", SetLastError = true)]
    private static extern int setsockopt(int sockfd, int level, int optname, ref TimeVal optval, uint optlen);

    [DllImport("libc", SetLastError = true)]
    private static extern int setsockopt(int sockfd, int level, int optname, ref int optval, uint optlen);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, ref Ifreq arg);

    [DllImport("libc", SetLastError = true)]
    private static extern int recv(int sockfd, [Out] byte[] buf, nuint len, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern uint if_nametoindex([MarshalAs(UnmanagedType.LPUTF8Str)] string ifname);

    private readonly byte[] _buffer = new byte[65536];
    private int _fd = -1;
    private int _receivedLen;

    public bool IsOpen => _fd >= 0;

    /// <summary>errno from the last failed recv (EAGAIN while idle), or 0.</summary>
    public int LastError { get; private set; }

    /// <summary>The most recently read frame (valid only until the next call to <see cref="Read"/>).</summary>
    public ReadOnlySpan<byte> Received => _buffer.AsSpan(0, _receivedLen);

    public void Open(string interfaceName)
    {
        var ifindex = (int)if_nametoindex(interfaceName);
        if (ifindex == 0)
            throw new InvalidOperationException($"No such interface: '{interfaceName}'");

        var fd = socket(AfPacket, SockRaw, (int)EndianSwap(EthPAll));
        if (fd < 0)
            throw new InvalidOperationException($"socket(AF_PACKET) failed: {Marshal.GetLastPInvokeErrorMessage()}");

        try
        {
            // Blocking recv() with a timeout, so the loop wakes periodically to
            // observe cancellation instead of sleeping in recv() forever.
            var rcvtimeo = new TimeVal { TvSec = 0, TvUsec = ReadTimeoutMs * 1000 };
            if (setsockopt(fd, SolSocket, SoRcvtimeo, ref rcvtimeo, (uint)Marshal.SizeOf<TimeVal>()) != 0)
                throw new InvalidOperationException($"setsockopt(SO_RCVTIMEO) failed: {Marshal.GetLastPInvokeErrorMessage()}");

            // Generous kernel buffer to survive bursts (e.g. right after a hop).
            var rcvbuf = 1 << 22;
            if (setsockopt(fd, SolSocket, SoRcvbuf, ref rcvbuf, sizeof(int)) != 0)
                throw new InvalidOperationException($"setsockopt(SO_RCVBUF) failed: {Marshal.GetLastPInvokeErrorMessage()}");

            var addr = new SockAddrLl
            {
                Family = AfPacket,
                Protocol = EndianSwap(EthPAll),
                IfIndex = ifindex,
                Addr = new byte[8],
            };
            if (bind(fd, ref addr, SockAddrLlSize) != 0)
                throw new InvalidOperationException($"bind({interfaceName}) failed: {Marshal.GetLastPInvokeErrorMessage()}");

            // Socket-level promiscuous membership (what libpcap registers).
            var mreq = new PacketMreq
            {
                IfIndex = ifindex,
                MrType = PacketMrPromisc,
                Address = new byte[8],
            };
            if (setsockopt(fd, SolPacket, PacketAddMembership, ref mreq, PacketMreqSize) != 0)
                throw new InvalidOperationException(
                    $"promiscuous membership on {interfaceName} failed: {Marshal.GetLastPInvokeErrorMessage()}");

            // Device-level IFF_PROMISC, exactly like libpcap does.
            var ifreq = new Ifreq { Name = interfaceName };
            if (ioctl(fd, Siocgifflags, ref ifreq) != 0)
                throw new InvalidOperationException($"ioctl(SIOCGIFFLAGS) failed: {Marshal.GetLastPInvokeErrorMessage()}");
            ifreq.Flags |= IffPromisc;
            if (ioctl(fd, Siocsifflags, ref ifreq) != 0)
                throw new InvalidOperationException($"ioctl(SIOCSIFFLAGS) failed: {Marshal.GetLastPInvokeErrorMessage()}");

            _fd = fd;
        }
        catch
        {
            close(fd);
            throw;
        }
    }

    /// <summary>
    /// Blocks up to <see cref="ReadTimeoutMs"/> for the next frame. Returns the
    /// frame length, or &lt; 0 when nothing arrived in time (idle timeout) or
    /// the capture failed. Check <see cref="LastError"/> on failure.
    /// </summary>
    public int Read()
    {
        var len = recv(_fd, _buffer, (nuint)_buffer.Length, 0);
        if (len < 0)
        {
            _receivedLen = -1;
            LastError = Marshal.GetLastPInvokeError();
            return _receivedLen;
        }

        LastError = 0;
        _receivedLen = len;
        return _receivedLen;
    }

    public void Close()
    {
        if (_fd >= 0)
        {
            close(_fd);
            _fd = -1;
        }
    }

    public void Dispose() => Close();

    private static ushort EndianSwap(ushort value) => (ushort)((value << 8) | (value >> 8));
}
