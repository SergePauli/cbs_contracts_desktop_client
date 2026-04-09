using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace CbsContractsDesktopClient.Services
{
    public class CredentialManagerService : ICredentialManagerService
    {
        private const string TargetName = "CBS.Contracts.Desktop";
        private const int CredTypeGeneric = 1;
        private const int CredPersistLocalMachine = 2;

        public SavedCredentials? TryGetCredentials()
        {
            if (!CredRead(TargetName, CredTypeGeneric, 0, out var credentialPtr) || credentialPtr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                var username = credential.UserName ?? string.Empty;
                var password = ReadSecret(credential.CredentialBlob, credential.CredentialBlobSize);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                {
                    return null;
                }

                return new SavedCredentials(username, password);
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }

        public void SaveCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return;
            }

            var secretBytes = Encoding.Unicode.GetBytes(password);
            var secretPtr = Marshal.AllocCoTaskMem(secretBytes.Length);

            try
            {
                Marshal.Copy(secretBytes, 0, secretPtr, secretBytes.Length);

                var credential = new CREDENTIAL
                {
                    Type = CredTypeGeneric,
                    TargetName = TargetName,
                    CredentialBlobSize = (uint)secretBytes.Length,
                    CredentialBlob = secretPtr,
                    Persist = CredPersistLocalMachine,
                    UserName = username
                };

                if (!CredWrite(ref credential, 0))
                {
                    throw new InvalidOperationException($"Не удалось сохранить учетные данные. Win32={Marshal.GetLastWin32Error()}");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(secretPtr);
            }
        }

        public void DeleteCredentials()
        {
            CredDelete(TargetName, CredTypeGeneric, 0);
        }

        private static string ReadSecret(IntPtr secretPtr, uint secretSize)
        {
            if (secretPtr == IntPtr.Zero || secretSize == 0)
            {
                return string.Empty;
            }

            var secretBytes = new byte[secretSize];
            Marshal.Copy(secretPtr, secretBytes, 0, (int)secretSize);
            return Encoding.Unicode.GetString(secretBytes).TrimEnd('\0');
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr credentialPtr);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }
    }

    public record SavedCredentials(string Username, string Password);
}
