// ShieldGate - xac minh chu ky so cua tep thuc thi bang WinVerifyTrust.
// Chi dung kho chung chi va cache cua may: WTD_CACHE_ONLY_URL_RETRIEVAL,
// khong tai cung/crl tu Internet. Day la ly do A7: CreateFromSignedFile chi
// thay blob chu ky, khong phat hien chu ky gia hoac chuoi tin cay bi dut.
//
// Nhieu tep he thong (notepad.exe, cmd.exe, driver) khong nhung chu ky ma nam
// trong catalog cua Windows. Vi vay sau khi thu chu ky nhung, chi voi thu muc
// he thong moi tra catalog: ham bam ca tep nen khong duoc chay tren moi exe
// tren USB.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ShieldGate
{
    internal enum SignatureStatus
    {
        Unsigned,
        Signed,
        Invalid,
        Error
    }

    internal static class Signature
    {
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdChoiceCatalog = 2;
        private const uint WtdStateActionVerify = 1;
        private const uint WtdStateActionClose = 2;
        private const uint WtdCacheOnlyUrlRetrieval = 0x1000;

        private const int TrustENosignature = unchecked((int)0x800B0100);
        private const int TrustESubjectFormUnknown = unchecked((int)0x800B0003);
        private const int TrustEProviderUnknown = unchecked((int)0x800B0001);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustCatalogInfo
        {
            public uint cbStruct;
            public uint dwCatalogVersion;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszCatalogFilePath;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszMemberTag;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcwszMemberFilePath;
            public IntPtr hMemberFile;
            public IntPtr pbCalculatedFileHash;
            public uint cbCalculatedFileHash;
            public IntPtr pcCatalogContext;
        }

        // Chi khai bao cac truong co tren Windows 7. Windows 8+ chap nhan cbStruct
        // nho hon nen khong can pSignatureSettings (chi co tu Windows 8).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pUnion;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CatalogInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string wszCatalogFile;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, IntPtr data);

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern bool CryptCATAdminAcquireContext(out IntPtr phCatAdmin,
                                                               IntPtr pgSubsystem, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern bool CryptCATAdminCalcHashFromFileHandle(IntPtr hFile,
                                                                       ref uint pcbHash,
                                                                       byte[] pbHash, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr hCatAdmin,
                                                                      byte[] pbHash, uint cbHash,
                                                                      uint dwFlags,
                                                                      ref IntPtr phCatInfo);

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CryptCATCatalogInfoFromContext(IntPtr hCatInfo,
                                                                  ref CatalogInfo psCatInfo,
                                                                  uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr hCatAdmin,
                                                                      IntPtr hCatInfo, uint dwFlags);

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern bool CryptCATAdminReleaseContext(IntPtr hCatAdmin, uint dwFlags);

        private static readonly Guid ActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

        // Cache theo (duong dan, kich thuoc, thoi diem sua). Quet lai cung mot
        // o khong phai mo lai WinVerifyTrust cho tung exe; 4.096 muc la du cho
        // mot lan quet va khong giu bo nho vo han.
        private sealed class CacheEntry
        {
            public long Size;
            public long Ticks;
            public SignatureStatus Status;
        }

        private const int CacheLimit = 4096;
        private static readonly Dictionary<string, CacheEntry> Cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheGate = new object();

        internal static int CacheCount
        {
            get { lock (CacheGate) return Cache.Count; }
        }

        // Signed: chu ky hop le va chuoi tin cay duoc chap nhan (nhung hoac catalog).
        // Unsigned: khong co chu ky hoac khong phai dinh dang PE.
        // Invalid: co chu ky nhung sai/hong/khong tin cay (coi nhu khong ky).
        public static SignatureStatus Verify(string path)
        {
            if (string.IsNullOrEmpty(path)) return SignatureStatus.Unsigned;
            string key;
            long size, ticks;
            try
            {
                key = WinPath.Ext(path);
                if (!WinPath.Exists(key)) return SignatureStatus.Unsigned;
                if (!WinPath.Stat(key, out size, out ticks)) return SignatureStatus.Error;
                if (size <= 0) return SignatureStatus.Unsigned;
            }
            catch { return SignatureStatus.Error; }

            lock (CacheGate)
            {
                CacheEntry hit;
                if (Cache.TryGetValue(key, out hit) &&
                    hit.Size == size && hit.Ticks == ticks)
                    return hit.Status;
            }

            SignatureStatus status = VerifyUncached(path);
            Remember(key, size, ticks, status);
            return status;
        }

        private static SignatureStatus VerifyUncached(string path)
        {
            // Tep khong co Certificate Table thi khong the co chu ky nhung;
            // bo qua WinVerifyTrust (ham bam ca tep) tru khi la tep he thong
            // co the nam trong catalog.
            SignatureStatus embedded = PeHasSignature(path)
                ? VerifyEmbedded(path) : SignatureStatus.Unsigned;
            if (embedded == SignatureStatus.Signed || embedded == SignatureStatus.Error)
                return embedded;
            if (!ShouldTryCatalog(path)) return embedded;

            SignatureStatus catalog = VerifyCatalog(path);
            if (catalog == SignatureStatus.Signed) return SignatureStatus.Signed;
            if (catalog == SignatureStatus.Invalid) return SignatureStatus.Invalid;
            return embedded;
        }

        private static void Remember(string key, long size, long ticks, SignatureStatus status)
        {
            // Loi doc tep co the tam thoi (dang bi khoa); lan sau thu lai.
            if (status == SignatureStatus.Error) return;
            lock (CacheGate)
            {
                if (Cache.Count >= CacheLimit) Cache.Clear();
                CacheEntry e = new CacheEntry();
                e.Size = size;
                e.Ticks = ticks;
                e.Status = status;
                Cache[key] = e;
            }
        }

        // Doc DOS/NT header: co Certificate Table (data directory so 4) moi goi
        // WinVerifyTrust. Loi doc header thi tra true de khong bo sot chu ky.
        private static bool PeHasSignature(string path)
        {
            try
            {
                using (FileStream fs = WinPath.OpenRead(WinPath.Ext(path)))
                {
                    if (fs.Length < 0x40) return false;
                    byte[] head = new byte[0x40];
                    if (ReadFull(fs, head, 0, head.Length) != head.Length) return true;
                    if (head[0] != 0x4D || head[1] != 0x5A) return false;
                    int peOffset = BitConverter.ToInt32(head, 0x3C);
                    if (peOffset < 0x40 || peOffset > fs.Length - 24) return false;

                    fs.Seek(peOffset, SeekOrigin.Begin);
                    byte[] pe = new byte[24];
                    if (ReadFull(fs, pe, 0, pe.Length) != pe.Length) return true;
                    if (pe[0] != 0x50 || pe[1] != 0x45 || pe[2] != 0 || pe[3] != 0)
                        return false;

                    fs.Seek(peOffset + 24, SeekOrigin.Begin);
                    byte[] opt = new byte[240];
                    int got = ReadFull(fs, opt, 0, opt.Length);
                    if (got < 4) return true;
                    int dataDir;
                    if (opt[0] == 0x0B && opt[1] == 0x01) dataDir = 96;       // PE32
                    else if (opt[0] == 0x0B && opt[1] == 0x02) dataDir = 112;  // PE32+
                    else return false;

                    int security = dataDir + 4 * 8;
                    if (got < security + 8) return true;
                    return BitConverter.ToUInt32(opt, security + 4) > 0;
                }
            }
            catch { return true; }
        }

        private static int ReadFull(Stream s, byte[] buf, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, offset + total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        // Chi tep cua Windows/Program Files moi co catalog; ham bam ca tep nen
        // khong tra catalog cho exe tren USB hay thu muc nguoi dung.
        private static bool ShouldTryCatalog(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch { return false; }
            string[] roots = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                string prefix = root.TrimEnd('\\') + "\\";
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static SignatureStatus VerifyEmbedded(string path)
        {
            IntPtr unionPtr = IntPtr.Zero;
            try
            {
                WinTrustFileInfo file = new WinTrustFileInfo();
                file.cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
                file.pcwszFilePath = path;
                unionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustFileInfo)));
                Marshal.StructureToPtr(file, unionPtr, false);
                return RunVerify(unionPtr, WtdChoiceFile);
            }
            catch (DllNotFoundException) { return SignatureStatus.Error; }
            catch (EntryPointNotFoundException) { return SignatureStatus.Error; }
            catch { return SignatureStatus.Error; }
            finally
            {
                if (unionPtr != IntPtr.Zero)
                {
                    try { Marshal.DestroyStructure(unionPtr, typeof(WinTrustFileInfo)); } catch { }
                    Marshal.FreeHGlobal(unionPtr);
                }
            }
        }

        private static SignatureStatus VerifyCatalog(string path)
        {
            IntPtr admin = IntPtr.Zero;
            IntPtr catInfo = IntPtr.Zero;
            IntPtr hashPtr = IntPtr.Zero;
            IntPtr unionPtr = IntPtr.Zero;
            try
            {
                if (!CryptCATAdminAcquireContext(out admin, IntPtr.Zero, 0))
                    return SignatureStatus.Error;

                using (FileStream fs = new FileStream(path, FileMode.Open,
                                                       FileAccess.Read, FileShare.ReadWrite))
                {
                    IntPtr handle = fs.SafeFileHandle.DangerousGetHandle();
                    uint size = 0;
                    CryptCATAdminCalcHashFromFileHandle(handle, ref size, null, 0);
                    if (size == 0 || size > 128) return SignatureStatus.Unsigned;

                    byte[] hash = new byte[size];
                    if (!CryptCATAdminCalcHashFromFileHandle(handle, ref size, hash, 0))
                        return SignatureStatus.Error;

                    catInfo = CryptCATAdminEnumCatalogFromHash(admin, hash, size, 0, ref catInfo);
                    if (catInfo == IntPtr.Zero) return SignatureStatus.Unsigned;

                    CatalogInfo ci = new CatalogInfo();
                    ci.cbStruct = (uint)Marshal.SizeOf(typeof(CatalogInfo));
                    if (!CryptCATCatalogInfoFromContext(catInfo, ref ci, 0))
                        return SignatureStatus.Error;

                    WinTrustCatalogInfo wc = new WinTrustCatalogInfo();
                    wc.cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustCatalogInfo));
                    wc.dwCatalogVersion = 0;
                    wc.pcwszCatalogFilePath = ci.wszCatalogFile;
                    wc.pcwszMemberTag = Path.GetFileName(path);
                    wc.pcwszMemberFilePath = path;
                    wc.hMemberFile = IntPtr.Zero;
                    hashPtr = Marshal.AllocHGlobal(hash.Length);
                    Marshal.Copy(hash, 0, hashPtr, hash.Length);
                    wc.pbCalculatedFileHash = hashPtr;
                    wc.cbCalculatedFileHash = size;
                    wc.pcCatalogContext = IntPtr.Zero;

                    unionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustCatalogInfo)));
                    Marshal.StructureToPtr(wc, unionPtr, false);
                    return RunVerify(unionPtr, WtdChoiceCatalog);
                }
            }
            catch (DllNotFoundException) { return SignatureStatus.Error; }
            catch (EntryPointNotFoundException) { return SignatureStatus.Error; }
            catch { return SignatureStatus.Error; }
            finally
            {
                if (catInfo != IntPtr.Zero)
                {
                    try { CryptCATAdminReleaseCatalogContext(admin, catInfo, 0); } catch { }
                }
                if (admin != IntPtr.Zero)
                {
                    try { CryptCATAdminReleaseContext(admin, 0); } catch { }
                }
                if (hashPtr != IntPtr.Zero) Marshal.FreeHGlobal(hashPtr);
                if (unionPtr != IntPtr.Zero)
                {
                    try { Marshal.DestroyStructure(unionPtr, typeof(WinTrustCatalogInfo)); } catch { }
                    Marshal.FreeHGlobal(unionPtr);
                }
            }
        }

        private static SignatureStatus RunVerify(IntPtr unionPtr, uint choice)
        {
            IntPtr dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustData)));
            try
            {
                WinTrustData data = new WinTrustData();
                data.cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData));
                data.dwUIChoice = WtdUiNone;
                data.fdwRevocationChecks = WtdRevokeNone;
                data.dwUnionChoice = choice;
                data.pUnion = unionPtr;
                data.dwStateAction = WtdStateActionVerify;
                data.dwProvFlags = WtdCacheOnlyUrlRetrieval;
                Marshal.StructureToPtr(data, dataPtr, false);

                Guid action = ActionGenericVerifyV2;
                int hr = WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                // Luon dong trang thai de wintrust nha bo nho, ke ca khi loi.
                data = (WinTrustData)Marshal.PtrToStructure(dataPtr, typeof(WinTrustData));
                data.dwStateAction = WtdStateActionClose;
                Marshal.StructureToPtr(data, dataPtr, false);
                WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                return Map(hr);
            }
            finally { Marshal.FreeHGlobal(dataPtr); }
        }

        private static SignatureStatus Map(int hr)
        {
            if (hr == 0) return SignatureStatus.Signed;
            if (hr == TrustENosignature || hr == TrustESubjectFormUnknown)
                return SignatureStatus.Unsigned;
            if (hr == TrustEProviderUnknown) return SignatureStatus.Error;
            return SignatureStatus.Invalid;
        }
    }
}
