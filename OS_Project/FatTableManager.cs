using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OS_Project
{
    internal class FatTableManager
    {
        private const int FatEntryCount = 1024;
        private const int ReservedClustersMax = FSConstants.FAT_END_CLUSTER;
        private readonly virtualDisk disk;
        private readonly int clusterSize = FSConstants.CLUSTER_SIZE;
        private readonly int[] fat;

        public FatTableManager(virtualDisk disk)
        {
            this.disk = disk;
            fat = new int[FatEntryCount];
            for (int i = 0; i < FatEntryCount; i++)
                fat[i] = 0;
        }

        public void LoadFatFromDisk()
        {
            byte[] all = new byte[(FSConstants.FAT_END_CLUSTER - FSConstants.FAT_START_CLUSTER + 1) * clusterSize];

            int pos = 0;
            for (int c = FSConstants.FAT_START_CLUSTER; c <= FSConstants.FAT_END_CLUSTER; c++)
            {
                byte[] clusterData = disk.ReadCluster(c);
                Array.Copy(clusterData, 0, all, pos, clusterSize);
                pos += clusterSize;
            }

            for (int i = 0; i < FatEntryCount; i++)
            {
                fat[i] = BitConverter.ToInt32(all, i * 4);
            }
        }

        public void FlushFatToDisk()
        {
            byte[] all = new byte[(FSConstants.FAT_END_CLUSTER - FSConstants.FAT_START_CLUSTER + 1) * clusterSize];

            for (int i = 0; i < FatEntryCount; i++)
            {
                byte[] entryBytes = BitConverter.GetBytes(fat[i]);
                Array.Copy(entryBytes, 0, all, i * 4, 4);
            }

            int pos = 0;
            for (int c = FSConstants.FAT_START_CLUSTER; c <= FSConstants.FAT_END_CLUSTER; c++)
            {
                byte[] chunk = new byte[clusterSize];
                Array.Copy(all, pos, chunk, 0, clusterSize);
                pos += clusterSize;
                disk.WriteCluster(c, chunk);
            }
        }

        public int GetFatEntry(int index)
        {
            ValidateIndex(index);
            return fat[index];
        }

        public void SetFatEntry(int index, int value)
        {
            ValidateIndex(index);
            fat[index] = value;
        }

        public int[] ReadAllFat()
        {
            int[] copy = new int[FatEntryCount];
            Array.Copy(fat, copy, FatEntryCount);
            return copy;
        }

        public void WriteAllFat(int[] entries)
        {
            if (entries.Length != FatEntryCount)
                throw new ArgumentException("Invalid FAT length.");
            Array.Copy(entries, fat, FatEntryCount);
        }

        public List<int> FollowChain(int startCluster)
        {
            ValidateCluster(startCluster);
            List<int> chain = new List<int>();
            int current = startCluster;

            while (current != -1)
            {
                chain.Add(current);
                int next = fat[current];

                if (next == 0)
                    throw new InvalidOperationException("Broken chain detected!");

                current = next;
            }
            return chain;
        }

        public int AllocateChain(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            List<int> free = new List<int>();

            for (int i = ReservedClustersMax + 1; i < FatEntryCount && free.Count < count; i++)
                if (fat[i] == 0)
                    free.Add(i);

            if (free.Count < count)
                throw new Exception("Not enough free clusters!");

            for (int i = 0; i < free.Count; i++)
                fat[free[i]] = (i == free.Count - 1) ? -1 : free[i + 1];

            return free[0];
        }

        public void FreeChain(int startCluster)
        {
            ValidateCluster(startCluster);

            int current = startCluster;
            while (current != -1)
            {
                int next = fat[current];
                fat[current] = 0;
                if (next <= 0) break;
                current = next;
            }
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= FatEntryCount)
                throw new ArgumentOutOfRangeException();
        }

        private void ValidateCluster(int cluster)
        {
            if (cluster <= ReservedClustersMax)
                throw new InvalidOperationException("Cluster is reserved!");
        }
    }
}
