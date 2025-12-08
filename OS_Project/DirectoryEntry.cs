using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OS_Project
{
    internal class DirectoryEntry
    {
        public string Name8Dot3 { get; set; } = new string(' ', 11);
        public byte Attribute { get; set; } = 0;
        public int FirstCluster { get; set; } = 0;
        public int FileSize { get; set; } = 0;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Name8Dot3) || Name8Dot3[0] == (char)0x00;

        public string DisplayName
        {
            get { return DirectoryManager.Parse8Dot3Name(Name8Dot3); }
        }
    }

    internal class DirectoryManager
    {
        private readonly virtualDisk _disk;
        private readonly FatTableManager _fat;
        private const int ENTRY_SIZE = 32;
        private readonly int ENTRIES_PER_CLUSTER = FSConstants.CLUSTER_SIZE / ENTRY_SIZE;

        public DirectoryManager(virtualDisk disk, FatTableManager fat)
        {
            _disk = disk ?? throw new ArgumentNullException(nameof(disk));
            _fat = fat ?? throw new ArgumentNullException(nameof(fat));
        }

        public List<DirectoryEntry> ReadDirectory(int startCluster)
        {
            var entries = new List<DirectoryEntry>();
            List<int> chain = _fat.FollowChain(startCluster);

            foreach (int cluster in chain)
            {
                byte[] clusterBytes = _disk.ReadCluster(cluster);

                for (int i = 0; i < ENTRIES_PER_CLUSTER; i++)
                {
                    int offset = i * ENTRY_SIZE;
                    if (clusterBytes[offset] == 0x00) continue;

                    byte[] nameBytes = new byte[11];
                    Array.Copy(clusterBytes, offset, nameBytes, 0, 11);

                    byte attr = clusterBytes[offset + 11];
                    int firstCluster = BitConverter.ToInt32(clusterBytes, offset + 12);
                    int fileSize = BitConverter.ToInt32(clusterBytes, offset + 16);

                    var entry = new DirectoryEntry
                    {
                        Name8Dot3 = Encoding.ASCII.GetString(nameBytes),
                        Attribute = attr,
                        FirstCluster = firstCluster,
                        FileSize = fileSize
                    };

                    entries.Add(entry);
                }
            }

            return entries;
        }

        public DirectoryEntry FindDirectoryEntry(int startCluster, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string target = FormatNameTo8Dot3(name);
            List<int> chain = _fat.FollowChain(startCluster);

            foreach (int cluster in chain)
            {
                byte[] clusterBytes = _disk.ReadCluster(cluster);

                for (int i = 0; i < ENTRIES_PER_CLUSTER; i++)
                {
                    int offset = i * ENTRY_SIZE;
                    if (clusterBytes[offset] == 0x00) continue;

                    string rawName = Encoding.ASCII.GetString(clusterBytes, offset, 11);

                    if (string.Equals(rawName, target, StringComparison.OrdinalIgnoreCase))
                    {
                        byte attr = clusterBytes[offset + 11];
                        int firstCluster = BitConverter.ToInt32(clusterBytes, offset + 12);
                        int fileSize = BitConverter.ToInt32(clusterBytes, offset + 16);

                        return new DirectoryEntry
                        {
                            Name8Dot3 = rawName,
                            Attribute = attr,
                            FirstCluster = firstCluster,
                            FileSize = fileSize
                        };
                    }
                }
            }

            return null;
        }

        public void AddDirectoryEntry(int startCluster, DirectoryEntry newEntry)
        {
            if (newEntry == null) throw new ArgumentNullException(nameof(newEntry));
            if (newEntry.Name8Dot3 == null) throw new ArgumentException("Name must be set in 8.3 format");

            string targetName = newEntry.Name8Dot3;
            if (targetName.Length != 11) targetName = FormatNameTo8Dot3(targetName);

            var chain = _fat.FollowChain(startCluster);
            bool wrote = false;

            foreach (int cluster in chain)
            {
                byte[] clusterBytes = _disk.ReadCluster(cluster);

                for (int i = 0; i < ENTRIES_PER_CLUSTER; i++)
                {
                    int offset = i * ENTRY_SIZE;
                    if (clusterBytes[offset] == 0x00)
                    {
                        WriteEntryToBuffer(clusterBytes, offset, targetName, newEntry);
                        _disk.WriteCluster(cluster, clusterBytes);
                        _fat.FlushFatToDisk();
                        wrote = true;
                        break;
                    }
                }

                if (wrote) break;
            }

            if (!wrote)
            {
                int newCluster = _fat.AllocateChain(1);
                int lastCluster = chain.Last();
                _fat.SetFatEntry(lastCluster, newCluster);
                _fat.FlushFatToDisk();

                byte[] newClusterBytes = new byte[FSConstants.CLUSTER_SIZE];
                WriteEntryToBuffer(newClusterBytes, 0, targetName, newEntry);
                _disk.WriteCluster(newCluster, newClusterBytes);

                _fat.FlushFatToDisk();
            }
        }

        public bool RemoveDirectoryEntry(int startCluster, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string target = FormatNameTo8Dot3(name);
            List<int> chain = _fat.FollowChain(startCluster);

            foreach (int cluster in chain)
            {
                byte[] clusterBytes = _disk.ReadCluster(cluster);

                for (int i = 0; i < ENTRIES_PER_CLUSTER; i++)
                {
                    int offset = i * ENTRY_SIZE;
                    if (clusterBytes[offset] == 0x00) continue;

                    string rawName = Encoding.ASCII.GetString(clusterBytes, offset, 11);
                    if (string.Equals(rawName, target, StringComparison.OrdinalIgnoreCase))
                    {
                        int firstCluster = BitConverter.ToInt32(clusterBytes, offset + 12);

                        clusterBytes[offset] = 0x00;
                        for (int b = 1; b < ENTRY_SIZE; b++)
                            clusterBytes[offset + b] = 0x00;

                        _disk.WriteCluster(cluster, clusterBytes);

                        if (firstCluster > 0)
                        {
                            _fat.FreeChain(firstCluster);
                            _fat.FlushFatToDisk();
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private void WriteEntryToBuffer(byte[] buffer, int offset, string name8dot3, DirectoryEntry entry)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name8dot3);
            if (nameBytes.Length != 11)
            {
                Array.Resize(ref nameBytes, 11);
                for (int i = 0; i < 11; i++)
                    if (nameBytes[i] == 0) nameBytes[i] = (byte)' ';
            }

            Array.Copy(nameBytes, 0, buffer, offset, 11);
            buffer[offset + 11] = entry.Attribute;
            Array.Copy(BitConverter.GetBytes(entry.FirstCluster), 0, buffer, offset + 12, 4);
            Array.Copy(BitConverter.GetBytes(entry.FileSize), 0, buffer, offset + 16, 4);

            for (int i = offset + 20; i < offset + ENTRY_SIZE; i++)
                buffer[i] = 0x00;
        }

        public static string FormatNameTo8Dot3(string name)
        {
            if (string.IsNullOrEmpty(name)) return new string(' ', 11);

            name = name.Trim().ToUpperInvariant();

            string baseName = name;
            string ext = string.Empty;
            int dotIdx = name.LastIndexOf('.');
            if (dotIdx >= 0)
            {
                baseName = name.Substring(0, dotIdx);
                ext = name.Substring(dotIdx + 1);
            }

            baseName = SanitizeFor8Dot3(baseName);
            ext = SanitizeFor8Dot3(ext);

            if (baseName.Length > 8) baseName = baseName.Substring(0, 8);
            if (ext.Length > 3) ext = ext.Substring(0, 3);

            baseName = baseName.PadRight(8, ' ');
            ext = ext.PadRight(3, ' ');

            return baseName + ext;
        }

        private static string SanitizeFor8Dot3(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789$_~!#%&'()-@^`{}";
            var cleaned = new StringBuilder();
            foreach (char c in s)
            {
                char uc = char.ToUpperInvariant(c);
                if (allowed.IndexOf(uc) >= 0)
                    cleaned.Append(uc);
                else if (uc == ' ')
                    cleaned.Append(' ');
                else
                    cleaned.Append('_');
            }
            return cleaned.ToString();
        }

        public static string Parse8Dot3Name(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return string.Empty;

            if (rawName.Length < 11)
                rawName = rawName.PadRight(11, ' ');

            string name = rawName.Substring(0, 8).TrimEnd();
            string ext = rawName.Substring(8, 3).TrimEnd();

            return string.IsNullOrEmpty(ext) ? name : $"{name}.{ext}";
        }
    }
}
